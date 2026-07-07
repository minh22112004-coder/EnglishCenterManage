namespace ManagementSystem.Api.Models
{
    public class AttendanceReportDto
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string ParentName { get; set; } = string.Empty;
        public string ParentPhoneNumber { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public List<DateTime> PresentDates { get; set; } = new List<DateTime>();
        public List<DateTime> AbsentDates { get; set; } = new List<DateTime>();
    }
}
