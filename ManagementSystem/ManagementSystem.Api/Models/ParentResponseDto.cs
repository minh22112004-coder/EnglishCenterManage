namespace ManagementSystem.Api.Models
{
    public class ParentChildItemDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = null!;
        public string? ClassName { get; set; }
        public int TotalRemainingSessions { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class ParentResponseDto
    {
        public string Id { get; set; } = null!;
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public bool IsLockedOut { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<ParentChildItemDto> Children { get; set; } = new();
    }
}
