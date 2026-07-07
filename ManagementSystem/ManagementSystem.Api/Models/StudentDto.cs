namespace ManagementSystem.Api.Models
{
    public class StudentDto
    {
        public string FullName { get; set; } = null!;
        public DateTime DateOfBirth { get; set; }
        public string ParentName { get; set; } = null!;
        public string ParentPhoneNumber { get; set; } = null!;
        public int TotalRemainingSessions { get; set; }
        public int TotalAttendedSessions { get; set; }
        public int TotalPaidSessions { get; set; }
        public int ClassId { get; set; }
    }

    public class StudentResponseDto : StudentDto
    {
        public int Id { get; set; }
        public int? AttendanceRate { get; set; }
        public string? ClassName { get; set; }
    }
}
