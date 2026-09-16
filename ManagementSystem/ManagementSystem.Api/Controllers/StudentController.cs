using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagementSystem.Api.Data;
using ManagementSystem.Api.Models;
using System.Text.RegularExpressions;
using System.Security.Claims;
using ManagementSystem.Api.Constants;
using ManagementSystem.Api.Common;

namespace ManagementSystem.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StudentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public StudentController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StudentResponseDto>>> GetStudents([FromQuery] string? search)
        {
            IQueryable<Student> query;

            if (User.IsInRole(AppRoles.Admin))
            {
                query = _context.Students.Include(s => s.Class).AsQueryable();
            }
            else if (User.IsInRole(AppRoles.Lecture))
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                var myClassIds = await _context.WorkSchedules
                    .Where(w => w.UserId == userId)
                    .SelectMany(w => w.Classes.Select(c => c.Id))
                    .Distinct()
                    .ToListAsync();

                var userPhone = PhoneHelper.NormalizePhone(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.MobilePhone)?.Value ?? User.Identity?.Name);
                if (User.IsInRole(AppRoles.User) && !string.IsNullOrEmpty(userPhone))
                {
                    query = _context.Students.Include(s => s.Class)
                        .Where(s => (s.ClassId.HasValue && myClassIds.Contains(s.ClassId.Value)) || s.ParentPhoneCanonical == userPhone);
                }
                else
                {
                    query = _context.Students.Include(s => s.Class)
                        .Where(s => s.ClassId.HasValue && myClassIds.Contains(s.ClassId.Value));
                }
            }
            else if (User.IsInRole(AppRoles.User))
            {
                var userPhone = PhoneHelper.NormalizePhone(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.MobilePhone)?.Value ?? User.Identity?.Name);
                if (string.IsNullOrEmpty(userPhone)) return Forbid();

                query = _context.Students.Include(s => s.Class)
                    .Where(s => s.ParentPhoneCanonical == userPhone);
            }
            else
            {
                return Forbid();
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(s => s.FullName.Contains(search) || s.ParentPhoneNumber.Contains(search));
            }

            return await query
                .Select(s => new StudentResponseDto
                {
                    Id = s.Id,
                    FullName = s.FullName,
                    DateOfBirth = s.DateOfBirth,
                    ParentName = s.ParentName,
                    ParentPhoneNumber = s.ParentPhoneNumber,
                    TotalRemainingSessions = s.TotalRemainingSessions,
                    TotalAttendedSessions = s.TotalAttendedSessions,
                    TotalPaidSessions = s.TotalPaidSessions,
                    ClassId = s.ClassId,
                    ClassName = s.Class != null ? s.Class.ClassName : null,
                    IsDeleted = s.IsDeleted
                })
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<StudentResponseDto>> GetStudent(int id)
        {
            var s = await _context.Students.Include(s => s.Class).FirstOrDefaultAsync(s => s.Id == id);

            if (s == null)
            {
                return NotFound();
            }

            // Enforce resource-level authorization (Step 7)
            if (!User.IsInRole(AppRoles.Admin))
            {
                var userPhone = PhoneHelper.NormalizePhone(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.MobilePhone)?.Value ?? User.Identity?.Name);
                bool isParentOwner = User.IsInRole(AppRoles.User) && !string.IsNullOrEmpty(userPhone) && s.ParentPhoneCanonical == userPhone;

                if (User.IsInRole(AppRoles.Lecture))
                {
                    var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                    bool isAssignedLecture = s.ClassId.HasValue && await _context.WorkSchedules
                        .AnyAsync(w => w.UserId == userId && w.Classes.Any(c => c.Id == s.ClassId.Value));

                    if (!isAssignedLecture && !isParentOwner)
                    {
                        return Forbid();
                    }
                }
                else if (User.IsInRole(AppRoles.User))
                {
                    if (!isParentOwner)
                    {
                        return Forbid();
                    }
                }
                else
                {
                    return Forbid();
                }
            }

            var attendanceCount = await _context.Attendances.CountAsync(a => a.StudentId == id);
            var presentCount = await _context.Attendances.CountAsync(a => a.StudentId == id && a.IsPresent);
            int attendanceRate = attendanceCount == 0 ? 100 : (int)Math.Round((double)presentCount / attendanceCount * 100);

            return new StudentResponseDto
            {
                Id = s.Id,
                FullName = s.FullName,
                DateOfBirth = s.DateOfBirth,
                ParentName = s.ParentName,
                ParentPhoneNumber = s.ParentPhoneNumber,
                TotalRemainingSessions = s.TotalRemainingSessions,
                TotalAttendedSessions = s.TotalAttendedSessions,
                TotalPaidSessions = s.TotalPaidSessions,
                ClassId = s.ClassId,
                ClassName = s.Class?.ClassName,
                AttendanceRate = attendanceRate,
                IsDeleted = s.IsDeleted
            };
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<ActionResult<StudentResponseDto>> PostStudent(StudentDto dto)
        {
            var rawPhone = dto.ParentPhoneNumber ?? "";
            string validPhone = string.Empty;
            if (!string.IsNullOrWhiteSpace(rawPhone) && !PhoneHelper.TryNormalizePhone(rawPhone, out validPhone))
            {
                return BadRequest(new { message = "Số điện thoại phụ huynh không hợp lệ." });
            }

            var trimmedName = (dto.FullName ?? "").Trim();
            if (string.IsNullOrEmpty(trimmedName))
            {
                return BadRequest(new { message = "Tên học sinh không được để trống." });
            }

            var normName = trimmedName.ToLowerInvariant();
            var activeExists = await _context.Students
                .AnyAsync(s => !s.IsDeleted && s.NormalizedFullName == normName && s.ParentPhoneCanonical == validPhone);
            if (activeExists)
            {
                return Conflict(new { message = $"Học sinh '{dto.FullName}' với số điện thoại phụ huynh này đã tồn tại trong hệ thống." });
            }

            var parentName = dto.ParentName ?? "";
            if (rawPhone != validPhone && Regex.IsMatch(rawPhone, @"[a-zA-Z]"))
            {
                parentName = string.IsNullOrWhiteSpace(parentName) ? rawPhone : $"{parentName} ({rawPhone})";
            }

            var validClassId = (dto.ClassId.HasValue && dto.ClassId.Value > 0) ? dto.ClassId : null;

            var student = new Student
            {
                FullName = trimmedName,
                DateOfBirth = dto.DateOfBirth,
                ParentName = parentName,
                ParentPhoneNumber = validPhone,
                TotalAttendedSessions = dto.TotalAttendedSessions,
                TotalPaidSessions = dto.TotalPaidSessions,
                ClassId = validClassId
            };

            await UpdateRemainingSessionsAsync(student);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Students.Add(student);
                await _context.SaveChangesAsync();

                // Step 6: Tự động tạo hoặc gán quyền User cho phụ huynh (Lecture -> Lecture + User)
                if (!string.IsNullOrWhiteSpace(validPhone))
                {
                    var (parentUser, collisionError) = await FindParentUserByCanonicalPhoneAsync(validPhone);
                    if (collisionError != null)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return Conflict(new { message = collisionError });
                    }

                    if (parentUser == null)
                    {
                        var newUser = new AppUser
                        {
                            UserName = validPhone,
                            Email = validPhone + "@example.com",
                            PhoneNumber = validPhone,
                            FullName = parentName
                        };
                        var result = await _userManager.CreateAsync(newUser, validPhone + "@123Aa");
                        if (!result.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            _context.ChangeTracker.Clear();
                            return BadRequest(result.Errors);
                        }
                        var roleResult = await _userManager.AddToRoleAsync(newUser, AppRoles.User);
                        if (!roleResult.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            _context.ChangeTracker.Clear();
                            return BadRequest(roleResult.Errors);
                        }
                    }
                    else if (!await _userManager.IsInRoleAsync(parentUser, AppRoles.Admin))
                    {
                        if (PhoneHelper.NormalizePhone(parentUser.UserName) == validPhone && parentUser.UserName != validPhone)
                        {
                            var uNameRes = await _userManager.SetUserNameAsync(parentUser, validPhone);
                            if (!uNameRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(uNameRes.Errors);
                            }
                            var emailRes = await _userManager.SetEmailAsync(parentUser, validPhone + "@example.com");
                            if (!emailRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(emailRes.Errors);
                            }
                        }

                        if (parentUser.PhoneNumber != validPhone)
                        {
                            parentUser.PhoneNumber = validPhone;
                            var updateResult = await _userManager.UpdateAsync(parentUser);
                            if (!updateResult.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(updateResult.Errors);
                            }
                        }

                        if (!await _userManager.IsInRoleAsync(parentUser, AppRoles.User))
                        {
                            var roleResult = await _userManager.AddToRoleAsync(parentUser, AppRoles.User);
                            if (!roleResult.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(roleResult.Errors);
                            }
                            var stampResult = await _userManager.UpdateSecurityStampAsync(parentUser);
                            if (!stampResult.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(stampResult.Errors);
                            }
                        }

                        if (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(parentUser))
                        {
                            var lockoutResult = await _userManager.SetLockoutEndDateAsync(parentUser, null);
                            if (!lockoutResult.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(lockoutResult.Errors);
                            }
                            var resetCountResult = await _userManager.ResetAccessFailedCountAsync(parentUser);
                            if (!resetCountResult.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(resetCountResult.Errors);
                            }
                        }
                    }
                }

                await transaction.CommitAsync();

                return CreatedAtAction(nameof(GetStudent), new { id = student.Id }, new StudentResponseDto
                {
                    Id = student.Id,
                    FullName = student.FullName,
                    DateOfBirth = student.DateOfBirth,
                    ParentName = student.ParentName,
                    ParentPhoneNumber = student.ParentPhoneNumber,
                    TotalRemainingSessions = student.TotalRemainingSessions,
                    TotalAttendedSessions = student.TotalAttendedSessions,
                    TotalPaidSessions = student.TotalPaidSessions,
                    ClassId = student.ClassId,
                    IsDeleted = student.IsDeleted
                });
            }
            catch (Exception ex) when (DbErrorHelper.IsUniqueViolation(ex))
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return Conflict(new { message = "Học sinh với tên và số điện thoại này đã tồn tại trong hệ thống." });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> PutStudent(int id, StudentDto dto)
        {
            var rawPhone = dto.ParentPhoneNumber ?? "";
            string validPhone = string.Empty;
            if (!string.IsNullOrWhiteSpace(rawPhone) && !PhoneHelper.TryNormalizePhone(rawPhone, out validPhone))
            {
                return BadRequest(new { message = "Số điện thoại phụ huynh không hợp lệ." });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existing = await _context.Students
                    .FromSqlRaw("SELECT * FROM \"Students\" WHERE \"Id\" = {0} FOR UPDATE", id)
                    .SingleOrDefaultAsync();
                if (existing == null)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return NotFound();
                }

                var oldCanon = existing.ParentPhoneCanonical;
                var parentName = dto.ParentName ?? "";
                if (rawPhone != validPhone && Regex.IsMatch(rawPhone, @"[a-zA-Z]"))
                {
                    parentName = string.IsNullOrWhiteSpace(parentName) ? rawPhone : $"{parentName} ({rawPhone})";
                }

                existing.FullName = dto.FullName;
                existing.DateOfBirth = dto.DateOfBirth;
                existing.ParentName = parentName;
                existing.ParentPhoneNumber = validPhone;
                existing.TotalAttendedSessions = dto.TotalAttendedSessions;
                existing.TotalPaidSessions = dto.TotalPaidSessions;
                existing.ClassId = (dto.ClassId.HasValue && dto.ClassId.Value > 0) ? dto.ClassId : null;

                await UpdateRemainingSessionsAsync(existing);
                await _context.SaveChangesAsync();

                // Xử lý tài khoản khi thay đổi số điện thoại phụ huynh
                if (!string.IsNullOrWhiteSpace(oldCanon) && oldCanon != validPhone)
                {
                    var oldPhoneHasStudents = await _context.Students
                        .AnyAsync(s => s.Id != existing.Id && !s.IsDeleted && s.ParentPhoneCanonical == oldCanon);

                    if (!oldPhoneHasStudents)
                    {
                        var (oldUser, _) = await FindParentUserByCanonicalPhoneAsync(oldCanon);
                        if (oldUser != null && !await _userManager.IsInRoleAsync(oldUser, AppRoles.Admin) && !await _userManager.IsInRoleAsync(oldUser, AppRoles.Lecture))
                        {
                            if (_userManager.SupportsUserLockout)
                            {
                                var lockoutRes = await _userManager.SetLockoutEndDateAsync(oldUser, DateTimeOffset.UtcNow.AddYears(100));
                                if (!lockoutRes.Succeeded)
                                {
                                    await transaction.RollbackAsync();
                                    _context.ChangeTracker.Clear();
                                    return BadRequest(lockoutRes.Errors);
                                }
                            }
                            var stampRes = await _userManager.UpdateSecurityStampAsync(oldUser);
                            if (!stampRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(stampRes.Errors);
                            }
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(validPhone))
                {
                    var (parentUser, collisionError) = await FindParentUserByCanonicalPhoneAsync(validPhone);
                    if (collisionError != null)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return Conflict(new { message = collisionError });
                    }

                    if (parentUser == null)
                    {
                        var newUser = new AppUser
                        {
                            UserName = validPhone,
                            Email = validPhone + "@example.com",
                            PhoneNumber = validPhone,
                            FullName = parentName ?? "Phụ huynh " + existing.FullName
                        };
                        var result = await _userManager.CreateAsync(newUser, validPhone + "@123Aa");
                        if (!result.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            _context.ChangeTracker.Clear();
                            return BadRequest(result.Errors);
                        }
                        var roleRes = await _userManager.AddToRoleAsync(newUser, AppRoles.User);
                        if (!roleRes.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            _context.ChangeTracker.Clear();
                            return BadRequest(roleRes.Errors);
                        }
                    }
                    else if (!await _userManager.IsInRoleAsync(parentUser, AppRoles.Admin))
                    {
                        if (PhoneHelper.NormalizePhone(parentUser.UserName) == validPhone && parentUser.UserName != validPhone)
                        {
                            var uNameRes = await _userManager.SetUserNameAsync(parentUser, validPhone);
                            if (!uNameRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(uNameRes.Errors);
                            }
                            var emailRes = await _userManager.SetEmailAsync(parentUser, validPhone + "@example.com");
                            if (!emailRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(emailRes.Errors);
                            }
                        }

                        if (parentUser.PhoneNumber != validPhone)
                        {
                            parentUser.PhoneNumber = validPhone;
                            var updateRes = await _userManager.UpdateAsync(parentUser);
                            if (!updateRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(updateRes.Errors);
                            }
                        }

                        if (!await _userManager.IsInRoleAsync(parentUser, AppRoles.User))
                        {
                            var roleRes = await _userManager.AddToRoleAsync(parentUser, AppRoles.User);
                            if (!roleRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(roleRes.Errors);
                            }
                            var stampRes = await _userManager.UpdateSecurityStampAsync(parentUser);
                            if (!stampRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(stampRes.Errors);
                            }
                        }

                        if (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(parentUser))
                        {
                            var lockRes = await _userManager.SetLockoutEndDateAsync(parentUser, null);
                            if (!lockRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(lockRes.Errors);
                            }
                            var resetCountRes = await _userManager.ResetAccessFailedCountAsync(parentUser);
                            if (!resetCountRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(resetCountRes.Errors);
                            }
                        }
                    }
                }

                await transaction.CommitAsync();
                return NoContent();
            }
            catch (Exception ex) when (DbErrorHelper.IsUniqueViolation(ex))
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return Conflict(new { message = "Học sinh với tên và số điện thoại này đã tồn tại trong hệ thống." });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existing = await _context.Students.FindAsync(id);
                if (existing == null)
                {
                    return NotFound();
                }

                _context.Students.Remove(existing);
                await _context.SaveChangesAsync();

                // Lock out parent account if no active students remain (Bug #02)
                var parentCanon = existing.ParentPhoneCanonical;
                if (!string.IsNullOrWhiteSpace(parentCanon))
                {
                    var hasRemaining = await _context.Students.AnyAsync(s => !s.IsDeleted && s.ParentPhoneCanonical == parentCanon);
                    if (!hasRemaining)
                    {
                        var (parentUser, _) = await FindParentUserByCanonicalPhoneAsync(parentCanon);
                        if (parentUser != null && !await _userManager.IsInRoleAsync(parentUser, AppRoles.Admin) && !await _userManager.IsInRoleAsync(parentUser, AppRoles.Lecture))
                        {
                            if (_userManager.SupportsUserLockout)
                            {
                                var lockRes = await _userManager.SetLockoutEndDateAsync(parentUser, DateTimeOffset.UtcNow.AddYears(100));
                                if (!lockRes.Succeeded)
                                {
                                    await transaction.RollbackAsync();
                                    _context.ChangeTracker.Clear();
                                    return BadRequest(lockRes.Errors);
                                }
                            }
                            var stampRes = await _userManager.UpdateSecurityStampAsync(parentUser);
                            if (!stampRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(stampRes.Errors);
                            }
                        }
                    }
                }

                await transaction.CommitAsync();
                return NoContent();
            }
            catch (Exception ex) when (DbErrorHelper.IsTransientConcurrencyError(ex))
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return StatusCode(StatusCodes.Status409Conflict, new { message = "Xung đột đồng thời khi xóa học sinh, vui lòng thử lại." });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        [HttpDelete("bulk")]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> BulkDeleteStudents([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any())
                return BadRequest("Không có học sinh nào được chọn.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var studentsToDelete = await _context.Students.Where(s => ids.Contains(s.Id)).ToListAsync();

                if (!studentsToDelete.Any())
                    return NotFound("Không tìm thấy học sinh nào.");

                var parentCanonicalPhones = studentsToDelete
                    .Select(s => s.ParentPhoneCanonical)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct()
                    .ToList();

                _context.Students.RemoveRange(studentsToDelete);
                await _context.SaveChangesAsync();

                // Lock out parents that have no active students remaining (Bug #03)
                foreach (var canonPhone in parentCanonicalPhones)
                {
                    var hasRemaining = await _context.Students.AnyAsync(s => !s.IsDeleted && s.ParentPhoneCanonical == canonPhone);
                    if (!hasRemaining)
                    {
                        var (parentUser, _) = await FindParentUserByCanonicalPhoneAsync(canonPhone);
                        if (parentUser != null && !await _userManager.IsInRoleAsync(parentUser, AppRoles.Admin) && !await _userManager.IsInRoleAsync(parentUser, AppRoles.Lecture))
                        {
                            if (_userManager.SupportsUserLockout)
                            {
                                var lockRes = await _userManager.SetLockoutEndDateAsync(parentUser, DateTimeOffset.UtcNow.AddYears(100));
                                if (!lockRes.Succeeded)
                                {
                                    await transaction.RollbackAsync();
                                    _context.ChangeTracker.Clear();
                                    return BadRequest(lockRes.Errors);
                                }
                            }
                            var stampRes = await _userManager.UpdateSecurityStampAsync(parentUser);
                            if (!stampRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(stampRes.Errors);
                            }
                        }
                    }
                }

                await transaction.CommitAsync();
                return NoContent();
            }
            catch (Exception ex) when (DbErrorHelper.IsTransientConcurrencyError(ex))
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return StatusCode(StatusCodes.Status409Conflict, new { message = "Xung đột đồng thời khi xóa học sinh, vui lòng thử lại." });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        [HttpGet("deleted")]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<ActionResult<IEnumerable<StudentResponseDto>>> GetDeletedStudents()
        {
            var students = await _context.Students
                .IgnoreQueryFilters()
                .Include(s => s.Class)
                .Where(s => s.IsDeleted)
                .Select(s => new StudentResponseDto
                {
                    Id = s.Id,
                    FullName = s.FullName,
                    DateOfBirth = s.DateOfBirth,
                    ParentName = s.ParentName,
                    ParentPhoneNumber = s.ParentPhoneNumber,
                    TotalRemainingSessions = s.TotalRemainingSessions,
                    TotalAttendedSessions = s.TotalAttendedSessions,
                    TotalPaidSessions = s.TotalPaidSessions,
                    ClassId = s.ClassId,
                    ClassName = s.Class != null ? s.Class.ClassName : null
                })
                .ToListAsync();

            return Ok(students);
        }

        [HttpPut("recover")]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> RecoverStudents([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any())
                return BadRequest("Không có học sinh nào được chọn.");

            var distinctIds = ids.Distinct().ToList();
            if (distinctIds.Count != ids.Count)
            {
                return BadRequest("Danh sách ID học sinh chọn khôi phục có chứa giá trị trùng lặp.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var studentsToRecover = await _context.Students
                    .IgnoreQueryFilters()
                    .Where(s => distinctIds.Contains(s.Id) && s.IsDeleted)
                    .ToListAsync();

                if (!studentsToRecover.Any())
                {
                    await transaction.RollbackAsync();
                    return NotFound("Không tìm thấy học sinh nào.");
                }

                // Kiểm tra xung đột trùng lặp ngay trong danh sách được chọn khôi phục
                var selectedIdentities = new HashSet<(string Name, string Phone)>();
                foreach (var student in studentsToRecover)
                {
                    var normName = student.NormalizedFullName ?? (student.FullName ?? "").Trim().ToLowerInvariant();
                    var canonPhone = student.ParentPhoneCanonical ?? PhoneHelper.NormalizePhone(student.ParentPhoneNumber);
                    var key = (normName, canonPhone);
                    if (!selectedIdentities.Add(key))
                    {
                        await transaction.RollbackAsync();
                        return Conflict(new { message = $"Danh sách học sinh chọn khôi phục có nhiều hơn 1 học sinh cùng định danh: '{student.FullName}' (SĐT: {student.ParentPhoneNumber})." });
                    }
                }

                // Kiểm tra xung đột với các học sinh đang hoạt động bằng 1 truy vấn bulk có giới hạn
                var candidateNames = selectedIdentities.Select(k => k.Name).Distinct().ToList();
                var candidatePhones = selectedIdentities.Select(k => k.Phone).Distinct().ToList();

                var activeCollisions = await _context.Students
                    .Where(s => !s.IsDeleted && candidateNames.Contains(s.NormalizedFullName) && candidatePhones.Contains(s.ParentPhoneCanonical))
                    .Select(s => new { s.NormalizedFullName, s.ParentPhoneCanonical, s.FullName, s.ParentPhoneNumber })
                    .ToListAsync();

                var activeCollisionSet = new HashSet<(string Name, string Phone)>(
                    activeCollisions.Select(c => (c.NormalizedFullName, c.ParentPhoneCanonical))
                );

                var conflictErrors = new List<string>();
                foreach (var student in studentsToRecover)
                {
                    var normName = student.NormalizedFullName ?? (student.FullName ?? "").Trim().ToLowerInvariant();
                    var canonPhone = student.ParentPhoneCanonical ?? PhoneHelper.NormalizePhone(student.ParentPhoneNumber);
                    if (activeCollisionSet.Contains((normName, canonPhone)))
                    {
                        conflictErrors.Add($"Học sinh '{student.FullName}' (SĐT: {student.ParentPhoneNumber}) đã có bản ghi đang hoạt động trong hệ thống.");
                    }
                }

                if (conflictErrors.Any())
                {
                    await transaction.RollbackAsync();
                    return Conflict(new { message = "Không thể khôi phục một số học sinh do bị trùng lặp dữ liệu.", errors = conflictErrors });
                }

                // Bulk load danh sách ID các lớp đang active
                var referencedClassIds = studentsToRecover
                    .Where(s => s.ClassId.HasValue)
                    .Select(s => s.ClassId!.Value)
                    .Distinct()
                    .ToList();

                var activeClassIds = referencedClassIds.Count > 0
                    ? await _context.Classes.Where(c => referencedClassIds.Contains(c.Id)).Select(c => c.Id).ToListAsync()
                    : new List<int>();
                var activeClassSet = new HashSet<int>(activeClassIds);

                // Bulk load tài khoản phụ huynh theo CanonicalPhone
                var nonBlankPhones = candidatePhones.Where(p => !string.IsNullOrEmpty(p)).ToList();
                var parentUsers = nonBlankPhones.Count > 0
                    ? await _context.Users.Where(u => u.CanonicalPhone != null && nonBlankPhones.Contains(u.CanonicalPhone)).ToListAsync()
                    : new List<AppUser>();
                var parentUserDict = parentUsers
                    .GroupBy(u => u.CanonicalPhone!, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

                var missingPhones = nonBlankPhones.Except(parentUserDict.Keys).ToList();
                if (missingPhones.Count > 0)
                {
                    var directUsers = await _context.Users.Where(u => missingPhones.Contains(u.UserName!)).ToListAsync();
                    foreach (var du in directUsers)
                    {
                        var p = PhoneHelper.NormalizePhone(du.UserName);
                        if (!string.IsNullOrEmpty(p) && !parentUserDict.ContainsKey(p))
                        {
                            parentUserDict[p] = du;
                        }
                    }
                }

                var parentUserIds = parentUserDict.Values.Select(u => u.Id).Distinct().ToList();
                var userRoleEntries = parentUserIds.Count > 0
                    ? await (from ur in _context.UserRoles
                             join r in _context.Roles on ur.RoleId equals r.Id
                             where parentUserIds.Contains(ur.UserId)
                             select new { ur.UserId, RoleName = r.Name }).ToListAsync()
                    : new();

                var userRolesMap = userRoleEntries
                    .GroupBy(x => x.UserId)
                    .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(x => x.RoleName!)));

                foreach (var student in studentsToRecover)
                {
                    student.IsDeleted = false;
                    student.DeletedAt = null;

                    var canonicalPhone = student.ParentPhoneCanonical ?? PhoneHelper.NormalizePhone(student.ParentPhoneNumber);
                    if (!string.IsNullOrEmpty(canonicalPhone) && student.ParentPhoneNumber != canonicalPhone)
                    {
                        student.ParentPhoneNumber = canonicalPhone;
                    }

                    // Nếu lớp cũ đã bị xóa mềm, gỡ ClassId để tránh reference hỏng (SC-09)
                    if (student.ClassId.HasValue && !activeClassSet.Contains(student.ClassId.Value))
                    {
                        student.ClassId = null;
                    }

                    _context.Entry(student).State = EntityState.Modified;

                    if (!string.IsNullOrWhiteSpace(canonicalPhone) && parentUserDict.TryGetValue(canonicalPhone, out var parentUser))
                    {
                        var roles = userRolesMap.TryGetValue(parentUser.Id, out var rSet) ? rSet : new HashSet<string>();
                        if (roles.Contains(AppRoles.Admin))
                        {
                            continue;
                        }

                        if (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(parentUser))
                        {
                            var lockRes = await _userManager.SetLockoutEndDateAsync(parentUser, null);
                            if (!lockRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(lockRes.Errors);
                            }
                            var resetCountRes = await _userManager.ResetAccessFailedCountAsync(parentUser);
                            if (!resetCountRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(resetCountRes.Errors);
                            }
                        }

                        if (!roles.Contains(AppRoles.User))
                        {
                            var roleRes = await _userManager.AddToRoleAsync(parentUser, AppRoles.User);
                            if (!roleRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(roleRes.Errors);
                            }
                            roles.Add(AppRoles.User);

                            var stampRes = await _userManager.UpdateSecurityStampAsync(parentUser);
                            if (!stampRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(stampRes.Errors);
                            }
                        }

                        if (PhoneHelper.NormalizePhone(parentUser.UserName) == canonicalPhone && parentUser.UserName != canonicalPhone)
                        {
                            var uNameRes = await _userManager.SetUserNameAsync(parentUser, canonicalPhone);
                            if (!uNameRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(uNameRes.Errors);
                            }
                            var emailRes = await _userManager.SetEmailAsync(parentUser, canonicalPhone + "@example.com");
                            if (!emailRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(emailRes.Errors);
                            }
                        }

                        if (parentUser.PhoneNumber != canonicalPhone)
                        {
                            parentUser.PhoneNumber = canonicalPhone;
                            var updateRes = await _userManager.UpdateAsync(parentUser);
                            if (!updateRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(updateRes.Errors);
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return NoContent();
            }
            catch (Exception ex) when (DbErrorHelper.IsUniqueViolation(ex))
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return Conflict(new { message = "Khôi phục thất bại do xung đột dữ liệu học sinh đang hoạt động." });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        [HttpPost("sync-accounts")]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> SyncAccounts()
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Lấy toàn bộ học sinh active (áp dụng Global Query Filter !IsDeleted)
                var students = await _context.Students.ToListAsync();
                int createdCount = 0;
                int unlockedCount = 0;

                // Lưu các SĐT đã xử lý tài khoản Identity để tránh query DB lặp lại đối với phụ huynh nhiều con
                var processedPhones = new HashSet<string>();

                foreach (var student in students)
                {
                    var rawPhone = student.ParentPhoneNumber;
                    var validPhone = !string.IsNullOrEmpty(student.ParentPhoneCanonical)
                        ? student.ParentPhoneCanonical
                        : PhoneHelper.NormalizePhone(rawPhone);

                    // Bỏ qua nếu không trích xuất được số điện thoại hợp lệ
                    if (string.IsNullOrWhiteSpace(validPhone)) continue;

                    // === PHASE 1: Chuẩn hóa bản ghi học sinh trong DB (áp dụng cho từng học sinh) ===
                    if (rawPhone != validPhone && Regex.IsMatch(rawPhone ?? "", @"[a-zA-Z]"))
                    {
                        student.ParentName = string.IsNullOrWhiteSpace(student.ParentName)
                            ? (rawPhone ?? "")
                            : $"{student.ParentName} ({rawPhone})";
                    }

                    if (student.ParentPhoneNumber != validPhone)
                    {
                        student.ParentPhoneNumber = validPhone;
                    }

                    // === PHASE 2: Xử lý tài khoản Identity (Đảm bảo mỗi SĐT duy nhất chỉ query UserManager đúng 1 lần) ===
                    if (!processedPhones.Add(validPhone)) continue;

                    var (parentUser, collisionError) = await FindParentUserByCanonicalPhoneAsync(validPhone);
                    if (collisionError != null)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return Conflict(new { message = collisionError });
                    }

                    if (parentUser == null)
                    {
                        var newUser = new AppUser
                        {
                            UserName = validPhone,
                            Email = validPhone + "@example.com",
                            PhoneNumber = validPhone,
                            FullName = !string.IsNullOrWhiteSpace(student.ParentName)
                                ? student.ParentName
                                : ("Phụ huynh " + student.FullName)
                        };

                        var result = await _userManager.CreateAsync(newUser, validPhone + "@123Aa");
                        if (!result.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            _context.ChangeTracker.Clear();
                            return BadRequest(result.Errors);
                        }
                        var roleRes = await _userManager.AddToRoleAsync(newUser, AppRoles.User);
                        if (!roleRes.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            _context.ChangeTracker.Clear();
                            return BadRequest(roleRes.Errors);
                        }
                        createdCount++;
                    }
                    else
                    {
                        if (!await _userManager.IsInRoleAsync(parentUser, AppRoles.Admin))
                        {
                            if (PhoneHelper.NormalizePhone(parentUser.UserName) == validPhone && parentUser.UserName != validPhone)
                            {
                                var uNameRes = await _userManager.SetUserNameAsync(parentUser, validPhone);
                                if (!uNameRes.Succeeded)
                                {
                                    await transaction.RollbackAsync();
                                    _context.ChangeTracker.Clear();
                                    return BadRequest(uNameRes.Errors);
                                }
                                var emailRes = await _userManager.SetEmailAsync(parentUser, validPhone + "@example.com");
                                if (!emailRes.Succeeded)
                                {
                                    await transaction.RollbackAsync();
                                    _context.ChangeTracker.Clear();
                                    return BadRequest(emailRes.Errors);
                                }
                            }

                            if (parentUser.PhoneNumber != validPhone)
                            {
                                parentUser.PhoneNumber = validPhone;
                                var updateRes = await _userManager.UpdateAsync(parentUser);
                                if (!updateRes.Succeeded)
                                {
                                    await transaction.RollbackAsync();
                                    _context.ChangeTracker.Clear();
                                    return BadRequest(updateRes.Errors);
                                }
                            }

                            if (!await _userManager.IsInRoleAsync(parentUser, AppRoles.User))
                            {
                                var roleRes = await _userManager.AddToRoleAsync(parentUser, AppRoles.User);
                                if (!roleRes.Succeeded)
                                {
                                    await transaction.RollbackAsync();
                                    _context.ChangeTracker.Clear();
                                    return BadRequest(roleRes.Errors);
                                }
                                var stampRes = await _userManager.UpdateSecurityStampAsync(parentUser);
                                if (!stampRes.Succeeded)
                                {
                                    await transaction.RollbackAsync();
                                    _context.ChangeTracker.Clear();
                                    return BadRequest(stampRes.Errors);
                                }
                            }

                            if (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(parentUser))
                            {
                                if (!await _userManager.IsInRoleAsync(parentUser, AppRoles.Lecture))
                                {
                                    var lockRes = await _userManager.SetLockoutEndDateAsync(parentUser, null);
                                    if (!lockRes.Succeeded)
                                    {
                                        await transaction.RollbackAsync();
                                        _context.ChangeTracker.Clear();
                                        return BadRequest(lockRes.Errors);
                                    }
                                    var resetCountRes = await _userManager.ResetAccessFailedCountAsync(parentUser);
                                    if (!resetCountRes.Succeeded)
                                    {
                                        await transaction.RollbackAsync();
                                        _context.ChangeTracker.Clear();
                                        return BadRequest(resetCountRes.Errors);
                                    }
                                    unlockedCount++;
                                }
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = $"Đã đồng bộ thành công: tạo mới {createdCount} và mở khóa {unlockedCount} tài khoản phụ huynh.",
                    createdCount,
                    unlockedCount
                });
            }
            catch (Exception ex) when (DbErrorHelper.IsUniqueViolation(ex))
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return Conflict(new { message = "Đồng bộ tài khoản thất bại do xung đột dữ liệu duy nhất." });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        private string ExtractValidPhoneNumber(string? rawPhone)
        {
            return PhoneHelper.NormalizePhone(rawPhone);
        }

        private async Task<(AppUser? User, string? CollisionError)> FindParentUserByCanonicalPhoneAsync(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return (null, null);

            var canonicalPhone = PhoneHelper.NormalizePhone(phone);
            if (string.IsNullOrEmpty(canonicalPhone))
            {
                var directUser = await _userManager.FindByNameAsync(phone.Trim());
                return (directUser, null);
            }

            try
            {
                var user = await _userManager.Users.SingleOrDefaultAsync(u => u.CanonicalPhone == canonicalPhone)
                           ?? await _userManager.FindByNameAsync(canonicalPhone);
                return (user, null);
            }
            catch (InvalidOperationException)
            {
                return (null, $"Phát hiện nhiều tài khoản trùng khớp số điện thoại {canonicalPhone}.");
            }
        }

        [HttpGet("mychildren")]
        [Authorize(Roles = AppRoles.User)]
        public async Task<ActionResult<IEnumerable<StudentResponseDto>>> GetMyChildren()
        {
            var userPhone = PhoneHelper.NormalizePhone(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.MobilePhone)?.Value ?? User.Identity?.Name);
            if (string.IsNullOrEmpty(userPhone)) return Unauthorized();

            var students = await _context.Students
                .IgnoreQueryFilters()
                .Include(s => s.Class)
                .Where(s => s.ParentPhoneCanonical == userPhone)
                .ToListAsync();

            var studentIds = students.Select(s => s.Id).ToList();
            var attendances = await _context.Attendances
                .IgnoreQueryFilters()
                .Where(a => studentIds.Contains(a.StudentId))
                .Select(a => new { a.StudentId, a.IsPresent })
                .ToListAsync();

            var result = new List<StudentResponseDto>();
            foreach(var s in students)
            {
                var studentAtts = attendances.Where(a => a.StudentId == s.Id).ToList();
                var attendanceCount = studentAtts.Count;
                var presentCount = studentAtts.Count(a => a.IsPresent);
                int attendanceRate = attendanceCount == 0 ? 100 : (int)Math.Round((double)presentCount / attendanceCount * 100);

                result.Add(new StudentResponseDto
                {
                    Id = s.Id,
                    FullName = s.FullName,
                    DateOfBirth = s.DateOfBirth,
                    ParentName = s.ParentName,
                    ParentPhoneNumber = s.ParentPhoneNumber,
                    TotalRemainingSessions = s.TotalRemainingSessions,
                    TotalAttendedSessions = s.TotalAttendedSessions,
                    TotalPaidSessions = s.TotalPaidSessions,
                    ClassId = s.ClassId,
                    AttendanceRate = attendanceRate,
                    ClassName = s.Class?.ClassName ?? "",
                    IsDeleted = s.IsDeleted
                });
            }

            return Ok(result);
        }

        private class ValidatedImportCommand
        {
            public int Index { get; set; }
            public ImportStudentDto Dto { get; set; } = null!;
            public string NormalizedName { get; set; } = string.Empty;
            public string CanonicalPhone { get; set; } = string.Empty;
            public string ParentName { get; set; } = string.Empty;
            public string ClassName { get; set; } = string.Empty;
            public List<DateOnly> AttendedDays { get; set; } = new();
        }

        [HttpPost("import")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ImportStudents([FromBody] List<ImportStudentDto> dtos)
        {
            if (dtos == null || !dtos.Any())
                return BadRequest("Không có dữ liệu.");

            var commands = new List<ValidatedImportCommand>();
            var seenStudentIdentities = new HashSet<(string NormName, string CanonPhone)>();

            for (int i = 0; i < dtos.Count; i++)
            {
                var dto = dtos[i];
                var rawName = (dto.FullName ?? "").Trim();
                if (string.IsNullOrEmpty(rawName))
                {
                    return BadRequest(new { message = $"Dòng {i + 1}: Tên học sinh không được để trống." });
                }
                var normName = rawName.ToLowerInvariant();

                var rawPhone = dto.ParentPhoneNumber ?? "";
                string canonPhone = string.Empty;
                if (!string.IsNullOrWhiteSpace(rawPhone))
                {
                    if (!PhoneHelper.TryNormalizePhone(rawPhone, out canonPhone))
                    {
                        return BadRequest(new { message = $"Dòng {i + 1}: Số điện thoại phụ huynh '{rawPhone}' không đúng định dạng." });
                    }
                }

                var identityKey = (normName, canonPhone);
                if (!seenStudentIdentities.Add(identityKey))
                {
                    return Conflict(new { message = $"Dòng {i + 1}: Học sinh '{dto.FullName}' (SĐT: {dto.ParentPhoneNumber}) bị trùng lặp với một dòng khác trong danh sách import." });
                }

                var parentName = dto.ParentName ?? "";
                if (rawPhone != canonPhone && Regex.IsMatch(rawPhone, @"[a-zA-Z]"))
                {
                    parentName = string.IsNullOrWhiteSpace(parentName) ? rawPhone : $"{parentName} ({rawPhone})";
                }

                var className = (dto.ClassName ?? "").Trim();
                if (string.IsNullOrEmpty(className)) className = "Chưa rõ";

                var seenDays = new HashSet<DateOnly>();
                var attendedDays = new List<DateOnly>();
                if (dto.AttendedDates != null)
                {
                    foreach (var dt in dto.AttendedDates)
                    {
                        var day = DateOnly.FromDateTime(dt);
                        if (!seenDays.Add(day))
                        {
                            return Conflict(new { message = $"Dòng {i + 1}: Học sinh '{dto.FullName}' có ngày điểm danh bị trùng lặp: {day:yyyy-MM-dd}." });
                        }
                        attendedDays.Add(day);
                    }
                }

                commands.Add(new ValidatedImportCommand
                {
                    Index = i + 1,
                    Dto = dto,
                    NormalizedName = normName,
                    CanonicalPhone = canonPhone,
                    ParentName = parentName,
                    ClassName = className,
                    AttendedDays = attendedDays
                });
            }

            // Bulk preflight
            var distinctClassNames = commands.Select(c => c.ClassName).Distinct().ToList();
            var existingClasses = await _context.Classes
                .Where(c => distinctClassNames.Contains(c.ClassName))
                .ToListAsync();
            var classMap = existingClasses
                .GroupBy(c => c.ClassName, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var candidateNames = commands.Select(c => c.NormalizedName).Distinct().ToList();
            var candidatePhones = commands.Select(c => c.CanonicalPhone).Distinct().ToList();

            var existingStudents = await _context.Students
                .Where(s => !s.IsDeleted && candidateNames.Contains(s.NormalizedFullName) && candidatePhones.Contains(s.ParentPhoneCanonical))
                .ToListAsync();

            var studentMap = existingStudents
                .GroupBy(s => (s.NormalizedFullName, s.ParentPhoneCanonical))
                .ToDictionary(g => g.Key, g => g.First());

            var existingStudentIds = existingStudents.Select(s => s.Id).ToList();
            var distinctDays = commands.SelectMany(c => c.AttendedDays).Distinct().ToList();
            var existingAttendances = existingStudentIds.Count > 0 && distinctDays.Count > 0
                ? await _context.Attendances
                    .Where(a => existingStudentIds.Contains(a.StudentId) && distinctDays.Contains(a.AttendanceDay))
                    .Select(a => new { a.StudentId, a.AttendanceDay })
                    .ToListAsync()
                : new();

            var existingAttendanceSet = new HashSet<(int StudentId, DateOnly Day)>(
                existingAttendances.Select(a => (a.StudentId, a.AttendanceDay))
            );

            var distinctParentPhones = commands.Select(c => c.CanonicalPhone).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
            var parentUsers = distinctParentPhones.Count > 0
                ? await _context.Users.Where(u => u.CanonicalPhone != null && distinctParentPhones.Contains(u.CanonicalPhone)).ToListAsync()
                : new List<AppUser>();
            var parentUserDict = parentUsers
                .GroupBy(u => u.CanonicalPhone!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var missingParentPhones = distinctParentPhones.Except(parentUserDict.Keys).ToList();
            if (missingParentPhones.Count > 0)
            {
                var directUsers = await _context.Users.Where(u => missingParentPhones.Contains(u.UserName!)).ToListAsync();
                foreach (var du in directUsers)
                {
                    var p = PhoneHelper.NormalizePhone(du.UserName);
                    if (!string.IsNullOrEmpty(p) && !parentUserDict.ContainsKey(p))
                    {
                        parentUserDict[p] = du;
                    }
                }
            }

            var parentUserIds = parentUserDict.Values.Select(u => u.Id).Distinct().ToList();
            var userRoleEntries = parentUserIds.Count > 0
                ? await (from ur in _context.UserRoles
                         join r in _context.Roles on ur.RoleId equals r.Id
                         where parentUserIds.Contains(ur.UserId)
                         select new { ur.UserId, RoleName = r.Name }).ToListAsync()
                : new();

            var userRolesMap = userRoleEntries
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(x => x.RoleName!)));

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var name in distinctClassNames)
                {
                    if (!classMap.TryGetValue(name, out var cls))
                    {
                        var uniqueCode = $"CL{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(1000, 9999)}";
                        cls = new Class
                        {
                            ClassName = name,
                            ClassCode = uniqueCode,
                            TotalSessions = 100,
                            TimeSlots = "Chưa thiết lập"
                        };
                        _context.Classes.Add(cls);
                        classMap[name] = cls;
                    }
                }

                foreach (var cmd in commands)
                {
                    var cls = classMap[cmd.ClassName];
                    var identityKey = (cmd.NormalizedName, cmd.CanonicalPhone);

                    Student student;
                    if (!studentMap.TryGetValue(identityKey, out student!))
                    {
                        var paidSessions = cmd.Dto.TotalPaidSessions;
                        if (paidSessions == 0 && cmd.Dto.TotalRemainingSessions > 0 && cmd.Dto.TotalAttendedSessions == 0)
                        {
                            paidSessions = cmd.Dto.TotalRemainingSessions;
                        }

                        student = new Student
                        {
                            FullName = cmd.Dto.FullName ?? string.Empty,
                            ParentName = cmd.ParentName,
                            ParentPhoneNumber = !string.IsNullOrEmpty(cmd.CanonicalPhone) ? cmd.CanonicalPhone : (cmd.Dto.ParentPhoneNumber ?? ""),
                            DateOfBirth = cmd.Dto.DateOfBirth,
                            TotalAttendedSessions = cmd.Dto.TotalAttendedSessions,
                            TotalPaidSessions = paidSessions,
                            TotalRemainingSessions = paidSessions - cmd.Dto.TotalAttendedSessions,
                            Class = cls
                        };
                        _context.Students.Add(student);
                        studentMap[identityKey] = student;
                    }
                    else
                    {
                        student.TotalAttendedSessions = cmd.Dto.TotalAttendedSessions;
                        student.TotalPaidSessions = cmd.Dto.TotalPaidSessions;
                        if (student.TotalPaidSessions == 0 && cmd.Dto.TotalRemainingSessions > 0 && cmd.Dto.TotalAttendedSessions == 0)
                        {
                            student.TotalPaidSessions = cmd.Dto.TotalRemainingSessions;
                        }
                        student.TotalRemainingSessions = student.TotalPaidSessions - student.TotalAttendedSessions;
                        student.Class = cls;

                        if (!string.IsNullOrWhiteSpace(cmd.CanonicalPhone))
                        {
                            student.ParentPhoneNumber = cmd.CanonicalPhone;
                        }
                        if (!string.IsNullOrWhiteSpace(cmd.ParentName) && string.IsNullOrWhiteSpace(student.ParentName))
                        {
                            student.ParentName = cmd.ParentName;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(cmd.CanonicalPhone))
                    {
                        if (!parentUserDict.TryGetValue(cmd.CanonicalPhone, out var parentUser))
                        {
                            var newUser = new AppUser
                            {
                                UserName = cmd.CanonicalPhone,
                                Email = cmd.CanonicalPhone + "@example.com",
                                PhoneNumber = cmd.CanonicalPhone,
                                FullName = !string.IsNullOrWhiteSpace(cmd.ParentName) ? cmd.ParentName : "Phụ huynh " + cmd.Dto.FullName
                            };
                            var createRes = await _userManager.CreateAsync(newUser, cmd.CanonicalPhone + "@123Aa");
                            if (!createRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(createRes.Errors);
                            }
                            var roleRes = await _userManager.AddToRoleAsync(newUser, AppRoles.User);
                            if (!roleRes.Succeeded)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(roleRes.Errors);
                            }
                            parentUserDict[cmd.CanonicalPhone] = newUser;
                            userRolesMap[newUser.Id] = new HashSet<string> { AppRoles.User };
                        }
                        else
                        {
                            var roles = userRolesMap.TryGetValue(parentUser.Id, out var rSet) ? rSet : new HashSet<string>();
                            if (!roles.Contains(AppRoles.Admin))
                            {
                                if (PhoneHelper.NormalizePhone(parentUser.UserName) == cmd.CanonicalPhone && parentUser.UserName != cmd.CanonicalPhone)
                                {
                                    var uNameRes = await _userManager.SetUserNameAsync(parentUser, cmd.CanonicalPhone);
                                    if (!uNameRes.Succeeded)
                                    {
                                        await transaction.RollbackAsync();
                                        _context.ChangeTracker.Clear();
                                        return BadRequest(uNameRes.Errors);
                                    }
                                    var emailRes = await _userManager.SetEmailAsync(parentUser, cmd.CanonicalPhone + "@example.com");
                                    if (!emailRes.Succeeded)
                                    {
                                        await transaction.RollbackAsync();
                                        _context.ChangeTracker.Clear();
                                        return BadRequest(emailRes.Errors);
                                    }
                                }

                                if (parentUser.PhoneNumber != cmd.CanonicalPhone)
                                {
                                    parentUser.PhoneNumber = cmd.CanonicalPhone;
                                    var updateRes = await _userManager.UpdateAsync(parentUser);
                                    if (!updateRes.Succeeded)
                                    {
                                        await transaction.RollbackAsync();
                                        _context.ChangeTracker.Clear();
                                        return BadRequest(updateRes.Errors);
                                    }
                                }

                                if (!roles.Contains(AppRoles.User))
                                {
                                    var roleRes = await _userManager.AddToRoleAsync(parentUser, AppRoles.User);
                                    if (!roleRes.Succeeded)
                                    {
                                        await transaction.RollbackAsync();
                                        _context.ChangeTracker.Clear();
                                        return BadRequest(roleRes.Errors);
                                    }
                                    roles.Add(AppRoles.User);

                                    var stampRes = await _userManager.UpdateSecurityStampAsync(parentUser);
                                    if (!stampRes.Succeeded)
                                    {
                                        await transaction.RollbackAsync();
                                        _context.ChangeTracker.Clear();
                                        return BadRequest(stampRes.Errors);
                                    }
                                }

                                if (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(parentUser))
                                {
                                    var lockRes = await _userManager.SetLockoutEndDateAsync(parentUser, null);
                                    if (!lockRes.Succeeded)
                                    {
                                        await transaction.RollbackAsync();
                                        _context.ChangeTracker.Clear();
                                        return BadRequest(lockRes.Errors);
                                    }
                                    var resetCountRes = await _userManager.ResetAccessFailedCountAsync(parentUser);
                                    if (!resetCountRes.Succeeded)
                                    {
                                        await transaction.RollbackAsync();
                                        _context.ChangeTracker.Clear();
                                        return BadRequest(resetCountRes.Errors);
                                    }
                                }
                            }
                        }
                    }

                    foreach (var day in cmd.AttendedDays)
                    {
                        var dateUtc = DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
                        if (student.Id == 0 || !existingAttendanceSet.Contains((student.Id, day)))
                        {
                            _context.Attendances.Add(new Attendance
                            {
                                Student = student,
                                Class = cls,
                                Date = dateUtc,
                                AttendanceDay = day,
                                IsPresent = true
                            });
                            if (student.Id > 0)
                            {
                                existingAttendanceSet.Add((student.Id, day));
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { message = "Import thành công" });
            }
            catch (Exception ex) when (DbErrorHelper.IsUniqueViolation(ex))
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return Conflict(new { message = "Dữ liệu import bị trùng lặp khóa duy nhất trong cơ sở dữ liệu." });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Đã xảy ra lỗi khi import học sinh." });
            }
        }

        private async Task UpdateRemainingSessionsAsync(Student student)
        {
            student.TotalRemainingSessions = student.TotalPaidSessions - student.TotalAttendedSessions;
            var canonPhone = student.ParentPhoneCanonical ?? PhoneHelper.NormalizePhone(student.ParentPhoneNumber);
            if (student.TotalRemainingSessions < 3 && !string.IsNullOrWhiteSpace(canonPhone))
            {
                var (parentUser, _) = await FindParentUserByCanonicalPhoneAsync(canonPhone);
                if (parentUser != null)
                {
                    var existingNotif = await _context.Notifications
                        .FirstOrDefaultAsync(n => n.UserId == parentUser.Id && !n.IsRead && n.Message.Contains(student.FullName));
                    
                    if (existingNotif == null)
                    {
                        string message = student.TotalRemainingSessions < 0
                            ? $"Học sinh {student.FullName} đã học lố {-student.TotalRemainingSessions} buổi. Quý phụ huynh vui lòng hoàn thành đóng học phí."
                            : $"Học sinh {student.FullName} chỉ còn {student.TotalRemainingSessions} buổi học. Quý phụ huynh vui lòng đóng thêm học phí để không gián đoạn việc học.";

                        _context.Notifications.Add(new Notification
                        {
                            UserId = parentUser.Id,
                            Message = message,
                            IsRead = false,
                            HidePopup = false,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
        }
    }
}
