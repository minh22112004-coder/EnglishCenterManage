using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using ManagementSystem.Api.Data;
using ManagementSystem.Api.Models;
using System.Security.Claims;
using ManagementSystem.Api.Constants;
using ManagementSystem.Api.Common;

using ManagementSystem.Api.Services;

namespace ManagementSystem.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AttendanceController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IAttendanceService _attendanceService;

        public AttendanceController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            IAttendanceService attendanceService)
        {
            _context = context;
            _userManager = userManager;
            _attendanceService = attendanceService;
        }

        [HttpGet("date/{date}")]
        [Authorize(Roles = AppRoles.AdminOrLecture)]
        public async Task<IActionResult> GetAttendanceByDate(DateTime date)
        {
            var attendanceDay = DateOnly.FromDateTime(date);

            IQueryable<Attendance> query = _context.Attendances
                .Where(a => a.AttendanceDay == attendanceDay);

            if (User.IsInRole(AppRoles.Admin))
            {
                // Admin has full access
            }
            else if (User.IsInRole(AppRoles.Lecture))
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                var myClassIds = await _context.WorkSchedules
                    .Where(w => w.UserId == userId)
                    .SelectMany(w => w.Classes.Select(c => c.Id))
                    .Distinct()
                    .ToListAsync();

                query = query.Where(a => (a.ClassId.HasValue && myClassIds.Contains(a.ClassId.Value))
                                      || (a.Student.ClassId.HasValue && myClassIds.Contains(a.Student.ClassId.Value)));
            }
            else
            {
                return Forbid();
            }

            var attendances = await query
                .Select(a => new { a.StudentId, a.IsPresent })
                .ToListAsync();

            return Ok(attendances);
        }

        [HttpPost("batch")]
        [Authorize(Roles = AppRoles.AdminOrLecture)]
        public async Task<IActionResult> MarkAttendanceBatch([FromBody] List<AttendanceDto> dtos)
        {
            var isAdmin = User.IsInRole(AppRoles.Admin);
            var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            var result = await _attendanceService.MarkAttendanceBatchAsync(dtos, isAdmin, userId, HttpContext.RequestAborted);

            return result.Status switch
            {
                AttendanceBatchStatus.Success => Ok(new { message = result.Message }),
                AttendanceBatchStatus.BadRequest => BadRequest(new { message = result.Message }),
                AttendanceBatchStatus.Forbidden => Forbid(),
                AttendanceBatchStatus.ConflictDuplicate => Conflict(new { message = result.Message }),
                AttendanceBatchStatus.ConflictConcurrency => StatusCode(StatusCodes.Status409Conflict, new { message = result.Message }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.Message })
            };
        }

        [HttpGet("report")]
        [Authorize(Roles = AppRoles.AdminOrLecture)]
        public async Task<IActionResult> GetAttendanceReport()
        {
            IQueryable<Student> studentsQuery = _context.Students
                .Include(s => s.Class)
                .Where(s => !s.IsDeleted);

            if (User.IsInRole(AppRoles.Admin))
            {
                // Admin has full access
            }
            else if (User.IsInRole(AppRoles.Lecture))
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                var myClassIds = await _context.WorkSchedules
                    .Where(w => w.UserId == userId)
                    .SelectMany(w => w.Classes.Select(c => c.Id))
                    .Distinct()
                    .ToListAsync();

                studentsQuery = studentsQuery.Where(s => s.ClassId.HasValue && myClassIds.Contains(s.ClassId.Value));
            }
            else
            {
                return Forbid();
            }

            var students = await studentsQuery.ToListAsync();
            var studentIds = students.Select(s => s.Id).ToList();

            var attendances = await _context.Attendances
                .Where(a => studentIds.Contains(a.StudentId))
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
        [Authorize(Roles = AppRoles.All)]
        public async Task<IActionResult> GetAttendanceByStudent(int studentId)
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student == null)
            {
                return NotFound();
            }

            if (!User.IsInRole(AppRoles.Admin))
            {
                var userPhone = PhoneHelper.NormalizePhone(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.MobilePhone)?.Value ?? User.Identity?.Name);
                bool isParentOwner = User.IsInRole(AppRoles.User) && !string.IsNullOrEmpty(userPhone) && student.ParentPhoneCanonical == userPhone;

                if (User.IsInRole(AppRoles.Lecture))
                {
                    var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                    bool isAssignedLecture = student.ClassId.HasValue && !string.IsNullOrEmpty(userId) && await _context.WorkSchedules
                        .AnyAsync(w => w.UserId == userId && w.Classes.Any(c => c.Id == student.ClassId.Value));

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

            var attendances = await _context.Attendances
                .Where(a => a.StudentId == studentId && a.IsPresent)
                .OrderByDescending(a => a.Date)
                .Select(a => a.Date)
                .ToListAsync();

            return Ok(attendances);
        }
    }
}
