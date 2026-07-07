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
                    StudentIds = _context.Students.Where(s => s.ClassId == c.Id && !s.IsDeleted).Select(s => s.Id).ToList()
                })
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ClassResponseDto>> GetClass(int id)
        {
            var c = await _context.Classes.FindAsync(id);

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
                StudentIds = await _context.Students.Where(s => s.ClassId == c.Id && !s.IsDeleted).Select(s => s.Id).ToListAsync()
            };
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ClassResponseDto>> PostClass(ClassDto classDto)
        {
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
        [Authorize(Roles = "Admin,Lecture")]
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
                var currentStudents = await _context.Students.Where(s => s.ClassId == id).ToListAsync();
                foreach (var student in currentStudents)
                {
                    if (!classDto.StudentIds.Contains(student.Id))
                    {
                        student.ClassId = 0; // Remove from class
                    }
                }

                var newStudents = await _context.Students.Where(s => classDto.StudentIds.Contains(s.Id)).ToListAsync();
                foreach (var student in newStudents)
                {
                    student.ClassId = id;
                }
            }

            await _context.SaveChangesAsync();
            // TODO: Bắt sự kiện AuditLog nếu là Lecture

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

            // EF Core SaveChanges() đã được override để đổi State thành Modified và gán IsDeleted = true
            _context.Classes.Remove(existingClass);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("{id}/attendances")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetClassAttendances(int id)
        {
            var attendances = await _context.Attendances
                .Include(a => a.Student)
                .Where(a => a.Student.ClassId == id)
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
