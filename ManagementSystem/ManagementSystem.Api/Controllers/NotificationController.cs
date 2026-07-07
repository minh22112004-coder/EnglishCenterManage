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
    [Authorize(Roles = "User")]
    public class NotificationController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public NotificationController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyNotifications()
        {
            var user = await _userManager.FindByNameAsync(User.Identity?.Name ?? "");
            if (user == null) return Unauthorized();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new {
                    n.Id,
                    n.Message,
                    n.IsRead,
                    n.HidePopup,
                    n.CreatedAt
                })
                .ToListAsync();

            return Ok(notifications);
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var user = await _userManager.FindByNameAsync(User.Identity?.Name ?? "");
            if (user == null) return Unauthorized();

            var notif = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);
            if (notif == null) return NotFound();

            notif.IsRead = true;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPut("{id}/hide")]
        public async Task<IActionResult> HidePopup(int id)
        {
            var user = await _userManager.FindByNameAsync(User.Identity?.Name ?? "");
            if (user == null) return Unauthorized();

            var notif = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);
            if (notif == null) return NotFound();

            notif.HidePopup = true;
            notif.IsRead = true;
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
