using Microsoft.EntityFrameworkCore;
using ManagementSystem.Api.Common;
using ManagementSystem.Api.Data;
using ManagementSystem.Api.Models;
using ManagementSystem.Api.Services;

namespace ManagementSystem.Api.Tests
{
    public class AttendanceRetryTests
    {
        private class TestDbContextFactory : IDbContextFactory<AppDbContext>
        {
            public int CreateCount { get; private set; }
            public List<AppDbContext> CreatedContexts { get; } = new();
            private readonly DbContextOptions<AppDbContext> _options;

            public TestDbContextFactory()
            {
                _options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseNpgsql("Host=localhost;Database=dummy")
                    .Options;
            }

            public AppDbContext CreateDbContext()
            {
                CreateCount++;
                var context = new AppDbContext(_options, null!);
                CreatedContexts.Add(context);
                return context;
            }

            public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            {
                CreateCount++;
                var context = new AppDbContext(_options, null!);
                CreatedContexts.Add(context);
                return Task.FromResult(context);
            }
        }

        private class MockAttemptAttendanceService : AttendanceService
        {
            private readonly Queue<(AttendanceBatchStatus Status, string? Message, bool IsTransient)> _scriptedAttempts;
            public List<int> AttemptIndices { get; } = new();

            public MockAttemptAttendanceService(
                IDbContextFactory<AppDbContext> factory,
                IEnumerable<(AttendanceBatchStatus Status, string? Message, bool IsTransient)> scriptedAttempts)
                : base(factory)
            {
                _scriptedAttempts = new Queue<(AttendanceBatchStatus, string?, bool)>(scriptedAttempts);
            }

            public override async Task<(AttendanceBatchStatus Status, string? Message, bool IsTransient)> ExecuteAttemptAsync(
                List<(int StudentId, DateOnly AttendanceDay, DateTime DateUtc, bool IsPresent, int? DtoClassId)> clientItems,
                List<int> affectedStudentIds,
                bool isAdmin,
                string? userId,
                CancellationToken cancellationToken = default)
            {
                // Exercise the same production context-creation seam used by the
                // real attempt. This makes the retry test fail if a retry reuses
                // a context/change tracker after a rollback.
                await using var context = await CreateDbContextAsync(cancellationToken);
                AttemptIndices.Add(AttemptIndices.Count + 1);
                if (_scriptedAttempts.Count > 0)
                {
                    return _scriptedAttempts.Dequeue();
                }
                return (AttendanceBatchStatus.Success, (string?)"Success", false);
            }
        }

        [Fact]
        public async Task Test3_RetryPath_TransientConcurrencyError_RetriesOnFreshDbContextAndSucceeds()
        {
            var factory = new TestDbContextFactory();
            // Attempt 1: Transient concurrency failure (40001/40P01)
            // Attempt 2: Success
            var service = new MockAttemptAttendanceService(factory, new[]
            {
                (AttendanceBatchStatus.ConflictConcurrency, (string?)null, true),
                (AttendanceBatchStatus.Success, (string?)"Lưu điểm danh thành công.", false)
            });

            var dtos = new List<AttendanceDto>
            {
                new AttendanceDto
                {
                    StudentId = 1,
                    ClassId = 10,
                    Date = new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc),
                    IsPresent = true
                }
            };

            var result = await service.MarkAttendanceBatchAsync(dtos, isAdmin: true, userId: null);

            Assert.Equal(AttendanceBatchStatus.Success, result.Status);
            Assert.Equal(2, result.Attempts);
            Assert.Equal(2, service.AttemptIndices.Count);
            Assert.Equal(2, factory.CreateCount);
            Assert.Equal(2, factory.CreatedContexts.Distinct().Count());
        }

        [Fact]
        public async Task Test3_RetryPath_TransientConcurrencyExhausted_ReturnsConflictConcurrency()
        {
            var factory = new TestDbContextFactory();
            // All attempts return transient concurrency failure
            var service = new MockAttemptAttendanceService(factory, new[]
            {
                (AttendanceBatchStatus.ConflictConcurrency, (string?)null, true),
                (AttendanceBatchStatus.ConflictConcurrency, (string?)null, true),
                (AttendanceBatchStatus.ConflictConcurrency, (string?)null, true),
                (AttendanceBatchStatus.ConflictConcurrency, (string?)null, true)
            });

            var dtos = new List<AttendanceDto>
            {
                new AttendanceDto
                {
                    StudentId = 1,
                    ClassId = 10,
                    Date = new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc),
                    IsPresent = true
                }
            };

            var result = await service.MarkAttendanceBatchAsync(dtos, isAdmin: true, userId: null);

            Assert.Equal(AttendanceBatchStatus.ConflictConcurrency, result.Status);
            Assert.Equal(4, result.Attempts); // Initial + 3 retries = 4 attempts total
            Assert.Equal(4, service.AttemptIndices.Count);
            Assert.Equal(4, factory.CreateCount);
            Assert.Equal(4, factory.CreatedContexts.Distinct().Count());
        }

        [Fact]
        public async Task Test3_NonTransientError_DoesNotRetry_ReturnsImmediately()
        {
            var factory = new TestDbContextFactory();
            // Unique violation (23505) is NOT transient -> must NOT retry
            var service = new MockAttemptAttendanceService(factory, new[]
            {
                (AttendanceBatchStatus.ConflictDuplicate, (string?)"Trùng lặp điểm danh", false)
            });

            var dtos = new List<AttendanceDto>
            {
                new AttendanceDto
                {
                    StudentId = 1,
                    ClassId = 10,
                    Date = new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc),
                    IsPresent = true
                }
            };

            var result = await service.MarkAttendanceBatchAsync(dtos, isAdmin: true, userId: null);

            Assert.Equal(AttendanceBatchStatus.ConflictDuplicate, result.Status);
            Assert.Equal(1, result.Attempts); // Exactly 1 attempt
            Assert.Single(service.AttemptIndices);
            Assert.Equal(1, factory.CreateCount);
        }
    }
}
