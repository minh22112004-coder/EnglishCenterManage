namespace ManagementSystem.Api.Models
{
    public class ClassDto
    {
        public string ClassCode { get; set; } = null!;
        public string ClassName { get; set; } = null!;
        public int TotalSessions { get; set; }
        public string? TimeSlots { get; set; }
        public List<int>? StudentIds { get; set; }
    }

    public class ClassResponseDto : ClassDto
    {
        public int Id { get; set; }
    }
}
