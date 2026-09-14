using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagementSystem.Api.Data;
using ManagementSystem.Api.Models;

namespace ManagementSystem.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ClassController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ClassController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ClassResponseDto>>> GetClasses()
        {
            return await _context.Classes
                .Select(c => new ClassResponseDto
                {
                    Id = c.Id,
                    ClassCode = c.ClassCode,
                    ClassName = c.ClassName,
                    TotalSessions = c.TotalSessions,
                    TimeSlots = c.TimeSlots,
                    StudentIds = c.Students.Where(s => !s.IsDeleted).Select(s => s.Id).ToList()
                })
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ClassResponseDto>> GetClass(int id)
        {
            var c = await _context.Classes
                .Include(c => c.Students.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync(c => c.Id == id);

            if (c == null)
            {
                return NotFound();
            }

            return new ClassResponseDto
            {
                Id = c.Id,
                ClassCode = c.ClassCode,
                ClassName = c.ClassName,
                TotalSessions = c.TotalSessions,
                TimeSlots = c.TimeSlots,
                StudentIds = c.Students.Where(s => !s.IsDeleted).Select(s => s.Id).ToList()
            };
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ClassResponseDto>> PostClass(ClassDto classDto)
        {
            // Kiểm tra trùng lặp học sinh đang thuộc lớp khác (SC-02)
            if (classDto.StudentIds != null && classDto.StudentIds.Any())
            {
                var conflictingStudents = await _context.Students
                    .Include(s => s.Class)
                    .Where(s => classDto.StudentIds.Contains(s.Id) && s.ClassId != null)
                    .ToListAsync();

                if (conflictingStudents.Any())
                {
                    var conflictDetails = string.Join(", ", conflictingStudents.Select(s => $"{s.FullName} (đang ở lớp {s.Class?.ClassName ?? s.ClassId.ToString()})"));
                    return BadRequest(new { message = $"Không thể thêm học sinh vì đã thuộc lớp khác: {conflictDetails}." });
                }
            }

            var newClass = new Class
            {
                ClassCode = classDto.ClassCode,
                ClassName = classDto.ClassName,
                TotalSessions = classDto.TotalSessions,
                TimeSlots = classDto.TimeSlots
            };

            _context.Classes.Add(newClass);
            await _context.SaveChangesAsync();

            if (classDto.StudentIds != null && classDto.StudentIds.Any())
            {
                var students = await _context.Students.Where(s => classDto.StudentIds.Contains(s.Id)).ToListAsync();
                foreach (var student in students)
                {
                    student.ClassId = newClass.Id;
                }
                await _context.SaveChangesAsync();
            }

            return CreatedAtAction(nameof(GetClass), new { id = newClass.Id }, new ClassResponseDto
            {
                Id = newClass.Id,
                ClassCode = newClass.ClassCode,
                ClassName = newClass.ClassName,
                TotalSessions = newClass.TotalSessions,
                TimeSlots = newClass.TimeSlots,
                StudentIds = classDto.StudentIds
            });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PutClass(int id, ClassDto classDto)
        {
            var existingClass = await _context.Classes.FindAsync(id);
            if (existingClass == null)
            {
                return NotFound();
            }

            existingClass.ClassCode = classDto.ClassCode;
            existingClass.ClassName = classDto.ClassName;
            existingClass.TotalSessions = classDto.TotalSessions;
            existingClass.TimeSlots = classDto.TimeSlots;

            if (classDto.StudentIds != null)
            {
                // 1. Kiểm tra học sinh có đang thuộc lớp khác không (SC-02: Chống Student Stealing)
                var conflictingStudents = await _context.Students
                    .Include(s => s.Class)
                    .Where(s => classDto.StudentIds.Contains(s.Id) && s.ClassId != null && s.ClassId != id)
                    .ToListAsync();

                if (conflictingStudents.Any())
                {
                    var conflictDetails = string.Join(", ", conflictingStudents.Select(s => $"{s.FullName} (đang ở lớp {s.Class?.ClassName ?? s.ClassId.ToString()})"));
                    return BadRequest(new { message = $"Không thể thêm học sinh vì đã thuộc lớp khác: {conflictDetails}." });
                }

                // 2. Gỡ những học sinh không còn trong danh sách lớp (SC-01: nullable ClassId thay vì 0)
                var currentStudents = await _context.Students.Where(s => s.ClassId == id).ToListAsync();
                foreach (var student in currentStudents)
                {
                    if (!classDto.StudentIds.Contains(student.Id))
                    {
                        student.ClassId = null; // Gỡ khỏi lớp an toàn, không vi phạm FK
                    }
                }

                // 3. Gán học sinh mới vào lớp
                var newStudents = await _context.Students.Where(s => classDto.StudentIds.Contains(s.Id)).ToListAsync();
                foreach (var student in newStudents)
                {
                    student.ClassId = id;
                }
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteClass(int id)
        {
            var existingClass = await _context.Classes.FindAsync(id);
            if (existingClass == null)
            {
                return NotFound();
            }

            // Kiểm tra xem lớp còn học sinh active không (SC-03)
            var hasActiveStudents = await _context.Students.AnyAsync(s => s.ClassId == id && !s.IsDeleted);
            if (hasActiveStudents)
            {
                return BadRequest(new { message = "Không thể xóa lớp học vì vẫn còn học sinh đang theo học. Vui lòng chuyển hoặc gỡ học sinh khỏi lớp trước khi xóa." });
            }

            // EF Core SaveChanges() đã được override để đổi State thành Modified và gán IsDeleted = true
            _context.Classes.Remove(existingClass);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("{id}/attendances")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetClassAttendances(int id)
        {
            // Lọc theo ClassId của Attendance (SC-04: bảo toàn lịch sử khi học sinh chuyển lớp)
            var attendances = await _context.Attendances
                .Include(a => a.Student)
                .Where(a => a.ClassId == id || (a.ClassId == null && a.Student.ClassId == id))
                .OrderByDescending(a => a.Date)
                .Select(a => new
                {
                    StudentName = a.Student.FullName,
                    Date = a.Date,
                    IsPresent = a.IsPresent
                })
                .ToListAsync();

            return Ok(attendances);
        }
    }
}
