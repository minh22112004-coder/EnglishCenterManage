using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
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

        private static string NormalizePhone(string? rawPhone)
        {
            if (string.IsNullOrWhiteSpace(rawPhone)) return "";
            var cleaned = Regex.Replace(rawPhone, @"[\s\.\-\(\)\+]+", "");
            var match = Regex.Match(cleaned, @"(0|84)\d{9}");
            if (!match.Success) return "";
            var phoneStr = match.Value;
            if (phoneStr.StartsWith("84") && phoneStr.Length == 11)
                return "0" + phoneStr.Substring(2);
            return phoneStr;
        }

        [HttpPost("lectures")]
        public async Task<IActionResult> CreateLecture([FromBody] LectureDto dto)
        {
            var newPhone = NormalizePhone(dto.PhoneNumber);
            if (string.IsNullOrEmpty(newPhone))
                return BadRequest(new { message = "Số điện thoại không hợp lệ hoặc không đúng định dạng." });

            var existingUser = await _userManager.FindByNameAsync(newPhone);
            if (existingUser != null)
            {
                // Người dùng đã tồn tại (VD: đã là User/Phụ huynh), gán thêm quyền Lecture và cập nhật thông tin
                if (!await _userManager.IsInRoleAsync(existingUser, "Lecture"))
                {
                    await _userManager.AddToRoleAsync(existingUser, "Lecture");
                }
                
                existingUser.FullName = dto.FullName ?? existingUser.FullName;
                existingUser.PhoneNumber = newPhone;
                existingUser.BankNumber = dto.BankNumber ?? existingUser.BankNumber;
                existingUser.BankName = dto.BankName ?? existingUser.BankName;
                if (!string.IsNullOrEmpty(dto.QrCodeImage)) existingUser.QrCodeImage = dto.QrCodeImage;

                // Mở khóa tài khoản nếu đang bị Lockout (trường hợp phụ huynh cũ bị khóa)
                if (_userManager.SupportsUserLockout)
                {
                    await _userManager.SetLockoutEndDateAsync(existingUser, null);
                    await _userManager.ResetAccessFailedCountAsync(existingUser);
                }

                await _userManager.UpdateAsync(existingUser);
                
                return Ok(new { message = "Số điện thoại đã tồn tại trong hệ thống. Đã tích hợp thêm quyền Giáo viên và kích hoạt tài khoản.", id = existingUser.Id });
            }

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

            var password = newPhone + "@123Aa"; // Mật khẩu mặc định
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

            var newPhone = NormalizePhone(dto.PhoneNumber);
            if (string.IsNullOrEmpty(newPhone))
                return BadRequest(new { message = "Số điện thoại không hợp lệ hoặc không đúng định dạng." });

            // Đồng bộ UserName & Email nếu SĐT thay đổi
            if (user.UserName != newPhone)
            {
                // Kiểm tra SĐT mới chưa có ai dùng
                var conflictUser = await _userManager.FindByNameAsync(newPhone);
                if (conflictUser != null && conflictUser.Id != user.Id)
                    return Conflict(new { message = "Số điện thoại mới đã được dùng bởi tài khoản khác." });

                await _userManager.SetUserNameAsync(user, newPhone);
                await _userManager.SetEmailAsync(user, newPhone + "@example.com");
            }

            user.PhoneNumber = newPhone;
            user.FullName = dto.FullName;
            user.BankNumber = dto.BankNumber;
            user.BankName = dto.BankName;
            if (!string.IsNullOrEmpty(dto.QrCodeImage)) user.QrCodeImage = dto.QrCodeImage;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                return Ok(new { message = "Cập nhật thành công" });
            }

            return BadRequest(result.Errors);
        }

        [HttpPost("lectures/{id}/reset-password")]
        public async Task<IActionResult> ResetLecturePassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

            // Không cho reset tài khoản Admin
            if (await _userManager.IsInRoleAsync(user, "Admin"))
                return Forbid();

            var phone = user.PhoneNumber ?? user.UserName ?? "";
            if (string.IsNullOrEmpty(phone))
                return BadRequest(new { message = "Không thể xác định SĐT để tạo mật khẩu mặc định." });

            var removeResult = await _userManager.RemovePasswordAsync(user);
            if (!removeResult.Succeeded)
                return BadRequest(new { message = "Không thể xóa mật khẩu cũ." });

            var addResult = await _userManager.AddPasswordAsync(user, phone + "@123Aa");
            if (addResult.Succeeded)
            {
                if (_userManager.SupportsUserLockout)
                {
                    await _userManager.SetLockoutEndDateAsync(user, null);
                    await _userManager.ResetAccessFailedCountAsync(user);
                }
                return Ok(new { message = $"Đã reset mật khẩu về mặc định: {phone}@123Aa" });
            }

            var errors = string.Join(", ", addResult.Errors.Select(e => e.Description));
            return BadRequest(new { message = $"Reset thất bại: {errors}" });
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

        [HttpGet("parents")]
        public async Task<IActionResult> GetParents()
        {
            var users = await _userManager.GetUsersInRoleAsync("User");
            var result = new List<ParentResponseDto>();

            var allStudents = await _context.Students
                .IgnoreQueryFilters()
                .Include(s => s.Class)
                .ToListAsync();

            var studentsByPhone = allStudents
                .Where(s => !string.IsNullOrWhiteSpace(s.ParentPhoneNumber))
                .GroupBy(s => s.ParentPhoneNumber)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var u in users)
            {
                if (await _userManager.IsInRoleAsync(u, "Admin"))
                    continue;

                var isLocked = _userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(u);
                var userRoles = (await _userManager.GetRolesAsync(u)).ToList();

                var children = new List<ParentChildItemDto>();
                var phone = u.PhoneNumber ?? u.UserName ?? "";
                if (studentsByPhone.TryGetValue(phone, out var matchedStudents))
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
            if (roles.Contains("Lecture"))
            {
                var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")).Date;
                var todayUtc = DateTime.SpecifyKind(todayLocal, DateTimeKind.Utc);

                upcomingSchedules = await _context.WorkSchedules
                    .Where(w => w.UserId == user.Id && w.Date.Date >= todayUtc)
                    .CountAsync();
            }

            var phone = user.PhoneNumber ?? user.UserName ?? "";
            var children = await _context.Students
                .Where(s => s.ParentPhoneNumber == phone)
                .Select(s => s.FullName)
                .ToListAsync();

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
            var user = await _userManager.FindByIdAsync(id)
                       ?? await _userManager.FindByNameAsync(id)
                       ?? await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == id);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return BadRequest(new { message = "Không thể thay đổi vai trò của tài khoản Quản trị viên (Admin)." });
            }

            var validRoles = new[] { "Lecture", "User", "Both" };
            if (!validRoles.Contains(dto.TargetRole))
            {
                return BadRequest(new { message = "Vai trò đích không hợp lệ. Chỉ chấp nhận: Lecture, User, hoặc Both." });
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            string warningMessage = "";

            if (dto.TargetRole == "Lecture")
            {
                if (currentRoles.Contains("User"))
                {
                    await _userManager.RemoveFromRoleAsync(user, "User");
                }
                if (!currentRoles.Contains("Lecture"))
                {
                    await _userManager.AddToRoleAsync(user, "Lecture");
                }

                if (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(user))
                {
                    await _userManager.SetLockoutEndDateAsync(user, null);
                    await _userManager.ResetAccessFailedCountAsync(user);
                }

                if (!string.IsNullOrWhiteSpace(dto.BankName)) user.BankName = dto.BankName;
                if (!string.IsNullOrWhiteSpace(dto.BankNumber)) user.BankNumber = dto.BankNumber;
                if (!string.IsNullOrWhiteSpace(dto.QrCodeImage)) user.QrCodeImage = dto.QrCodeImage;
            }
            else if (dto.TargetRole == "User")
            {
                var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")).Date;
                var todayUtc = DateTime.SpecifyKind(todayLocal, DateTimeKind.Utc);
                var upcoming = await _context.WorkSchedules
                    .Where(w => w.UserId == user.Id && w.Date.Date >= todayUtc)
                    .CountAsync();

                if (upcoming > 0)
                {
                    warningMessage = $" Lưu ý: Người này có {upcoming} ca dạy từ hôm nay trở đi cần được phân công lại.";
                }

                if (currentRoles.Contains("Lecture"))
                {
                    await _userManager.RemoveFromRoleAsync(user, "Lecture");
                }
                if (!currentRoles.Contains("User"))
                {
                    await _userManager.AddToRoleAsync(user, "User");
                }
            }
            else if (dto.TargetRole == "Both")
            {
                if (!currentRoles.Contains("Lecture"))
                {
                    await _userManager.AddToRoleAsync(user, "Lecture");
                }
                if (!currentRoles.Contains("User"))
                {
                    await _userManager.AddToRoleAsync(user, "User");
                }

                if (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(user))
                {
                    await _userManager.SetLockoutEndDateAsync(user, null);
                    await _userManager.ResetAccessFailedCountAsync(user);
                }

                if (!string.IsNullOrWhiteSpace(dto.BankName)) user.BankName = dto.BankName;
                if (!string.IsNullOrWhiteSpace(dto.BankNumber)) user.BankNumber = dto.BankNumber;
                if (!string.IsNullOrWhiteSpace(dto.QrCodeImage)) user.QrCodeImage = dto.QrCodeImage;
            }

            if (dto.ResetPasswordToDefault)
            {
                var phone = user.PhoneNumber ?? user.UserName ?? "";
                if (!string.IsNullOrEmpty(phone))
                {
                    await _userManager.RemovePasswordAsync(user);
                    await _userManager.AddPasswordAsync(user, phone + "@123Aa");
                    if (_userManager.SupportsUserLockout)
                    {
                        await _userManager.SetLockoutEndDateAsync(user, null);
                        await _userManager.ResetAccessFailedCountAsync(user);
                    }
                }
            }

            await _userManager.UpdateAsync(user);

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
                message = $"Đã cập nhật vai trò của người dùng thành: {roleDescription}.{warningMessage}",
                roles = updatedRoles,
                userId = user.Id
            });
        }
    }
}
