using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ManagementSystem.Api.Controllers;
using ManagementSystem.Api.Models;
using ManagementSystem.Api.Services;

namespace ManagementSystem.Api.Tests;

public class AttendanceControllerTests
{
    [Fact]
    public async Task MarkAttendanceBatch_WrappedUniqueViolationStatus_IsReturnedAsHttp409()
    {
        var service = new StubAttendanceService(new AttendanceBatchResult(
            AttendanceBatchStatus.ConflictDuplicate,
            "duplicate",
            Attempts: 1));
        var controller = new AttendanceController(null!, null!, service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Role, "Admin"),
                        new Claim(ClaimTypes.NameIdentifier, "admin-test")
                    }))
                }
            }
        };

        var response = await controller.MarkAttendanceBatch(new List<AttendanceDto>
        {
            new() { StudentId = 1, Date = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc), IsPresent = true }
        });

        var conflict = Assert.IsType<ConflictObjectResult>(response);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    private sealed class StubAttendanceService : IAttendanceService
    {
        private readonly AttendanceBatchResult _result;

        public StubAttendanceService(AttendanceBatchResult result) => _result = result;

        public Task<AttendanceBatchResult> MarkAttendanceBatchAsync(
            List<AttendanceDto> dtos,
            bool isAdmin,
            string? userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_result);

        public Task<(AttendanceBatchStatus Status, string? Message, bool IsTransient)> ExecuteAttemptAsync(
            List<(int StudentId, DateOnly AttendanceDay, DateTime DateUtc, bool IsPresent, int? DtoClassId)> clientItems,
            List<int> affectedStudentIds,
            bool isAdmin,
            string? userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult((_result.Status, _result.Message, false));
    }
}
