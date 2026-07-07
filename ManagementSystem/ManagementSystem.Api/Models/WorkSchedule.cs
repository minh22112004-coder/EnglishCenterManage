namespace ManagementSystem.Api.Models
{
    public class WorkSchedule
    {
        public int Id { get; set; }
        public string UserId { get; set; } = null!;
        public DateTime Date { get; set; }
        public bool IsTaskCompleted { get; set; }
        public string? AdminNotice { get; set; }
        public string? LectureNotice { get; set; }

        public AppUser User { get; set; } = null!;
        public ICollection<Class> Classes { get; set; } = new List<Class>();
        public ICollection<ScheduleTask> Tasks { get; set; } = new List<ScheduleTask>();
        public string? RoomCode { get; set; } // Mã phòng học
    }
}
