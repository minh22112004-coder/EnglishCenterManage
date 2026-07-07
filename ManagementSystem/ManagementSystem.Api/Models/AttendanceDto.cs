namespace ManagementSystem.Api.Models
{
    public class AttendanceDto
    {
        public int StudentId { get; set; }
        public DateTime Date { get; set; }
        public bool IsPresent { get; set; }
    }
}
