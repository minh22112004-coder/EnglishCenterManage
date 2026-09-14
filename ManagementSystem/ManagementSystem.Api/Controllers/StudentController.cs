using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagementSystem.Api.Data;
using ManagementSystem.Api.Models;
using System.Text.RegularExpressions;
using System.Security.Claims;

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

            if (User.IsInRole("Admin"))
            {
                query = _context.Students.Include(s => s.Class).AsQueryable();
            }
            else
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                var myClassIds = await _context.WorkSchedules
                    .Where(w => w.UserId == userId)
                    .SelectMany(w => w.Classes.Select(c => c.Id))
                    .Distinct()
                    .ToListAsync();
                    
                query = _context.Students.Include(s => s.Class)
                    .Where(s => s.ClassId.HasValue && myClassIds.Contains(s.ClassId.Value));
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
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<StudentResponseDto>> PostStudent(StudentDto dto)
        {
            var rawPhone = dto.ParentPhoneNumber ?? "";
            var validPhone = ExtractValidPhoneNumber(rawPhone);
            var parentName = dto.ParentName ?? "";
            if (rawPhone != validPhone && Regex.IsMatch(rawPhone, @"[a-zA-Z]"))
            {
                parentName = string.IsNullOrWhiteSpace(parentName) ? rawPhone : $"{parentName} ({rawPhone})";
            }

            var validClassId = (dto.ClassId.HasValue && dto.ClassId.Value > 0) ? dto.ClassId : null;

            var student = new Student
            {
                FullName = dto.FullName,
                DateOfBirth = dto.DateOfBirth,
                ParentName = parentName,
                ParentPhoneNumber = validPhone,
                TotalAttendedSessions = dto.TotalAttendedSessions,
                TotalPaidSessions = dto.TotalPaidSessions,
                ClassId = validClassId
            };

            await UpdateRemainingSessionsAsync(student);

            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            // Tự động tạo tài khoản User cho phụ huynh nếu có SĐT và chưa có tài khoản
            if (!string.IsNullOrWhiteSpace(validPhone))
            {
                var parentUser = await _userManager.FindByNameAsync(validPhone);
                if (parentUser == null)
                {
                    var newUser = new AppUser
                    {
                        UserName = validPhone,
                        Email = validPhone + "@example.com", // Dummy email
                        FullName = parentName // Sử dụng tên phụ huynh thay vì chuỗi mặc định
                    };
                    var result = await _userManager.CreateAsync(newUser, validPhone + "@123Aa");
                    if (result.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(newUser, "User");
                    }
                }
            }

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

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PutStudent(int id, StudentDto dto)
        {
            var existing = await _context.Students.FindAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            var rawPhone = dto.ParentPhoneNumber ?? "";
            var validPhone = ExtractValidPhoneNumber(rawPhone);
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

            if (!string.IsNullOrWhiteSpace(validPhone))
            {
                var parentUser = await _userManager.FindByNameAsync(validPhone);
                if (parentUser == null)
                {
                    var newUser = new AppUser
                    {
                        UserName = validPhone,
                        Email = validPhone + "@example.com",
                        FullName = parentName ?? "Phụ huynh " + existing.FullName
                    };
                    var result = await _userManager.CreateAsync(newUser, validPhone + "@123Aa");
                    if (result.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(newUser, "User");
                    }
                }
            }

            // TODO: Bắt sự kiện AuditLog nếu là Lecture

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var existing = await _context.Students.FindAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            _context.Students.Remove(existing);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("bulk")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BulkDeleteStudents([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any())
                return BadRequest("Không có học sinh nào được chọn.");

            var studentsToDelete = await _context.Students.Where(s => ids.Contains(s.Id)).ToListAsync();
            
            if (!studentsToDelete.Any())
                return NotFound("Không tìm thấy học sinh nào.");

            _context.Students.RemoveRange(studentsToDelete);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("deleted")]
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RecoverStudents([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any())
                return BadRequest("Không có học sinh nào được chọn.");

            var studentsToRecover = await _context.Students
                .IgnoreQueryFilters()
                .Where(s => ids.Contains(s.Id) && s.IsDeleted)
                .ToListAsync();
            
            if (!studentsToRecover.Any())
                return NotFound("Không tìm thấy học sinh nào.");

            // Lấy danh sách ID các lớp đang active (SC-09)
            var activeClassIds = await _context.Classes.Select(c => c.Id).ToListAsync();

            foreach (var student in studentsToRecover)
            {
                student.IsDeleted = false;
                student.DeletedAt = null;

                // Nếu lớp cũ đã bị xóa mềm, gỡ ClassId để tránh reference hỏng (SC-09)
                if (student.ClassId.HasValue && !activeClassIds.Contains(student.ClassId.Value))
                {
                    student.ClassId = null;
                }

                // Thay đổi State thành Modified để EF cập nhật thay vì xóa
                _context.Entry(student).State = EntityState.Modified;
            }
            
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("sync-accounts")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SyncAccounts()
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
                var validPhone = ExtractValidPhoneNumber(rawPhone);

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

                var parentUser = await _userManager.FindByNameAsync(validPhone);
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
                    if (result.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(newUser, "User");
                        createdCount++;
                    }
                }
                else if (await _userManager.IsLockedOutAsync(parentUser))
                {
                    // [Self-healing/Repair]: Tự động mở khóa tài khoản bị kẹt lockout
                    // nhưng hiện vẫn còn ít nhất 1 học sinh active trong hệ thống.
                    var roles = await _userManager.GetRolesAsync(parentUser);
                    if (!roles.Contains("Admin") && !roles.Contains("Lecture"))
                    {
                        await _userManager.SetLockoutEndDateAsync(parentUser, null);
                        await _userManager.ResetAccessFailedCountAsync(parentUser);
                        unlockedCount++;
                    }
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Đã đồng bộ thành công: tạo mới {createdCount} và mở khóa {unlockedCount} tài khoản phụ huynh.",
                createdCount,
                unlockedCount
            });
        }

        private string ExtractValidPhoneNumber(string? rawPhone)
        {
            if (string.IsNullOrWhiteSpace(rawPhone)) return "";
            var cleaned = Regex.Replace(rawPhone, @"[\s\.\-\(\)]+", "");
            var match = Regex.Match(cleaned, @"(0|84)\d{9}");
            if (match.Success)
            {
                var phoneStr = match.Value;
                if (phoneStr.StartsWith("84") && phoneStr.Length == 11) {
                    return "0" + phoneStr.Substring(2);
                }
                return phoneStr;
            }
            return "";
        }

        [HttpGet("mychildren")]
        [Authorize(Roles = "User")]
        public async Task<ActionResult<IEnumerable<StudentResponseDto>>> GetMyChildren()
        {
            var userPhone = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.MobilePhone)?.Value ?? User.Identity?.Name;
            if (string.IsNullOrEmpty(userPhone)) return Unauthorized();

            var students = await _context.Students
                .IgnoreQueryFilters()
                .Include(s => s.Class)
                .Where(s => s.ParentPhoneNumber == userPhone)
                .ToListAsync();

            var studentIds = students.Select(s => s.Id).ToList();
            var attendances = await _context.Attendances
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

        [HttpPost("import")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ImportStudents([FromBody] List<ImportStudentDto> dtos)
        {
            try 
            {
                if (dtos == null || !dtos.Any())
                    return BadRequest("Không có dữ liệu.");

                // SC-06: Cache danh sách Class để tránh query lặp và chống race condition tạo duplicate class
                var classCache = new Dictionary<string, Class>(StringComparer.OrdinalIgnoreCase);
                var existingClasses = await _context.Classes.ToListAsync();
                foreach (var c in existingClasses)
                {
                    if (!classCache.ContainsKey(c.ClassName))
                    {
                        classCache[c.ClassName] = c;
                    }
                }

                foreach (var dto in dtos)
                {
                    var className = dto.ClassName?.Trim() ?? "Chưa rõ";
                    if (!classCache.TryGetValue(className, out var cls))
                    {
                        // SC-06: Tạo ClassCode duy nhất, không dùng new Random() đơn thuần
                        var uniqueCode = $"CL{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(1000, 9999)}";
                        cls = new Class
                        {
                            ClassName = className,
                            ClassCode = uniqueCode,
                            TotalSessions = 100,
                            TimeSlots = "Chưa thiết lập"
                        };
                        _context.Classes.Add(cls);
                        await _context.SaveChangesAsync();
                        classCache[className] = cls;
                    }

                    // Check trùng số điện thoại
                    var student = await _context.Students.FirstOrDefaultAsync(s => s.ParentPhoneNumber == dto.ParentPhoneNumber && s.FullName == dto.FullName && !s.IsDeleted);
                    if (student == null)
                    {
                        var rawPhone = dto.ParentPhoneNumber ?? "";
                        var validPhone = ExtractValidPhoneNumber(rawPhone);
                        var parentName = dto.ParentName ?? "";
                        if (rawPhone != validPhone && Regex.IsMatch(rawPhone, @"[a-zA-Z]"))
                        {
                            parentName = string.IsNullOrWhiteSpace(parentName) ? rawPhone : $"{parentName} ({rawPhone})";
                        }

                        // SC-07: Đảm bảo tính toán nhất quán nếu file chỉ có số buổi còn lại
                        var paidSessions = dto.TotalPaidSessions;
                        if (paidSessions == 0 && dto.TotalRemainingSessions > 0 && dto.TotalAttendedSessions == 0)
                        {
                            paidSessions = dto.TotalRemainingSessions;
                        }

                        student = new Student
                        {
                            FullName = dto.FullName,
                            ParentName = parentName,
                            ParentPhoneNumber = validPhone,
                            DateOfBirth = dto.DateOfBirth,
                            TotalAttendedSessions = dto.TotalAttendedSessions,
                            TotalPaidSessions = paidSessions,
                            ClassId = cls.Id
                        };
                        _context.Students.Add(student);
                        
                        // Tạo tài khoản cho phụ huynh nếu có số điện thoại
                        if (!string.IsNullOrWhiteSpace(validPhone))
                        {
                            var parentUser = await _userManager.FindByNameAsync(validPhone);
                            if (parentUser == null)
                            {
                                var newUser = new AppUser
                                {
                                    UserName = validPhone,
                                    Email = validPhone + "@example.com",
                                    FullName = parentName ?? "Phụ huynh " + dto.FullName
                                };
                                var result = await _userManager.CreateAsync(newUser, validPhone + "@123Aa");
                                if (result.Succeeded)
                                {
                                    await _userManager.AddToRoleAsync(newUser, "User");
                                }
                            }
                        }
                    }
                    else
                    {
                        // Cập nhật nếu đã có (SC-07: nhất quán tính toán buổi còn lại)
                        student.TotalAttendedSessions = dto.TotalAttendedSessions;
                        student.TotalPaidSessions = dto.TotalPaidSessions;
                        if (student.TotalPaidSessions == 0 && dto.TotalRemainingSessions > 0 && dto.TotalAttendedSessions == 0)
                        {
                            student.TotalPaidSessions = dto.TotalRemainingSessions;
                        }
                        student.ClassId = cls.Id;
                    }

                    await UpdateRemainingSessionsAsync(student);

                    // Thêm danh sách ngày điểm danh (SC-04: kèm snapshot ClassId)
                    foreach (var dt in dto.AttendedDates)
                    {
                        var dtUtc = dt.ToUniversalTime();
                        var exists = student.Id > 0 && await _context.Attendances.AnyAsync(a => a.StudentId == student.Id && a.Date.Date == dtUtc.Date);
                        if (!exists)
                        {
                            _context.Attendances.Add(new Attendance {
                                Student = student,
                                ClassId = cls.Id,
                                Date = dtUtc,
                                IsPresent = true
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = "Import thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, inner = ex.InnerException?.Message, trace = ex.StackTrace });
            }
        }

        private async Task UpdateRemainingSessionsAsync(Student student)
        {
            student.TotalRemainingSessions = student.TotalPaidSessions - student.TotalAttendedSessions;
            if (student.TotalRemainingSessions < 3 && !string.IsNullOrWhiteSpace(student.ParentPhoneNumber))
            {
                var parentUser = await _userManager.FindByNameAsync(student.ParentPhoneNumber);
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
