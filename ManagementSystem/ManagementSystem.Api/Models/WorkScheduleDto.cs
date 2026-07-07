namespace ManagementSystem.Api.Models
{
    public class WorkScheduleDto
    {
        public string UserId { get; set; } = null!;
        public DateTime Date { get; set; }
        public List<int> ClassIds { get; set; } = new List<int>();
        public string? AdminNotice { get; set; }
        public string? LectureNotice { get; set; }
        public bool IsTaskCompleted { get; set; } // Giữ lại tương thích ngược hoặc bỏ
        public List<ScheduleTaskDto> Tasks { get; set; } = new List<ScheduleTaskDto>();
        public string? RoomCode { get; set; }
    }

    public class ScheduleTaskDto
    {
        public int Id { get; set; }
        public string TaskName { get; set; } = null!;
        public bool IsCompleted { get; set; }
    }
}
