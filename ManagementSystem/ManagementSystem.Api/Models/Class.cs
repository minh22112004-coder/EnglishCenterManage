namespace ManagementSystem.Api.Models
{
    public class Class : ISoftDelete
    {
        public int Id { get; set; }
        public string ClassCode { get; set; } = null!;
        public string ClassName { get; set; } = null!;
        public int TotalSessions { get; set; }
        public string? TimeSlots { get; set; }

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        public ICollection<Student> Students { get; set; } = new List<Student>();
        public ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
    }
}
