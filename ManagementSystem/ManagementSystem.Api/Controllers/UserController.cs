using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagementSystem.Api.Data;
using ManagementSystem.Api.Models;

namespace ManagementSystem.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
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
            var users = await _userManager.GetUsersInRoleAsync("Lecture");
            
            // Lấy danh sách lịch dạy hôm nay
            var today = DateTime.UtcNow.Date; 
            // Cẩn thận múi giờ, ở đây lấy tạm UtcNow.Date hoặc có thể pass từ frontend lên. 
            // Tạm thời lấy Date == DateTime.Today theo local server.
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
            if (string.IsNullOrEmpty(dto.PhoneNumber)) return BadRequest("Số điện thoại không được để trống.");

            var existingUser = await _userManager.FindByNameAsync(dto.PhoneNumber);
            if (existingUser != null)
            {
                // Người dùng đã tồn tại (VD: đã là User/Phụ huynh), gán thêm quyền Lecture và cập nhật thông tin
                if (!await _userManager.IsInRoleAsync(existingUser, "Lecture"))
                {
                    await _userManager.AddToRoleAsync(existingUser, "Lecture");
                }
                
                existingUser.FullName = dto.FullName ?? existingUser.FullName;
                existingUser.BankNumber = dto.BankNumber ?? existingUser.BankNumber;
                existingUser.BankName = dto.BankName ?? existingUser.BankName;
                if (!string.IsNullOrEmpty(dto.QrCodeImage)) existingUser.QrCodeImage = dto.QrCodeImage;

                await _userManager.UpdateAsync(existingUser);
                
                return Ok(new { message = "Số điện thoại đã tồn tại trong hệ thống. Đã tích hợp thêm quyền Giáo viên cho tài khoản này.", id = existingUser.Id });
            }

            var user = new AppUser
            {
                UserName = dto.PhoneNumber,
                Email = dto.PhoneNumber + "@example.com",
                PhoneNumber = dto.PhoneNumber,
                FullName = dto.FullName,
                BankNumber = dto.BankNumber,
                BankName = dto.BankName,
                QrCodeImage = dto.QrCodeImage
            };

            var password = dto.PhoneNumber + "@123Aa"; // Mật khẩu mặc định
            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Lecture");
                return Ok(new { message = "Tạo giáo viên thành công", id = user.Id });
            }

            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(new { message = $"Lỗi tạo giáo viên: {errors}" });
        }

        [HttpPut("lectures/{id}")]
        public async Task<IActionResult> UpdateLecture(string id, [FromBody] LectureDto dto)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.FullName = dto.FullName;
            user.PhoneNumber = dto.PhoneNumber;
            user.BankNumber = dto.BankNumber;
            user.BankName = dto.BankName;
            user.QrCodeImage = dto.QrCodeImage;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                return Ok(new { message = "Cập nhật thành công" });
            }

            return BadRequest(result.Errors);
        }

        [HttpDelete("lectures/{id}")]
        public async Task<IActionResult> DeleteLecture(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                return Ok(new { message = "Xóa giáo viên thành công" });
            }

            return BadRequest(result.Errors);
        }
    }
}
