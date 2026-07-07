namespace ManagementSystem.Api.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string Action { get; set; } = null!; // Insert, Update, Delete
        public string EntityName { get; set; } = null!;
        public string? OldValues { get; set; } // Dạng JSON
        public string? NewValues { get; set; } // Dạng JSON
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
