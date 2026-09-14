using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagementSystem.Api.Data;
using ManagementSystem.Api.Models;
using System.Security.Claims;

namespace ManagementSystem.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class WorkScheduleController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WorkScheduleController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("mychildren-upcoming")]
        [Authorize(Roles = "User")]
        public async Task<ActionResult<IEnumerable<object>>> GetMyChildrenUpcomingSchedules()
        {
            var userPhone = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.MobilePhone)?.Value ?? User.Identity?.Name;
            if (string.IsNullOrEmpty(userPhone)) return Unauthorized();

            var studentClasses = await _context.Students
                .IgnoreQueryFilters()
                .Where(s => s.ParentPhoneNumber == userPhone && s.ClassId.HasValue && s.ClassId.Value > 0)
                .Select(s => new { s.Id, s.FullName, ClassId = s.ClassId!.Value })
                .ToListAsync();

            var classIds = studentClasses.Select(s => s.ClassId).Distinct().ToList();
            var today = DateTime.UtcNow.Date;
            
            // Tính ngày Chủ nhật của tuần hiện tại
            int daysUntilSunday = ((int)DayOfWeek.Sunday - (int)today.DayOfWeek + 7) % 7;
            if (daysUntilSunday == 0 && today.DayOfWeek != DayOfWeek.Sunday) daysUntilSunday = 7;
            var endOfWeek = today.AddDays(daysUntilSunday).AddDays(1).AddTicks(-1); // Cuối ngày CN

            var upcomingSchedules = await _context.WorkSchedules
                .Include(w => w.Classes)
                .Where(w => w.Date >= today && w.Date <= endOfWeek && w.Classes.Any(c => classIds.Contains(c.Id)))
                .OrderBy(w => w.Date)
                .Select(w => new
                {
                    Id = w.Id,
                    Date = w.Date,
                    RoomCode = w.RoomCode,
                    Classes = w.Classes.Where(c => classIds.Contains(c.Id)).Select(c => new
                    {
                        c.Id,
                        c.ClassName,
                        c.TimeSlots
                    }).ToList()
                })
                .ToListAsync();

            return Ok(upcomingSchedules);
        }

        [HttpGet]
        public async Task<IActionResult> GetSchedules()
        {
            if (User.IsInRole("Admin"))
            {
                var schedules = await _context.WorkSchedules
                    .Include(w => w.User)
                    .Include(w => w.Classes)
                    .Include(w => w.Tasks)
                    .OrderByDescending(w => w.Date)
                    .ToListAsync();
                    
                return Ok(schedules.Select(s => new {
                    s.Id,
                    s.Date,
                    s.IsTaskCompleted,
                    s.AdminNotice,
                    s.LectureNotice,
                    s.RoomCode,
                    LectureName = s.User?.FullName,
                    LectureId = s.UserId,
                    Classes = s.Classes.Select(c => new { c.Id, c.ClassName, c.ClassCode, c.TimeSlots }),
                    Tasks = s.Tasks.Select(t => new { t.Id, t.TaskName, t.IsCompleted })
                }));
            }
            else
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var schedules = await _context.WorkSchedules
                    .Include(w => w.Classes)
                    .Include(w => w.Tasks)
                    .Where(w => w.UserId == userId)
                    .OrderBy(w => w.Date)
                    .ToListAsync();
                
                return Ok(schedules.Select(s => new {
                    s.Id,
                    s.Date,
                    s.IsTaskCompleted,
                    s.AdminNotice,
                    s.LectureNotice,
                    s.RoomCode,
                    Classes = s.Classes.Select(c => new { c.Id, c.ClassName, c.ClassCode, c.TimeSlots }),
                    Tasks = s.Tasks.Select(t => new { t.Id, t.TaskName, t.IsCompleted })
                }));
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateSchedule([FromBody] WorkScheduleDto dto)
        {
            var schedule = new WorkSchedule
            {
                UserId = dto.UserId,
                Date = dto.Date.ToUniversalTime(),
                AdminNotice = dto.AdminNotice,
                LectureNotice = dto.LectureNotice,
                IsTaskCompleted = false,
                RoomCode = dto.RoomCode
            };
            
            if (dto.ClassIds != null && dto.ClassIds.Any())
            {
                schedule.Classes = await _context.Classes.Where(c => dto.ClassIds.Contains(c.Id)).ToListAsync();
            }
            
            if (dto.Tasks != null && dto.Tasks.Any())
            {
                schedule.Tasks = dto.Tasks.Select(t => new ScheduleTask
                {
                    TaskName = t.TaskName,
                    IsCompleted = false
                }).ToList();
            }

            _context.WorkSchedules.Add(schedule);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Phân công lịch dạy thành công.", id = schedule.Id });
        }
        
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Lecture")]
        public async Task<IActionResult> UpdateSchedule(int id, [FromBody] WorkScheduleDto dto)
        {
            var schedule = await _context.WorkSchedules.FindAsync(id);
            if (schedule == null) return NotFound();
            
            if (User.IsInRole("Admin"))
            {
                schedule.AdminNotice = dto.AdminNotice;
                schedule.RoomCode = dto.RoomCode;
                if (dto.ClassIds != null)
                {
                    await _context.Entry(schedule).Collection(w => w.Classes).LoadAsync();
                    schedule.Classes = await _context.Classes.Where(c => dto.ClassIds.Contains(c.Id)).ToListAsync();
                }
                
                if (dto.Tasks != null)
                {
                    await _context.Entry(schedule).Collection(w => w.Tasks).LoadAsync();
                    _context.ScheduleTasks.RemoveRange(schedule.Tasks);
                    schedule.Tasks = dto.Tasks.Select(t => new ScheduleTask
                    {
                        TaskName = t.TaskName,
                        IsCompleted = t.IsCompleted
                    }).ToList();
                }
            }
            else
            {
                schedule.LectureNotice = dto.LectureNotice;
                schedule.IsTaskCompleted = dto.IsTaskCompleted;
                schedule.RoomCode = dto.RoomCode;
                
                if (dto.Tasks != null)
                {
                    await _context.Entry(schedule).Collection(w => w.Tasks).LoadAsync();
                    foreach (var t in dto.Tasks)
                    {
                        var existingTask = schedule.Tasks.FirstOrDefault(st => st.Id == t.Id);
                        if (existingTask != null)
                        {
                            existingTask.IsCompleted = t.IsCompleted;
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật lịch thành công." });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteSchedule(int id)
        {
            var schedule = await _context.WorkSchedules.FindAsync(id);
            if (schedule == null) return NotFound();

            _context.WorkSchedules.Remove(schedule);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã xóa lịch phân công." });
        }
    }
}
