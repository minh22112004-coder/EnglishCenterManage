using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ManagementSystem.Api.Models;

namespace ManagementSystem.Api.Controllers
{
    public class LoginDto
    {
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    public class ChangePasswordDto
    {
        public string OldPassword { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
    }

    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;

        public AuthController(UserManager<AppUser> userManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
        }

        [HttpPost("login")]
        [EnableRateLimiting("LoginLimiter")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            var username = (model.Username ?? "").Trim().Replace(" ", "");
            var normalizedUsername = username.ToLowerInvariant();

            // AUTH-04: Tìm kiếm linh hoạt theo Username hoặc Email (bao gồm cả chuẩn hóa chữ thường)
            var user = await _userManager.FindByEmailAsync(username) 
                       ?? await _userManager.FindByNameAsync(username)
                       ?? await _userManager.FindByEmailAsync(normalizedUsername)
                       ?? await _userManager.FindByNameAsync(normalizedUsername);
            
            if (user != null)
            {
                // AUTH-07: Kiểm tra xem tài khoản có đang bị khóa không
                if (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(user))
                {
                    return Unauthorized(new { message = "Tài khoản đang bị tạm khóa do nhập sai nhiều lần. Vui lòng liên hệ Quản trị viên." });
                }

                if (await _userManager.CheckPasswordAsync(user, model.Password))
                {
                    // Reset số lần đăng nhập sai
                    if (_userManager.SupportsUserLockout)
                    {
                        await _userManager.ResetAccessFailedCountAsync(user);
                    }

                    var userRoles = await _userManager.GetRolesAsync(user);

                    // AUTH-01: Quy tắc phân cấp ưu tiên quyền hạn: Admin > Lecture > User
                    string primaryRole = "User";
                    if (userRoles.Contains("Admin"))
                    {
                        primaryRole = "Admin";
                    }
                    else if (userRoles.Contains("Lecture"))
                    {
                        primaryRole = "Lecture";
                    }
                    else if (userRoles.Any())
                    {
                        primaryRole = userRoles.First();
                    }

                    var authClaims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                        new Claim(ClaimTypes.NameIdentifier, user.Id),
                        new Claim("FullName", user.FullName ?? string.Empty)
                    };

                    foreach (var userRole in userRoles)
                    {
                        authClaims.Add(new Claim(ClaimTypes.Role, userRole));
                    }

                    var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? ""));

                    // AUTH-02: Dùng DateTime.UtcNow thay vì DateTime.Now để chuẩn hóa múi giờ JWT
                    var token = new JwtSecurityToken(
                        issuer: _configuration["Jwt:Issuer"],
                        audience: _configuration["Jwt:Audience"],
                        expires: DateTime.UtcNow.AddHours(3),
                        claims: authClaims,
                        signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                    );

                    // AUTH-06: Trả về primaryRole cho tương thích ngược + mảng roles đầy đủ
                    return Ok(new
                    {
                        token = new JwtSecurityTokenHandler().WriteToken(token),
                        expiration = token.ValidTo,
                        role = primaryRole,
                        roles = userRoles,
                        user = new { user.Id, user.FullName, user.Email, user.PhoneNumber }
                    });
                }
                else
                {
                    // Tăng số lần đăng nhập sai
                    if (_userManager.SupportsUserLockout)
                    {
                        await _userManager.AccessFailedAsync(user);
                    }
                }
            }

            return Unauthorized(new { message = "Sai tài khoản hoặc mật khẩu." });
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            // Do hệ thống dùng JWT Token (Stateless), việc Logout thực tế chỉ cần 
            // Frontend xóa token khỏi LocalStorage. API này chỉ để xác nhận.
            return Ok(new { message = "Đăng xuất thành công." });
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto model)
        {
            // AUTH-05: Tìm user với cơ chế Fallback an toàn qua các Claim
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                             ?? User.FindFirst("sub")?.Value;

                if (!string.IsNullOrEmpty(userId))
                {
                    user = await _userManager.FindByIdAsync(userId);
                }

                if (user == null && !string.IsNullOrEmpty(User.Identity?.Name))
                {
                    user = await _userManager.FindByNameAsync(User.Identity.Name);
                }
            }

            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng (hoặc phiên đăng nhập đã hết hạn)." });

            var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
            
            if (result.Succeeded)
            {
                return Ok(new { message = "Đổi mật khẩu thành công." });
            }

            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(new { message = $"Đổi mật khẩu thất bại: {errors}" });
        }
    }
}
