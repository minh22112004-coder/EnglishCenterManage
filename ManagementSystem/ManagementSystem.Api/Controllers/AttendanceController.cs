using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using ManagementSystem.Api.Data;
using ManagementSystem.Api.Models;

namespace ManagementSystem.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Lecture")]
    public class AttendanceController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public AttendanceController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("date/{date}")]
        public async Task<IActionResult> GetAttendanceByDate(DateTime date)
        {
            var attendanceDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            var attendances = await _context.Attendances
                .Where(a => a.Date.Date == attendanceDate)
                .Select(a => new { a.StudentId, a.IsPresent })
                .ToListAsync();

            return Ok(attendances);
        }

        [HttpPost("batch")]
        public async Task<IActionResult> MarkAttendanceBatch([FromBody] List<AttendanceDto> dtos)
        {
            if (dtos == null || !dtos.Any())
            {
                return BadRequest("Không có dữ liệu điểm danh.");
            }

            foreach (var dto in dtos)
            {
                var student = await _context.Students.FindAsync(dto.StudentId);
                if (student == null) continue;

                // Cắt bỏ phần giờ, chỉ so sánh ngày
                var attendanceDate = DateTime.SpecifyKind(dto.Date.Date, DateTimeKind.Utc);

                var existingAttendance = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.StudentId == dto.StudentId && a.Date.Date == attendanceDate);

                if (existingAttendance != null)
                {
                    // Nếu đổi trạng thái
                    if (existingAttendance.IsPresent != dto.IsPresent)
                    {
                        if (dto.IsPresent)
                            student.TotalAttendedSessions += 1;
                        else
                            student.TotalAttendedSessions -= 1;
                            
                        existingAttendance.IsPresent = dto.IsPresent;
                    }
                }
                else
                {
                    // Tạo mới
                    var attendance = new Attendance
                    {
                        StudentId = dto.StudentId,
                        Date = DateTime.SpecifyKind(dto.Date, DateTimeKind.Utc), // Giữ nguyên time client gửi (đã chuẩn hoá UTC+7) để lưu
                        IsPresent = dto.IsPresent
                    };
                    _context.Attendances.Add(attendance);

                    if (dto.IsPresent)
                        student.TotalAttendedSessions += 1;
                }

                // Cập nhật số buổi còn lại và gửi thông báo
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

            await _context.SaveChangesAsync();

            return Ok(new { message = "Lưu điểm danh thành công." });
        }

        [HttpGet("report")]
        public async Task<IActionResult> GetAttendanceReport()
        {
            var students = await _context.Students
                .Include(s => s.Class)
                .Where(s => !s.IsDeleted)
                .ToListAsync();

            var attendances = await _context.Attendances
                .ToListAsync();

            var result = students.Select(s => new AttendanceReportDto
            {
                StudentId = s.Id,
                FullName = s.FullName,
                ParentName = s.ParentName,
                ParentPhoneNumber = s.ParentPhoneNumber,
                ClassName = s.Class?.ClassName ?? "Chưa có lớp",
                PresentDates = attendances.Where(a => a.StudentId == s.Id && a.IsPresent).Select(a => a.Date).ToList(),
                AbsentDates = attendances.Where(a => a.StudentId == s.Id && !a.IsPresent).Select(a => a.Date).ToList()
            }).ToList();

            return Ok(result);
        }

        [HttpGet("student/{studentId}")]
        [Authorize(Roles = "Admin,Lecture,User")]
        public async Task<IActionResult> GetAttendanceByStudent(int studentId)
        {
            var attendances = await _context.Attendances
                .Where(a => a.StudentId == studentId && a.IsPresent)
                .OrderByDescending(a => a.Date)
                .Select(a => a.Date)
                .ToListAsync();

            return Ok(attendances);
        }
    }
}
