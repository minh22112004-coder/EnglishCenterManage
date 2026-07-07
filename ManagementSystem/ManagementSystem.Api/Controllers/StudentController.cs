using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagementSystem.Api.Data;
using ManagementSystem.Api.Models;
using System.Text.RegularExpressions;

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
            var query = _context.Students.AsQueryable();
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
                    ClassId = s.ClassId
                })
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<StudentResponseDto>> GetStudent(int id)
        {
            var s = await _context.Students.FindAsync(id);

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
                AttendanceRate = attendanceRate
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

            var student = new Student
            {
                FullName = dto.FullName,
                DateOfBirth = dto.DateOfBirth,
                ParentName = parentName,
                ParentPhoneNumber = validPhone,
                TotalAttendedSessions = dto.TotalAttendedSessions,
                TotalPaidSessions = dto.TotalPaidSessions,
                ClassId = dto.ClassId
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
                ClassId = student.ClassId
            });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Lecture")]
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
            existing.ClassId = dto.ClassId;

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
                    ClassId = s.ClassId
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

            foreach (var student in studentsToRecover)
            {
                student.IsDeleted = false;
                student.DeletedAt = null;
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
            var students = await _context.Students.ToListAsync();
            int createdCount = 0;
            
            foreach (var student in students)
            {
                var rawPhone = student.ParentPhoneNumber;
                var validPhone = ExtractValidPhoneNumber(rawPhone);

                if (!string.IsNullOrWhiteSpace(validPhone))
                {
                    // Clean up DB if dirty
                    if (rawPhone != validPhone && Regex.IsMatch(rawPhone ?? "", @"[a-zA-Z]"))
                    {
                        student.ParentName = string.IsNullOrWhiteSpace(student.ParentName) ? rawPhone : $"{student.ParentName} ({rawPhone})";
                    }
                    if (student.ParentPhoneNumber != validPhone)
                    {
                        student.ParentPhoneNumber = validPhone;
                        _context.Entry(student).State = EntityState.Modified;
                    }

                    if (!string.IsNullOrWhiteSpace(validPhone))
                    {
                        var parentUser = await _userManager.FindByNameAsync(validPhone);
                        if (parentUser == null)
                        {
                            var newUser = new AppUser
                            {
                                UserName = validPhone,
                                Email = validPhone + "@example.com",
                                FullName = student.ParentName ?? "Phụ huynh " + student.FullName
                            };
                            var result = await _userManager.CreateAsync(newUser, validPhone + "@123Aa");
                            if (result.Succeeded)
                            {
                                await _userManager.AddToRoleAsync(newUser, "User");
                                createdCount++;
                            }
                        }
                    }
                }
            }
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Đã đồng bộ thành công. Dọn dẹp dữ liệu và tạo mới {createdCount} tài khoản phụ huynh." });
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
            var userPhone = User.Identity?.Name;
            if (string.IsNullOrEmpty(userPhone)) return Unauthorized();

            var students = await _context.Students
                .Where(s => s.ParentPhoneNumber == userPhone)
                .ToListAsync();

            var result = new List<StudentResponseDto>();
            foreach(var s in students)
            {
                var attendanceCount = await _context.Attendances.CountAsync(a => a.StudentId == s.Id);
                var presentCount = await _context.Attendances.CountAsync(a => a.StudentId == s.Id && a.IsPresent);
                int attendanceRate = attendanceCount == 0 ? 100 : (int)Math.Round((double)presentCount / attendanceCount * 100);
                
                // Trả về kèm class name để hiện thị
                var className = "";
                if (s.ClassId > 0) {
                    var cls = await _context.Classes.FindAsync(s.ClassId);
                    className = cls?.ClassName ?? "";
                }

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
                    ClassName = className
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

                foreach (var dto in dtos)
                {
                    // Tìm class theo tên
                    var cls = await _context.Classes.FirstOrDefaultAsync(c => c.ClassName == dto.ClassName && !c.IsDeleted);
                    if (cls == null)
                    {
                        // Tự động tạo class nếu chưa có
                        cls = new Class
                        {
                            ClassName = dto.ClassName ?? "Chưa rõ",
                            ClassCode = "CL" + new Random().Next(1000, 9999), // Dummy code
                            TotalSessions = 100,
                            TimeSlots = "Chưa thiết lập"
                        };
                        _context.Classes.Add(cls);
                        await _context.SaveChangesAsync();
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

                        student = new Student
                        {
                            FullName = dto.FullName,
                            ParentName = parentName,
                            ParentPhoneNumber = validPhone,
                            DateOfBirth = dto.DateOfBirth,
                            TotalAttendedSessions = dto.TotalAttendedSessions,
                            TotalPaidSessions = dto.TotalPaidSessions,
                            TotalRemainingSessions = dto.TotalRemainingSessions,
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
                        // Cập nhật nếu đã có
                        student.TotalAttendedSessions = dto.TotalAttendedSessions;
                        student.TotalPaidSessions = dto.TotalPaidSessions;
                        student.ClassId = cls.Id;
                    }

                    await UpdateRemainingSessionsAsync(student);

                    // Thêm danh sách ngày điểm danh
                    foreach (var dt in dto.AttendedDates)
                    {
                        var dtUtc = dt.ToUniversalTime();
                        var exists = student.Id > 0 && await _context.Attendances.AnyAsync(a => a.StudentId == student.Id && a.Date.Date == dtUtc.Date);
                        if (!exists)
                        {
                            _context.Attendances.Add(new Attendance {
                                Student = student,
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
            if (student.TotalRemainingSessions < 0 && !string.IsNullOrWhiteSpace(student.ParentPhoneNumber))
            {
                var parentUser = await _userManager.FindByNameAsync(student.ParentPhoneNumber);
                if (parentUser != null)
                {
                    var existingNotif = await _context.Notifications
                        .FirstOrDefaultAsync(n => n.UserId == parentUser.Id && !n.IsRead && n.Message.Contains(student.FullName));
                    
                    if (existingNotif == null)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            UserId = parentUser.Id,
                            Message = $"Học sinh {student.FullName} đã học lố {-student.TotalRemainingSessions} buổi. Quý phụ huynh vui lòng hoàn thành đóng học phí.",
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
