namespace ManagementSystem.Api.Models
{
    public class LectureDto
    {
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? BankNumber { get; set; }
        public string? BankName { get; set; }
        public string? QrCodeImage { get; set; }
    }

    public class LectureResponseDto : LectureDto
    {
        public string Id { get; set; } = null!;
        public string? Email { get; set; }
        public object? TodaySchedules { get; set; }
    }
}
