namespace ManagementSystem.Api.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public string UserId { get; set; } = null!;
        public AppUser User { get; set; } = null!;
        public string Message { get; set; } = null!;
        public bool IsRead { get; set; }
        public bool HidePopup { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
