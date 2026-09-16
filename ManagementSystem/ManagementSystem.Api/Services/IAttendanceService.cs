using ManagementSystem.Api.Models;

namespace ManagementSystem.Api.Services
{
    public enum AttendanceBatchStatus
    {
        Success,
        BadRequest,
        Forbidden,
        ConflictDuplicate,
        ConflictConcurrency,
        InternalError
    }

    public record AttendanceBatchResult(
        AttendanceBatchStatus Status,
        string? Message = null,
        int Attempts = 1
    );

    public interface IAttendanceService
    {
        Task<AttendanceBatchResult> MarkAttendanceBatchAsync(
            List<AttendanceDto> dtos,
            bool isAdmin,
            string? userId,
            CancellationToken cancellationToken = default);

        Task<(AttendanceBatchStatus Status, string? Message, bool IsTransient)> ExecuteAttemptAsync(
            List<(int StudentId, DateOnly AttendanceDay, DateTime DateUtc, bool IsPresent, int? DtoClassId)> clientItems,
            List<int> affectedStudentIds,
            bool isAdmin,
            string? userId,
            CancellationToken cancellationToken = default);
    }
}
