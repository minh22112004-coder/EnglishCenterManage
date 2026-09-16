using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using ManagementSystem.Api.Data;
using ManagementSystem.Api.Models;
using ManagementSystem.Api.Constants;
using ManagementSystem.Api.Common;

namespace ManagementSystem.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoles.Admin)]
    public class UserController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;

        public UserController(UserManager<AppUser> userManager, AppDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpGet("lectures")]
        public async Task<IActionResult> GetLectures()
        {
            var users = await _userManager.GetUsersInRoleAsync(AppRoles.Lecture);

            // Lấy danh sách lịch dạy hôm nay
            var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")).Date;
            var todayUtc = DateTime.SpecifyKind(todayLocal, DateTimeKind.Utc);

            var result = new List<LectureResponseDto>();

            foreach (var u in users)
            {
                var todaySchedules = await _context.WorkSchedules
                    .Include(w => w.Classes)
                    .Where(w => w.UserId == u.Id && w.Date.Date == todayUtc)
                    .Select(w => new {
                        ScheduleId = w.Id,
                        Classes = w.Classes.Select(c => new { c.ClassName, c.TimeSlots })
                    })
                    .ToListAsync();

                result.Add(new LectureResponseDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    BankNumber = u.BankNumber,
                    BankName = u.BankName,
                    QrCodeImage = u.QrCodeImage,
                    TodaySchedules = todaySchedules
                });
            }

            return Ok(result);
        }

        [HttpPost("lectures")]
        public async Task<IActionResult> CreateLecture([FromBody] LectureDto dto)
        {
            if (!PhoneHelper.TryNormalizePhone(dto.PhoneNumber, out var newPhone) || string.IsNullOrEmpty(newPhone))
                return BadRequest(new { message = "Số điện thoại không hợp lệ hoặc không đúng định dạng." });

            var existingUser = await _userManager.Users.FirstOrDefaultAsync(u => u.CanonicalPhone == newPhone)
                               ?? await _userManager.FindByNameAsync(newPhone);
            if (existingUser != null)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Người dùng đã tồn tại (VD: đã là User/Phụ huynh), gán thêm quyền Lecture và cập nhật thông tin
                    if (!await _userManager.IsInRoleAsync(existingUser, AppRoles.Lecture))
                    {
                        var addRes = await _userManager.AddToRoleAsync(existingUser, AppRoles.Lecture);
                        if (!addRes.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            _context.ChangeTracker.Clear();
                            return BadRequest(addRes.Errors);
                        }
                    }

                    existingUser.FullName = dto.FullName ?? existingUser.FullName;
                    existingUser.PhoneNumber = newPhone;
                    existingUser.BankNumber = dto.BankNumber ?? existingUser.BankNumber;
                    existingUser.BankName = dto.BankName ?? existingUser.BankName;
                    if (!string.IsNullOrEmpty(dto.QrCodeImage)) existingUser.QrCodeImage = dto.QrCodeImage;

                    // Mở khóa tài khoản nếu đang bị Lockout (trường hợp phụ huynh cũ bị khóa)
                    if (_userManager.SupportsUserLockout)
                    {
                        var unlockRes = await _userManager.SetLockoutEndDateAsync(existingUser, null);
                        if (!unlockRes.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            _context.ChangeTracker.Clear();
                            return BadRequest(unlockRes.Errors);
                        }
                        var resetFailRes = await _userManager.ResetAccessFailedCountAsync(existingUser);
                        if (!resetFailRes.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            _context.ChangeTracker.Clear();
                            return BadRequest(resetFailRes.Errors);
                        }
                    }

                    var stampRes = await _userManager.UpdateSecurityStampAsync(existingUser);
                    if (!stampRes.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return BadRequest(stampRes.Errors);
                    }

                    var updateRes = await _userManager.UpdateAsync(existingUser);
                    if (!updateRes.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return BadRequest(updateRes.Errors);
                    }

                    await transaction.CommitAsync();
                    return Ok(new { message = "Số điện thoại đã tồn tại trong hệ thống. Đã tích hợp thêm quyền Giáo viên và kích hoạt tài khoản.", id = existingUser.Id });
                }
                catch (Exception ex) when (DbErrorHelper.IsUniqueViolation(ex))
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return Conflict(new { message = "Số điện thoại hoặc thông tin bị trùng lặp trong hệ thống." });
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi cập nhật giáo viên." });
                }
            }

            using var transactionNew = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = new AppUser
                {
                    UserName = newPhone,
                    Email = newPhone + "@example.com",
                    PhoneNumber = newPhone,
                    FullName = dto.FullName,
                    BankNumber = dto.BankNumber,
                    BankName = dto.BankName,
                    QrCodeImage = dto.QrCodeImage
                };

                var createRes = await _userManager.CreateAsync(user, newPhone + "@123Aa");
                if (!createRes.Succeeded)
                {
                    await transactionNew.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return BadRequest(createRes.Errors);
                }

                var addRoleRes = await _userManager.AddToRoleAsync(user, AppRoles.Lecture);
                if (!addRoleRes.Succeeded)
                {
                    await transactionNew.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return BadRequest(addRoleRes.Errors);
                }

                await transactionNew.CommitAsync();
                return Ok(new { message = "Tạo giáo viên thành công", id = user.Id });
            }
            catch (Exception ex) when (DbErrorHelper.IsUniqueViolation(ex))
            {
                await transactionNew.RollbackAsync();
                _context.ChangeTracker.Clear();
                return Conflict(new { message = "Số điện thoại hoặc thông tin bị trùng lặp trong hệ thống." });
            }
            catch (Exception)
            {
                await transactionNew.RollbackAsync();
                _context.ChangeTracker.Clear();
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi tạo giáo viên." });
            }
        }

        [HttpPut("lectures/{id}")]
        public async Task<IActionResult> UpdateLecture(string id, [FromBody] LectureDto dto)
        {
            if (!PhoneHelper.TryNormalizePhone(dto.PhoneNumber, out var newPhone) || string.IsNullOrEmpty(newPhone))
                return BadRequest(new { message = "Số điện thoại không hợp lệ hoặc không đúng định dạng." });

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var user = await _context.Users.FromSqlRaw("SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {0} FOR UPDATE", id).SingleOrDefaultAsync();
                if (user == null)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return NotFound();
                }

                var roleNames = await (from ur in _context.UserRoles
                                       join r in _context.Roles on ur.RoleId equals r.Id
                                       where ur.UserId == user.Id
                                       select r.Name).ToListAsync();

                if (!roleNames.Contains(AppRoles.Lecture))
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return NotFound(new { message = "Không tìm thấy tài khoản giáo viên." });
                }

                bool isDualRole = roleNames.Contains(AppRoles.User);

                var oldPhone = user.CanonicalPhone ?? PhoneHelper.NormalizePhone(user.PhoneNumber);
                if (string.IsNullOrEmpty(oldPhone))
                    oldPhone = PhoneHelper.NormalizePhone(user.UserName);

                bool phoneChanged = (!string.IsNullOrEmpty(oldPhone) && oldPhone != newPhone) || user.UserName != newPhone;

                if (phoneChanged)
                {
                    var conflictExists = await _context.Users
                        .AsNoTracking()
                        .AnyAsync(u => u.Id != user.Id && u.CanonicalPhone == newPhone);

                    if (conflictExists)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return Conflict(new { message = "Số điện thoại mới đã được dùng bởi tài khoản khác." });
                    }

                    if (isDualRole && !string.IsNullOrEmpty(oldPhone))
                    {
                        var oldChildNames = await _context.Students
                            .Where(s => s.ParentPhoneCanonical == oldPhone)
                            .Select(s => s.NormalizedFullName)
                            .ToListAsync();

                        if (oldChildNames.Count > 0)
                        {
                            var collidingChild = await _context.Students
                                .Where(s => s.ParentPhoneCanonical == newPhone && oldChildNames.Contains(s.NormalizedFullName))
                                .Select(s => s.FullName)
                                .FirstOrDefaultAsync();

                            if (collidingChild != null)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return Conflict(new { message = $"Học sinh '{collidingChild}' đã tồn tại dưới số điện thoại mới." });
                            }
                        }

                        await _context.Students
                            .IgnoreQueryFilters()
                            .Where(s => s.ParentPhoneCanonical == oldPhone)
                            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.ParentPhoneNumber, newPhone));
                    }

                    var setUserNameRes = await _userManager.SetUserNameAsync(user, newPhone);
                    if (!setUserNameRes.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return BadRequest(setUserNameRes.Errors);
                    }

                    var setEmailRes = await _userManager.SetEmailAsync(user, newPhone + "@example.com");
                    if (!setEmailRes.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return BadRequest(setEmailRes.Errors);
                    }
                }

                user.PhoneNumber = newPhone;
                user.FullName = dto.FullName;
                user.BankNumber = dto.BankNumber;
                user.BankName = dto.BankName;
                if (!string.IsNullOrEmpty(dto.QrCodeImage)) user.QrCodeImage = dto.QrCodeImage;

                if (phoneChanged)
                {
                    var stampRes = await _userManager.UpdateSecurityStampAsync(user);
                    if (!stampRes.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return BadRequest(stampRes.Errors);
                    }
                }

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return BadRequest(result.Errors);
                }

                await transaction.CommitAsync();
                return Ok(new { message = "Cập nhật thành công" });
            }
            catch (Exception ex) when (DbErrorHelper.IsUniqueViolation(ex))
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return Conflict(new { message = "Số điện thoại hoặc thông tin bị trùng lặp trong hệ thống." });
            }
            catch (Exception ex) when (DbErrorHelper.IsTransientConcurrencyError(ex))
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return StatusCode(StatusCodes.Status409Conflict, new { message = "Yêu cầu bị xung đột đồng thời, vui lòng thử lại." });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi cập nhật giáo viên." });
            }
        }

        [HttpPost("lectures/{id}/reset-password")]
        public async Task<IActionResult> ResetLecturePassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

            // Không cho reset tài khoản Admin
            if (await _userManager.IsInRoleAsync(user, AppRoles.Admin))
                return Forbid();

            var phone = user.CanonicalPhone ?? PhoneHelper.NormalizePhone(user.PhoneNumber);
            if (string.IsNullOrEmpty(phone)) phone = PhoneHelper.NormalizePhone(user.UserName);
            if (string.IsNullOrEmpty(phone)) phone = user.UserName ?? "";

            if (string.IsNullOrEmpty(phone))
                return BadRequest(new { message = "Không thể xác định SĐT hoặc Username để tạo mật khẩu mặc định." });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, phone + "@123Aa");
                if (!resetResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
                    return BadRequest(new { message = $"Reset thất bại: {errors}" });
                }

                if (_userManager.SupportsUserLockout)
                {
                    var unlockRes = await _userManager.SetLockoutEndDateAsync(user, null);
                    if (!unlockRes.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return BadRequest(unlockRes.Errors);
                    }
                    var resetFailRes = await _userManager.ResetAccessFailedCountAsync(user);
                    if (!resetFailRes.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return BadRequest(resetFailRes.Errors);
                    }
                }

                var stampRes = await _userManager.UpdateSecurityStampAsync(user);
                if (!stampRes.Succeeded)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return BadRequest(stampRes.Errors);
                }

                await transaction.CommitAsync();
                return Ok(new { message = $"Đã reset mật khẩu về mặc định: {phone}@123Aa" });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi reset mật khẩu." });
            }
        }

        [HttpDelete("lectures/{id}")]
        public async Task<IActionResult> DeleteLecture(string id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Khóa dòng AspNetUsers bằng SELECT ... FOR UPDATE để serialize với việc phân công lịch dạy
                var user = await _context.Users
                    .FromSqlRaw("SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {0} FOR UPDATE", id)
                    .SingleOrDefaultAsync();

                if (user == null)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return NotFound();
                }

                if (await _userManager.IsInRoleAsync(user, AppRoles.Admin))
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return BadRequest(new { message = "Không thể xóa tài khoản Quản trị viên." });
                }

                if (!await _userManager.IsInRoleAsync(user, AppRoles.Lecture))
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return NotFound(new { message = "Không tìm thấy tài khoản giáo viên." });
                }

                var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")).Date;
                var todayUtc = DateTime.SpecifyKind(todayLocal, DateTimeKind.Utc);
                var upcoming = await _context.WorkSchedules
                    .Where(w => w.UserId == user.Id && w.Date.Date >= todayUtc)
                    .CountAsync();

                if (upcoming > 0)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return Conflict(new { message = $"Không thể xóa/gỡ giáo viên vì vẫn còn {upcoming} ca dạy chưa hoàn thành từ hôm nay trở đi." });
                }

                var roles = await _userManager.GetRolesAsync(user);

                // Step 5: Thay thế hard-delete đối với giáo viên kiêm nhiệm bằng việc chỉ gỡ role Lecture
                if (roles.Contains(AppRoles.User))
                {
                    var remResult = await _userManager.RemoveFromRoleAsync(user, AppRoles.Lecture);
                    if (!remResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return BadRequest(remResult.Errors);
                    }

                    var stampResult = await _userManager.UpdateSecurityStampAsync(user);
                    if (!stampResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return BadRequest(stampResult.Errors);
                    }

                    await transaction.CommitAsync();
                    return Ok(new { message = "Đã gỡ vai trò Giáo viên. Tài khoản Phụ huynh vẫn được duy trì." });
                }

                // Nếu người dùng không còn vai trò nào khác: kiểm tra dữ liệu phụ thuộc
                var hasSchedules = await _context.WorkSchedules.AnyAsync(w => w.UserId == user.Id);
                var hasAuditLogs = await _context.AuditLogs.AnyAsync(a => a.UserId == user.Id);

                if (hasSchedules || hasAuditLogs)
                {
                    var remResult = await _userManager.RemoveFromRoleAsync(user, AppRoles.Lecture);
                    if (!remResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return BadRequest(remResult.Errors);
                    }

                    if (_userManager.SupportsUserLockout)
                    {
                        var lockoutRes = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                        if (!lockoutRes.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            _context.ChangeTracker.Clear();
                            return BadRequest(lockoutRes.Errors);
                        }
                    }

                    var stampResult = await _userManager.UpdateSecurityStampAsync(user);
                    if (!stampResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return BadRequest(stampResult.Errors);
                    }

                    await transaction.CommitAsync();
                    return Ok(new { message = "Đã gỡ vai trò Giáo viên, khóa tài khoản và lưu trữ lịch sử." });
                }

                var delResult = await _userManager.DeleteAsync(user);
                if (!delResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return BadRequest(delResult.Errors);
                }

                await transaction.CommitAsync();
                return Ok(new { message = "Xóa giáo viên thành công" });
            }
            catch (Exception ex) when (DbErrorHelper.IsTransientConcurrencyError(ex))
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return StatusCode(StatusCodes.Status409Conflict, new { message = "Xung đột đồng thời khi xóa giáo viên, vui lòng thử lại." });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi xóa giáo viên." });
            }
        }

        [HttpGet("parents")]
        public async Task<IActionResult> GetParents()
        {
            var users = await _userManager.GetUsersInRoleAsync(AppRoles.User);
            var nonAdminParents = new List<AppUser>();
            foreach (var u in users)
            {
                if (!await _userManager.IsInRoleAsync(u, AppRoles.Admin))
                {
                    nonAdminParents.Add(u);
                }
            }

            var parentCanonicalPhones = nonAdminParents
                .Select(u => u.CanonicalPhone ?? PhoneHelper.NormalizePhone(u.PhoneNumber ?? u.UserName))
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .ToList();

            var matchedStudentsList = parentCanonicalPhones.Count > 0
                ? await _context.Students
                    .IgnoreQueryFilters()
                    .Include(s => s.Class)
                    .Where(s => !string.IsNullOrEmpty(s.ParentPhoneCanonical) && parentCanonicalPhones.Contains(s.ParentPhoneCanonical))
                    .ToListAsync()
                : new List<Student>();

            var studentsByCanonicalPhone = matchedStudentsList
                .GroupBy(s => s.ParentPhoneCanonical)
                .ToDictionary(g => g.Key!, g => g.ToList(), StringComparer.Ordinal);

            var result = new List<ParentResponseDto>();
            foreach (var u in nonAdminParents)
            {
                var isLocked = _userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(u);
                var userRoles = (await _userManager.GetRolesAsync(u)).ToList();

                var canonPhone = u.CanonicalPhone ?? PhoneHelper.NormalizePhone(u.PhoneNumber ?? u.UserName);
                var children = new List<ParentChildItemDto>();
                if (!string.IsNullOrEmpty(canonPhone) && studentsByCanonicalPhone.TryGetValue(canonPhone, out var matchedStudents))
                {
                    children = matchedStudents.Select(s => new ParentChildItemDto
                    {
                        Id = s.Id,
                        FullName = s.FullName,
                        ClassName = s.Class?.ClassName,
                        TotalRemainingSessions = s.TotalRemainingSessions,
                        IsDeleted = s.IsDeleted
                    }).ToList();
                }

                result.Add(new ParentResponseDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    Email = u.Email,
                    IsLockedOut = isLocked,
                    Roles = userRoles,
                    Children = children
                });
            }

            return Ok(result.OrderBy(p => p.FullName ?? p.PhoneNumber));
        }

        [HttpGet("{id}/role-summary")]
        public async Task<IActionResult> GetUserRoleSummary(string id)
        {
            var user = await _userManager.FindByIdAsync(id)
                       ?? await _userManager.FindByNameAsync(id)
                       ?? await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == id);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

            var roles = (await _userManager.GetRolesAsync(user)).ToList();
            var isLocked = _userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(user);

            int upcomingSchedules = 0;
            if (roles.Contains(AppRoles.Lecture))
            {
                var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")).Date;
                var todayUtc = DateTime.SpecifyKind(todayLocal, DateTimeKind.Utc);

                upcomingSchedules = await _context.WorkSchedules
                    .Where(w => w.UserId == user.Id && w.Date.Date >= todayUtc)
                    .CountAsync();
            }

            var canonPhone = user.CanonicalPhone ?? PhoneHelper.NormalizePhone(user.PhoneNumber ?? user.UserName);
            var children = !string.IsNullOrEmpty(canonPhone)
                ? await _context.Students
                    .Where(s => !s.IsDeleted && s.ParentPhoneCanonical == canonPhone)
                    .Select(s => s.FullName)
                    .ToListAsync()
                : new List<string>();

            return Ok(new UserRoleSummaryDto
            {
                Id = user.Id,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Email = user.Email,
                CurrentRoles = roles,
                UpcomingSchedulesCount = upcomingSchedules,
                ChildrenCount = children.Count,
                ChildrenNames = children,
                IsLockedOut = isLocked
            });
        }

        [HttpPost("{id}/change-role")]
        public async Task<IActionResult> ChangeRole(string id, [FromBody] ChangeRoleDto dto)
        {
            var validRoles = new[] { "Lecture", "User", "Both" };
            if (!validRoles.Contains(dto.TargetRole))
            {
                return BadRequest(new { message = "Vai trò đích không hợp lệ. Chỉ chấp nhận: Lecture, User, hoặc Both." });
            }

            var initialUser = await _userManager.FindByIdAsync(id)
                       ?? await _userManager.FindByNameAsync(id)
                       ?? await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == id);
            if (initialUser == null) return NotFound(new { message = "Không tìm thấy người dùng." });

            if (await _userManager.IsInRoleAsync(initialUser, AppRoles.Admin))
            {
                return BadRequest(new { message = "Không thể thay đổi vai trò của tài khoản Quản trị viên (Admin)." });
            }

            var desiredRoles = dto.TargetRole switch
            {
                "Lecture" => new List<string> { AppRoles.Lecture },
                "User" => new List<string> { AppRoles.User },
                "Both" => new List<string> { AppRoles.Lecture, AppRoles.User },
                _ => new List<string>()
            };

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Khóa dòng AspNetUsers của user bằng SELECT ... FOR UPDATE để serialize với việc phân công lịch dạy
                var user = await _context.Users
                    .FromSqlRaw("SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {0} FOR UPDATE", initialUser.Id)
                    .SingleOrDefaultAsync();

                if (user == null)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return NotFound(new { message = "Không tìm thấy người dùng." });
                }

                var currentRoles = await _userManager.GetRolesAsync(user);

                // Step 4: Khi chuyển từ Lecture sang User, từ chối với 409 Conflict nếu vẫn còn lịch dạy tương lai
                bool isDemotingLecture = currentRoles.Contains(AppRoles.Lecture) && !desiredRoles.Contains(AppRoles.Lecture);
                if (isDemotingLecture)
                {
                    var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")).Date;
                    var todayUtc = DateTime.SpecifyKind(todayLocal, DateTimeKind.Utc);
                    var upcoming = await _context.WorkSchedules
                        .Where(w => w.UserId == user.Id && w.Date.Date >= todayUtc)
                        .CountAsync();

                    if (upcoming > 0)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return Conflict(new { message = $"Không thể chuyển sang vai trò Phụ huynh vì người này vẫn còn {upcoming} ca dạy chưa hoàn thành từ hôm nay trở đi." });
                    }
                }

                // Step 5: Check usable phone/username before state changes if password reset requested
                var resetPhone = user.CanonicalPhone ?? PhoneHelper.NormalizePhone(user.PhoneNumber);
                if (string.IsNullOrEmpty(resetPhone)) resetPhone = PhoneHelper.NormalizePhone(user.UserName);
                if (string.IsNullOrEmpty(resetPhone)) resetPhone = user.UserName ?? "";

                if (dto.ResetPasswordToDefault && string.IsNullOrWhiteSpace(resetPhone))
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return BadRequest(new { message = "Không thể đặt lại mật khẩu mặc định vì người dùng không có số điện thoại hoặc tên đăng nhập hợp lệ." });
                }

                var toAdd = desiredRoles.Except(currentRoles).ToList();
                var toRemove = currentRoles.Intersect(new[] { AppRoles.Lecture, AppRoles.User }).Except(desiredRoles).ToList();

                if (toAdd.Any())
                {
                    var addRes = await _userManager.AddToRolesAsync(user, toAdd);
                    if (!addRes.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return BadRequest(new { message = "Lỗi khi gán vai trò mới.", errors = addRes.Errors });
                    }
                }

                if (toRemove.Any())
                {
                    var remRes = await _userManager.RemoveFromRolesAsync(user, toRemove);
                    if (!remRes.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return BadRequest(new { message = "Lỗi khi gỡ vai trò cũ.", errors = remRes.Errors });
                    }
                }

                if (desiredRoles.Contains(AppRoles.Lecture))
                {
                    if (!string.IsNullOrWhiteSpace(dto.BankName)) user.BankName = dto.BankName;
                    if (!string.IsNullOrWhiteSpace(dto.BankNumber)) user.BankNumber = dto.BankNumber;
                    if (!string.IsNullOrWhiteSpace(dto.QrCodeImage)) user.QrCodeImage = dto.QrCodeImage;

                    if (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(user))
                    {
                        var unlockRes = await _userManager.SetLockoutEndDateAsync(user, null);
                        if (!unlockRes.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            _context.ChangeTracker.Clear();
                            return BadRequest(new { message = "Lỗi khi mở khóa tài khoản.", errors = unlockRes.Errors });
                        }
                        var resetFailRes = await _userManager.ResetAccessFailedCountAsync(user);
                        if (!resetFailRes.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            _context.ChangeTracker.Clear();
                            return BadRequest(new { message = "Lỗi khi reset số lần đăng nhập sai.", errors = resetFailRes.Errors });
                        }
                    }
                }

                if (dto.ResetPasswordToDefault)
                {
                    var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var resetRes = await _userManager.ResetPasswordAsync(user, resetToken, resetPhone + "@123Aa");
                    if (!resetRes.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return BadRequest(new { message = "Lỗi khi đặt lại mật khẩu mặc định.", errors = resetRes.Errors });
                    }
                    if (_userManager.SupportsUserLockout)
                    {
                        var unlockRes = await _userManager.SetLockoutEndDateAsync(user, null);
                        if (!unlockRes.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            _context.ChangeTracker.Clear();
                            return BadRequest(new { message = "Lỗi khi mở khóa tài khoản sau reset mật khẩu.", errors = unlockRes.Errors });
                        }
                        var resetFailRes = await _userManager.ResetAccessFailedCountAsync(user);
                        if (!resetFailRes.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            _context.ChangeTracker.Clear();
                            return BadRequest(new { message = "Lỗi khi reset số lần đăng nhập sai sau reset mật khẩu.", errors = resetFailRes.Errors });
                        }
                    }
                }

                // Cập nhật security stamp để thu hồi token cũ ngay lập tức (Step 2)
                var stampRes = await _userManager.UpdateSecurityStampAsync(user);
                if (!stampRes.Succeeded)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return BadRequest(new { message = "Lỗi khi cập nhật security stamp.", errors = stampRes.Errors });
                }

                var updateRes = await _userManager.UpdateAsync(user);
                if (!updateRes.Succeeded)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return BadRequest(new { message = "Lỗi khi lưu thông tin người dùng.", errors = updateRes.Errors });
                }

                await transaction.CommitAsync();

                var updatedRoles = await _userManager.GetRolesAsync(user);
                string roleDescription = dto.TargetRole switch
                {
                    "Lecture" => "Giáo viên",
                    "User" => "Phụ huynh",
                    "Both" => "Kiêm nhiệm (Giáo viên & Phụ huynh)",
                    _ => dto.TargetRole
                };

                return Ok(new
                {
                    message = $"Đã cập nhật vai trò của người dùng thành: {roleDescription}.",
                    roles = updatedRoles,
                    userId = user.Id
                });
            }
            catch (Exception ex) when (DbErrorHelper.IsUniqueViolation(ex))
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return Conflict(new { message = "Thông tin vai trò hoặc người dùng bị xung đột trong hệ thống." });
            }
            catch (Exception ex) when (DbErrorHelper.IsTransientConcurrencyError(ex))
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return StatusCode(StatusCodes.Status409Conflict, new { message = "Xung đột đồng thời khi chuyển đổi vai trò, vui lòng thử lại." });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi xử lý chuyển đổi vai trò." });
            }
        }
    }
}
