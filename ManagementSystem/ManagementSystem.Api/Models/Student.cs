namespace ManagementSystem.Api.Models
{
    public class Student : ISoftDelete
    {
        public int Id { get; set; }
        public string FullName { get; set; } = null!;
        public DateTime DateOfBirth { get; set; }
        public string ParentName { get; set; } = null!;
        public string ParentPhoneNumber { get; set; } = null!;
        public int TotalRemainingSessions { get; set; }
        public int TotalAttendedSessions { get; set; }
        public int TotalPaidSessions { get; set; }
        
        public int? ClassId { get; set; }
        public Class? Class { get; set; }

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    }
}
