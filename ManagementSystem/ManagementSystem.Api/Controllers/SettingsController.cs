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
        private readonly IConfiguration _configuration;

        public SettingsController(IConfiguration configuration)
        {
            _settingsFilePath = Path.Combine(Directory.GetCurrentDirectory(), "paymentSettings.json");
            _configuration = configuration;
        }

        [HttpGet("payment")]
        [Authorize]
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

        [HttpGet("backup")]
        [Authorize(Roles = "Admin")]
        public IActionResult BackupDatabase()
        {
            try
            {
                var connStr = _configuration.GetConnectionString("DefaultConnection");
                // Giả sử định dạng: Host=localhost;Database=ManagementSystemDb;Username=postgres;Password=postgres
                
                string host = "localhost";
                string db = "ManagementSystemDb";
                string user = "postgres";
                string pass = "postgres";
                
                if (connStr != null) {
                    var parts = connStr.Split(';');
                    foreach (var part in parts)
                    {
                        var kvp = part.Split('=');
                        if (kvp.Length == 2)
                        {
                            var k = kvp[0].Trim().ToLower();
                            var v = kvp[1].Trim();
                            if (k == "host" || k == "server") host = v;
                            if (k == "database" || k == "db") db = v;
                            if (k == "username" || k == "user id" || k == "uid") user = v;
                            if (k == "password" || k == "pwd") pass = v;
                        }
                    }
                }

                var backupFileName = $"backup_{db}_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                var backupPath = Path.Combine(Path.GetTempPath(), backupFileName);

                string pgDumpPath = "pg_dump";
                string[] possiblePaths = {
                    @"C:\Program Files\PostgreSQL\18\bin\pg_dump.exe",
                    @"C:\Program Files\PostgreSQL\17\bin\pg_dump.exe",
                    @"C:\Program Files\PostgreSQL\16\bin\pg_dump.exe",
                    @"C:\Program Files\PostgreSQL\15\bin\pg_dump.exe",
                    @"C:\Program Files\PostgreSQL\14\bin\pg_dump.exe"
                };

                foreach (var path in possiblePaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        pgDumpPath = path;
                        break;
                    }
                }

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = pgDumpPath,
                    Arguments = $"-U {user} -h {host} -F c -b -v -f \"{backupPath}\" {db}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                // Set password via environment variable to avoid prompt
                startInfo.EnvironmentVariables["PGPASSWORD"] = pass;

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        process.WaitForExit();
                        if (process.ExitCode != 0)
                        {
                            var error = process.StandardError.ReadToEnd();
                            return StatusCode(500, new { message = "Lỗi khi backup CSDL: " + error });
                        }
                    }
                }

                if (!System.IO.File.Exists(backupPath))
                {
                    return StatusCode(500, new { message = "Không tạo được file backup." });
                }

                var fileBytes = System.IO.File.ReadAllBytes(backupPath);
                System.IO.File.Delete(backupPath); // Dọn dẹp file tạm
                
                return File(fileBytes, "application/octet-stream", backupFileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống: " + ex.Message });
            }
        }
    }
}
