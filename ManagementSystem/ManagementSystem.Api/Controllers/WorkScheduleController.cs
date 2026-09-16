using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagementSystem.Api.Data;
using ManagementSystem.Api.Models;
using System.Security.Claims;
using ManagementSystem.Api.Constants;
using ManagementSystem.Api.Common;

namespace ManagementSystem.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class WorkScheduleController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public WorkScheduleController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("mychildren-upcoming")]
        [Authorize(Roles = AppRoles.User)]
        public async Task<ActionResult<IEnumerable<object>>> GetMyChildrenUpcomingSchedules()
        {
            var rawPhone = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.MobilePhone)?.Value ?? User.Identity?.Name;
            var userPhone = PhoneHelper.NormalizePhone(rawPhone);
            if (string.IsNullOrEmpty(userPhone)) return Unauthorized();

            var studentClasses = await _context.Students
                .Where(s => s.ParentPhoneCanonical == userPhone && s.ClassId.HasValue && s.ClassId.Value > 0)
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
        [Authorize(Roles = AppRoles.AdminOrLecture)]
        public async Task<IActionResult> GetSchedules()
        {
            if (User.IsInRole(AppRoles.Admin))
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
            else if (User.IsInRole(AppRoles.Lecture))
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
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
            else
            {
                return Forbid();
            }
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> CreateSchedule([FromBody] WorkScheduleDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.UserId))
            {
                return BadRequest(new { message = "UserId không được để trống." });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Khóa dòng AspNetUsers của assignee bằng SELECT ... FOR UPDATE để serialize với việc hạ quyền / xóa giáo viên
                var assignee = await _context.Users
                    .FromSqlRaw("SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {0} FOR UPDATE", dto.UserId)
                    .SingleOrDefaultAsync();

                if (assignee == null || !await _userManager.IsInRoleAsync(assignee, AppRoles.Lecture))
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return BadRequest(new { message = "Người được phân công không tồn tại hoặc không có quyền Giảng viên (Lecture)." });
                }

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
                    if (!string.IsNullOrWhiteSpace(dto.TimeSlot))
                    {
                        foreach (var c in schedule.Classes)
                        {
                            c.TimeSlots = dto.TimeSlot;
                        }
                    }
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
                await transaction.CommitAsync();
                return Ok(new { message = "Phân công lịch dạy thành công.", id = schedule.Id });
            }
            catch (Exception ex) when (DbErrorHelper.IsTransientConcurrencyError(ex))
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return StatusCode(StatusCodes.Status409Conflict, new { message = "Xung đột đồng thời khi phân công lịch dạy, vui lòng thử lại." });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi tạo lịch làm việc." });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = AppRoles.AdminOrLecture)]
        public async Task<IActionResult> UpdateSchedule(int id, [FromBody] WorkScheduleDto dto)
        {
            var schedule = await _context.WorkSchedules.FindAsync(id);
            if (schedule == null) return NotFound();

            if (!User.IsInRole(AppRoles.Admin))
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || schedule.UserId != userId)
                {
                    return Forbid();
                }
            }

            if (User.IsInRole(AppRoles.Admin))
            {
                var targetUserId = (!string.IsNullOrEmpty(dto.UserId) && dto.UserId != "lecture_update")
                    ? dto.UserId
                    : schedule.UserId;

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var userIdsToLock = new List<string>();
                    if (!string.IsNullOrEmpty(schedule.UserId)) userIdsToLock.Add(schedule.UserId);
                    if (!string.IsNullOrEmpty(targetUserId)) userIdsToLock.Add(targetUserId);
                    userIdsToLock = userIdsToLock.Distinct().OrderBy(u => u, StringComparer.Ordinal).ToList();

                    if (userIdsToLock.Count > 0)
                    {
                        var lockedUsers = await _context.Users
                            .FromSqlRaw("SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = ANY({0}) ORDER BY \"Id\" FOR UPDATE", userIdsToLock.ToArray())
                            .ToListAsync();

                        var userMap = lockedUsers.ToDictionary(u => u.Id);

                        if (!string.IsNullOrEmpty(targetUserId))
                        {
                            if (!userMap.TryGetValue(targetUserId, out var assignee) || !await _userManager.IsInRoleAsync(assignee, AppRoles.Lecture))
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return BadRequest(new { message = "Người được phân công không tồn tại hoặc không có quyền Giảng viên (Lecture)." });
                            }
                        }
                    }

                    schedule.AdminNotice = dto.AdminNotice;
                    schedule.RoomCode = dto.RoomCode;
                    if (dto.Date != default)
                    {
                        schedule.Date = dto.Date.ToUniversalTime();
                    }
                    if (!string.IsNullOrEmpty(dto.UserId) && dto.UserId != "lecture_update")
                    {
                        schedule.UserId = dto.UserId;
                    }
                    if (dto.ClassIds != null && dto.ClassIds.Any())
                    {
                        await _context.Entry(schedule).Collection(w => w.Classes).LoadAsync();
                        schedule.Classes = await _context.Classes.Where(c => dto.ClassIds.Contains(c.Id)).ToListAsync();
                    }
                    else
                    {
                        await _context.Entry(schedule).Collection(w => w.Classes).LoadAsync();
                    }

                    if (!string.IsNullOrWhiteSpace(dto.TimeSlot) && schedule.Classes.Any())
                    {
                        foreach (var c in schedule.Classes)
                        {
                            c.TimeSlots = dto.TimeSlot;
                        }
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

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return Ok(new { message = "Cập nhật lịch thành công." });
                }
                catch (Exception ex) when (DbErrorHelper.IsTransientConcurrencyError(ex))
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return StatusCode(StatusCodes.Status409Conflict, new { message = "Xung đột đồng thời khi cập nhật lịch, vui lòng thử lại." });
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi cập nhật lịch." });
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

                await _context.SaveChangesAsync();
                return Ok(new { message = "Cập nhật lịch thành công." });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = AppRoles.Admin)]
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
