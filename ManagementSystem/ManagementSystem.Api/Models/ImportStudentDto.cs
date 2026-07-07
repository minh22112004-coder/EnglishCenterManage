namespace ManagementSystem.Api.Models
{
    public class ImportStudentDto
    {
        public string FullName { get; set; } = string.Empty;
        public string ParentName { get; set; } = string.Empty;
        public string ParentPhoneNumber { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public int TotalAttendedSessions { get; set; }
        public int TotalPaidSessions { get; set; }
        public int TotalRemainingSessions { get; set; }
        public List<DateTime> AttendedDates { get; set; } = new();
    }
}
