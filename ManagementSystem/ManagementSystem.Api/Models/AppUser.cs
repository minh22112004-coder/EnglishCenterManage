using Microsoft.AspNetCore.Identity;

namespace ManagementSystem.Api.Models
{
    public class AppUser : IdentityUser
    {
        public string? FullName { get; set; }
        public string? BankNumber { get; set; }
        public string? BankName { get; set; }
        public string? QrCodeImage { get; set; }
        public string? CanonicalPhone { get; set; }
        
        // Navigation Properties
        public ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
    }
}
