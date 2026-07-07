using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using ManagementSystem.Api.Models;

namespace ManagementSystem.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private readonly string _settingsFilePath;

        public SettingsController()
        {
            _settingsFilePath = Path.Combine(Directory.GetCurrentDirectory(), "paymentSettings.json");
        }

        [HttpGet("payment")]
        public IActionResult GetPaymentSettings()
        {
            if (!System.IO.File.Exists(_settingsFilePath))
            {
                return Ok(new PaymentSettings { BankId = "vietcombank", AccountNo = "0123456789", AccountName = "TRUNG TAM DAO TAO" });
            }

            var json = System.IO.File.ReadAllText(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<PaymentSettings>(json);
            return Ok(settings ?? new PaymentSettings());
        }

        [HttpPost("payment")]
        [Authorize(Roles = "Admin")]
        public IActionResult SavePaymentSettings([FromBody] PaymentSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(_settingsFilePath, json);
            return Ok(new { message = "Lưu thông tin thanh toán thành công." });
        }

        [HttpPost("upload-qr")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UploadQrCode(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("File không hợp lệ.");

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = "qr_" + DateTime.Now.Ticks + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Ok(new { url = $"/uploads/{fileName}" });
        }
    }
}
