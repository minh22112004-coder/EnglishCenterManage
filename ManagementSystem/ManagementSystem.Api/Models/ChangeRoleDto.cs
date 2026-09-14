namespace ManagementSystem.Api.Models
{
    public class ChangeRoleDto
    {
        /// <summary>
        /// Target role: "Lecture", "User", or "Both"
        /// </summary>
        public string TargetRole { get; set; } = null!;

        public string? BankName { get; set; }
        public string? BankNumber { get; set; }
        public string? QrCodeImage { get; set; }

        public bool ResetPasswordToDefault { get; set; } = false;
    }

    public class UserRoleSummaryDto
    {
        public string Id { get; set; } = null!;
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public List<string> CurrentRoles { get; set; } = new();
        public int UpcomingSchedulesCount { get; set; }
        public int ChildrenCount { get; set; }
        public List<string> ChildrenNames { get; set; } = new();
        public bool IsLockedOut { get; set; }
    }
}
