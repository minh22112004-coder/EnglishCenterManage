using Microsoft.EntityFrameworkCore;
using Npgsql;
using ManagementSystem.Api.Common;
using ManagementSystem.Api.Data;
using ManagementSystem.Api.Models;
using ManagementSystem.Api.Services;

namespace ManagementSystem.Api.Tests
{
    public class AttendanceIntegrationTests : IClassFixture<PostgreSqlFixture>
    {
        private readonly PostgreSqlFixture _fixture;

        public AttendanceIntegrationTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        [SkippableFact]
        public async Task Test1_UniqueIndex_ConcurrentWritesSameStudentAndDay_OnlyOneSucceeds_OtherReceivesSqlState23505()
        {
            _fixture.EnsureDockerOrSkip();

            await using var setupContext = await _fixture.CreateContextAsync();
            var student = new Student
            {
                FullName = "Unique Index Test Student",
                NormalizedFullName = "UNIQUE INDEX TEST STUDENT",
                ParentName = "Parent",
                ParentPhoneNumber = "0912345678",
                ParentPhoneCanonical = "0912345678",
                TotalPaidSessions = 10,
                TotalAttendedSessions = 0,
                TotalRemainingSessions = 10
            };
            setupContext.Students.Add(student);
            await setupContext.SaveChangesAsync();

            var attendanceDay = new DateOnly(2026, 9, 16);
            var dateUtc = DateTime.SpecifyKind(attendanceDay.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

            await using var ctx1 = await _fixture.CreateContextAsync();
            await using var ctx2 = await _fixture.CreateContextAsync();

            var barrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var task1 = Task.Run(async () =>
            {
                await barrier.Task;
                await using var tx = await ctx1.Database.BeginTransactionAsync();
                ctx1.Attendances.Add(new Attendance
                {
                    StudentId = student.Id,
                    AttendanceDay = attendanceDay,
                    Date = dateUtc,
                    IsPresent = true
                });
                await ctx1.SaveChangesAsync();
                await tx.CommitAsync();
            });

            var task2 = Task.Run(async () =>
            {
                await barrier.Task;
                await using var tx = await ctx2.Database.BeginTransactionAsync();
                ctx2.Attendances.Add(new Attendance
                {
                    StudentId = student.Id,
                    AttendanceDay = attendanceDay,
                    Date = dateUtc,
                    IsPresent = true
                });
                await ctx2.SaveChangesAsync();
                await tx.CommitAsync();
            });

            barrier.SetResult(true);

            var results = await Task.WhenAll(
                task1.ContinueWith(t => t.Exception?.InnerException),
                task2.ContinueWith(t => t.Exception?.InnerException)
            );

            var exceptions = results.Where(e => e != null).ToList();
            Assert.Single(exceptions);

            var ex = exceptions.First();
            var sqlState = DbErrorHelper.GetSqlState(ex);
            Assert.Equal(DbErrorHelper.UniqueViolation, sqlState);

            await using var verifyContext = await _fixture.CreateContextAsync();
            var count = await verifyContext.Attendances.CountAsync(a => a.StudentId == student.Id && a.AttendanceDay == attendanceDay);
            Assert.Equal(1, count);
        }

        [SkippableTheory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task Test2_ConcurrentAttendanceBatch_RowLockingAndDeltaTotals_ConsistentSessions(int iteration)
        {
            _fixture.EnsureDockerOrSkip();

            await using var setupContext = await _fixture.CreateContextAsync();
            var testClass = new Class
            {
                ClassCode = $"CC_{iteration}_{Guid.NewGuid():N}".Substring(0, 20),
                ClassName = $"Concurrent Class {iteration}_{Guid.NewGuid():N}"
            };
            setupContext.Classes.Add(testClass);
            await setupContext.SaveChangesAsync();

            var student = new Student
            {
                FullName = $"Concurrent Student {iteration}",
                NormalizedFullName = $"CONCURRENT STUDENT {iteration}",
                ParentName = "Parent",
                ParentPhoneNumber = "0912345678",
                ParentPhoneCanonical = "0912345678",
                ClassId = testClass.Id,
                TotalPaidSessions = 50,
                TotalAttendedSessions = 0,
                TotalRemainingSessions = 50
            };
            setupContext.Students.Add(student);
            await setupContext.SaveChangesAsync();

            var service = _fixture.CreateAttendanceService();

            int concurrencyCount = 4;
            var tasks = new List<Task<AttendanceBatchResult>>();
            var startBarrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            for (int i = 0; i < concurrencyCount; i++)
            {
                int offset = i;
                tasks.Add(Task.Run(async () =>
                {
                    await startBarrier.Task;
                    var dtos = new List<AttendanceDto>
                    {
                        new AttendanceDto
                        {
                            StudentId = student.Id,
                            ClassId = testClass.Id,
                            Date = DateTime.SpecifyKind(new DateTime(2026, 10, 1).AddDays(offset * 2), DateTimeKind.Utc),
                            IsPresent = true
                        },
                        new AttendanceDto
                        {
                            StudentId = student.Id,
                            ClassId = testClass.Id,
                            Date = DateTime.SpecifyKind(new DateTime(2026, 10, 1).AddDays(offset * 2 + 1), DateTimeKind.Utc),
                            IsPresent = true
                        }
                    };
                    return await service.MarkAttendanceBatchAsync(dtos, isAdmin: true, userId: null);
                }));
            }

            startBarrier.SetResult(true);
            var results = await Task.WhenAll(tasks);

            foreach (var r in results)
            {
                Assert.Equal(AttendanceBatchStatus.Success, r.Status);
            }

            await using var verifyContext = await _fixture.CreateContextAsync();
            var refreshedStudent = await verifyContext.Students.FindAsync(student.Id);
            Assert.NotNull(refreshedStudent);

            int expectedAttended = concurrencyCount * 2;
            Assert.Equal(expectedAttended, refreshedStudent.TotalAttendedSessions);
            Assert.Equal(50 - expectedAttended, refreshedStudent.TotalRemainingSessions);
        }

        [SkippableFact]
        public async Task Test4_HistoricalClassId_NotOverwrittenWhenStudentTransfersClass()
        {
            _fixture.EnsureDockerOrSkip();

            await using var setupContext = await _fixture.CreateContextAsync();
            var classA = new Class
            {
                ClassCode = $"CA_{Guid.NewGuid():N}".Substring(0, 20),
                ClassName = $"Class A_{Guid.NewGuid():N}"
            };
            var classB = new Class
            {
                ClassCode = $"CB_{Guid.NewGuid():N}".Substring(0, 20),
                ClassName = $"Class B_{Guid.NewGuid():N}"
            };
            setupContext.Classes.AddRange(classA, classB);
            await setupContext.SaveChangesAsync();

            var student = new Student
            {
                FullName = "Transfer Student",
                NormalizedFullName = "TRANSFER STUDENT",
                ParentName = "Parent",
                ParentPhoneNumber = "0987654321",
                ParentPhoneCanonical = "0987654321",
                ClassId = classA.Id,
                TotalPaidSessions = 20,
                TotalAttendedSessions = 0,
                TotalRemainingSessions = 20
            };
            setupContext.Students.Add(student);
            await setupContext.SaveChangesAsync();

            var service = _fixture.CreateAttendanceService();

            // 1. Mark attendance while student is in Class A
            var day1 = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
            var result1 = await service.MarkAttendanceBatchAsync(new List<AttendanceDto>
            {
                new AttendanceDto
                {
                    StudentId = student.Id,
                    ClassId = classA.Id,
                    Date = day1,
                    IsPresent = true
                }
            }, isAdmin: true, userId: null);
            Assert.Equal(AttendanceBatchStatus.Success, result1.Status);

            // 2. Transfer student to Class B
            await using var updateContext = await _fixture.CreateContextAsync();
            var studentToUpdate = await updateContext.Students.FindAsync(student.Id);
            Assert.NotNull(studentToUpdate);
            studentToUpdate.ClassId = classB.Id;
            await updateContext.SaveChangesAsync();

            // 3. Mark attendance for Day 2 while student is in Class B
            var day2 = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc);
            var result2 = await service.MarkAttendanceBatchAsync(new List<AttendanceDto>
            {
                new AttendanceDto
                {
                    StudentId = student.Id,
                    ClassId = classB.Id,
                    Date = day2,
                    IsPresent = true
                }
            }, isAdmin: true, userId: null);
            Assert.Equal(AttendanceBatchStatus.Success, result2.Status);

            // 4. Verify historical Day 1 attendance still belongs to Class A, Day 2 belongs to Class B
            await using var verifyContext = await _fixture.CreateContextAsync();
            var day1Attendance = await verifyContext.Attendances.FirstOrDefaultAsync(a => a.StudentId == student.Id && a.AttendanceDay == DateOnly.FromDateTime(day1));
            var day2Attendance = await verifyContext.Attendances.FirstOrDefaultAsync(a => a.StudentId == student.Id && a.AttendanceDay == DateOnly.FromDateTime(day2));

            Assert.NotNull(day1Attendance);
            Assert.NotNull(day2Attendance);
            Assert.Equal(classA.Id, day1Attendance.ClassId);
            Assert.Equal(classB.Id, day2Attendance.ClassId);

            // 5. Update Day 1 attendance (toggle present/absent) with null DtoClassId - must still preserve Class A
            var result3 = await service.MarkAttendanceBatchAsync(new List<AttendanceDto>
            {
                new AttendanceDto
                {
                    StudentId = student.Id,
                    ClassId = null,
                    Date = day1,
                    IsPresent = false
                }
            }, isAdmin: true, userId: null);
            Assert.Equal(AttendanceBatchStatus.Success, result3.Status);

            await using var verifyContext2 = await _fixture.CreateContextAsync();
            var day1Updated = await verifyContext2.Attendances.FirstOrDefaultAsync(a => a.StudentId == student.Id && a.AttendanceDay == DateOnly.FromDateTime(day1));
            Assert.NotNull(day1Updated);
            Assert.Equal(classA.Id, day1Updated.ClassId);
        }

        [SkippableFact]
        public async Task Test5_DuplicateStudentDayInBatch_IsRejectedBeforeAnyWrite()
        {
            _fixture.EnsureDockerOrSkip();

            await using var setupContext = await _fixture.CreateContextAsync();
            var testClass = new Class
            {
                ClassCode = $"DUP_{Guid.NewGuid():N}"[..20],
                ClassName = $"Duplicate batch class {Guid.NewGuid():N}"
            };
            var student = new Student
            {
                FullName = $"Duplicate batch student {Guid.NewGuid():N}",
                ParentName = "Parent",
                ParentPhoneNumber = "0912345678",
                Class = testClass,
                TotalPaidSessions = 10,
                TotalRemainingSessions = 10
            };
            setupContext.Students.Add(student);
            await setupContext.SaveChangesAsync();

            var day = new DateOnly(2026, 11, 5);
            var result = await _fixture.CreateAttendanceService().MarkAttendanceBatchAsync(new List<AttendanceDto>
            {
                new() { StudentId = student.Id, ClassId = testClass.Id, Date = day.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc), IsPresent = true },
                // Different timestamps still identify the same attendance day.
                new() { StudentId = student.Id, ClassId = testClass.Id, Date = day.ToDateTime(new TimeOnly(20, 0), DateTimeKind.Utc), IsPresent = false }
            }, isAdmin: true, userId: null);

            Assert.Equal(AttendanceBatchStatus.BadRequest, result.Status);

            await using var verifyContext = await _fixture.CreateContextAsync();
            Assert.Equal(0, await verifyContext.Attendances.CountAsync(a => a.StudentId == student.Id));
            var unchanged = await verifyContext.Students.SingleAsync(s => s.Id == student.Id);
            Assert.Equal(0, unchanged.TotalAttendedSessions);
            Assert.Equal(10, unchanged.TotalRemainingSessions);
        }

        [SkippableFact]
        public async Task Test6_RepeatedPresentForSameDay_IsIdempotentAndIncrementsOnce()
        {
            _fixture.EnsureDockerOrSkip();

            await using var setupContext = await _fixture.CreateContextAsync();
            var testClass = new Class
            {
                ClassCode = $"IDEM_{Guid.NewGuid():N}"[..20],
                ClassName = $"Idempotent class {Guid.NewGuid():N}"
            };
            var student = new Student
            {
                FullName = $"Idempotent student {Guid.NewGuid():N}",
                ParentName = "Parent",
                ParentPhoneNumber = "0912345678",
                Class = testClass,
                TotalPaidSessions = 10,
                TotalRemainingSessions = 10
            };
            setupContext.Students.Add(student);
            await setupContext.SaveChangesAsync();

            var dto = new AttendanceDto
            {
                StudentId = student.Id,
                ClassId = testClass.Id,
                Date = new DateTime(2026, 11, 6, 9, 30, 0, DateTimeKind.Utc),
                IsPresent = true
            };
            var service = _fixture.CreateAttendanceService();
            Assert.Equal(AttendanceBatchStatus.Success, (await service.MarkAttendanceBatchAsync(new() { dto }, true, null)).Status);
            Assert.Equal(AttendanceBatchStatus.Success, (await service.MarkAttendanceBatchAsync(new() { dto }, true, null)).Status);

            await using var verifyContext = await _fixture.CreateContextAsync();
            Assert.Equal(1, await verifyContext.Attendances.CountAsync(a => a.StudentId == student.Id));
            var persisted = await verifyContext.Students.SingleAsync(s => s.Id == student.Id);
            Assert.Equal(1, persisted.TotalAttendedSessions);
            Assert.Equal(9, persisted.TotalRemainingSessions);
        }

        [SkippableFact]
        public async Task Test7_PresentAbsentPresent_UpdatesDeltaExactlyOnce()
        {
            _fixture.EnsureDockerOrSkip();

            await using var setupContext = await _fixture.CreateContextAsync();
            var testClass = new Class
            {
                ClassCode = $"DELTA_{Guid.NewGuid():N}"[..20],
                ClassName = $"Delta class {Guid.NewGuid():N}"
            };
            var student = new Student
            {
                FullName = $"Delta student {Guid.NewGuid():N}",
                ParentName = "Parent",
                ParentPhoneNumber = "0912345678",
                Class = testClass,
                TotalPaidSessions = 10,
                TotalRemainingSessions = 10
            };
            setupContext.Students.Add(student);
            await setupContext.SaveChangesAsync();

            var service = _fixture.CreateAttendanceService();
            var day = new DateTime(2026, 11, 7, 11, 0, 0, DateTimeKind.Utc);
            async Task<AttendanceBatchResult> Mark(bool present) => await service.MarkAttendanceBatchAsync(new List<AttendanceDto>
            {
                new() { StudentId = student.Id, ClassId = testClass.Id, Date = day, IsPresent = present }
            }, true, null);

            Assert.Equal(AttendanceBatchStatus.Success, (await Mark(true)).Status);
            Assert.Equal(AttendanceBatchStatus.Success, (await Mark(false)).Status);
            Assert.Equal(AttendanceBatchStatus.Success, (await Mark(true)).Status);

            await using var verifyContext = await _fixture.CreateContextAsync();
            var attendance = await verifyContext.Attendances.SingleAsync(a => a.StudentId == student.Id);
            Assert.True(attendance.IsPresent);
            var persisted = await verifyContext.Students.SingleAsync(s => s.Id == student.Id);
            Assert.Equal(1, persisted.TotalAttendedSessions);
            Assert.Equal(9, persisted.TotalRemainingSessions);
        }

        [SkippableFact]
        public async Task Test8_InvalidStudentInBatch_RollsBackValidItems()
        {
            _fixture.EnsureDockerOrSkip();

            await using var setupContext = await _fixture.CreateContextAsync();
            var testClass = new Class
            {
                ClassCode = $"ROLL_{Guid.NewGuid():N}"[..20],
                ClassName = $"Rollback class {Guid.NewGuid():N}"
            };
            var student = new Student
            {
                FullName = $"Rollback student {Guid.NewGuid():N}",
                ParentName = "Parent",
                ParentPhoneNumber = "0912345678",
                Class = testClass,
                TotalPaidSessions = 10,
                TotalRemainingSessions = 10
            };
            setupContext.Students.Add(student);
            await setupContext.SaveChangesAsync();

            var result = await _fixture.CreateAttendanceService().MarkAttendanceBatchAsync(new List<AttendanceDto>
            {
                new() { StudentId = student.Id, ClassId = testClass.Id, Date = new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc), IsPresent = true },
                new() { StudentId = int.MaxValue, ClassId = testClass.Id, Date = new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc), IsPresent = true }
            }, true, null);

            Assert.Equal(AttendanceBatchStatus.Forbidden, result.Status);
            await using var verifyContext = await _fixture.CreateContextAsync();
            Assert.Equal(0, await verifyContext.Attendances.CountAsync(a => a.StudentId == student.Id));
            var unchanged = await verifyContext.Students.SingleAsync(s => s.Id == student.Id);
            Assert.Equal(0, unchanged.TotalAttendedSessions);
            Assert.Equal(10, unchanged.TotalRemainingSessions);
        }

        [SkippableFact]
        public async Task Test9_ConcurrentSameStudentDay_CreatesOneRowAndOneIncrement()
        {
            _fixture.EnsureDockerOrSkip();

            await using var setupContext = await _fixture.CreateContextAsync();
            var testClass = new Class
            {
                ClassCode = $"RACE_{Guid.NewGuid():N}"[..20],
                ClassName = $"Race class {Guid.NewGuid():N}"
            };
            var student = new Student
            {
                FullName = $"Race student {Guid.NewGuid():N}",
                ParentName = "Parent",
                ParentPhoneNumber = "0912345678",
                Class = testClass,
                TotalPaidSessions = 10,
                TotalRemainingSessions = 10
            };
            setupContext.Students.Add(student);
            await setupContext.SaveChangesAsync();

            var service = _fixture.CreateAttendanceService();
            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var dto = new AttendanceDto
            {
                StudentId = student.Id,
                ClassId = testClass.Id,
                Date = new DateTime(2026, 11, 9, 14, 0, 0, DateTimeKind.Utc),
                IsPresent = true
            };
            var first = Task.Run(async () => { await start.Task; return await service.MarkAttendanceBatchAsync(new() { dto }, true, null); });
            var second = Task.Run(async () => { await start.Task; return await service.MarkAttendanceBatchAsync(new() { dto }, true, null); });
            start.SetResult(true);
            var results = await Task.WhenAll(first, second);

            Assert.All(results, result => Assert.Equal(AttendanceBatchStatus.Success, result.Status));
            await using var verifyContext = await _fixture.CreateContextAsync();
            Assert.Equal(1, await verifyContext.Attendances.CountAsync(a => a.StudentId == student.Id));
            var persisted = await verifyContext.Students.SingleAsync(s => s.Id == student.Id);
            Assert.Equal(1, persisted.TotalAttendedSessions);
        }

        [SkippableFact]
        public async Task Test10_LectureMustHaveClassScheduleForAttendance()
        {
            _fixture.EnsureDockerOrSkip();

            await using var setupContext = await _fixture.CreateContextAsync();
            var testClass = new Class
            {
                ClassCode = $"AUTH_{Guid.NewGuid():N}"[..20],
                ClassName = $"Authorization class {Guid.NewGuid():N}"
            };
            var student = new Student
            {
                FullName = $"Authorization student {Guid.NewGuid():N}",
                ParentName = "Parent",
                ParentPhoneNumber = "0912345678",
                Class = testClass,
                TotalPaidSessions = 10,
                TotalRemainingSessions = 10
            };
            var lecture = new AppUser
            {
                Id = Guid.NewGuid().ToString("N"),
                UserName = $"lecture-{Guid.NewGuid():N}",
                NormalizedUserName = "LECTURE-" + Guid.NewGuid().ToString("N")
            };
            var day = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);
            var schedule = new WorkSchedule
            {
                User = lecture,
                Date = day,
                Classes = new List<Class> { testClass }
            };
            setupContext.Students.Add(student);
            setupContext.WorkSchedules.Add(schedule);
            await setupContext.SaveChangesAsync();

            var service = _fixture.CreateAttendanceService();
            var dto = new AttendanceDto { StudentId = student.Id, ClassId = testClass.Id, Date = day, IsPresent = true };
            var assigned = await service.MarkAttendanceBatchAsync(new() { dto }, isAdmin: false, userId: lecture.Id);
            Assert.Equal(AttendanceBatchStatus.Success, assigned.Status);

            var unauthorized = await service.MarkAttendanceBatchAsync(new()
            {
                new() { StudentId = student.Id, ClassId = testClass.Id, Date = day.AddDays(1), IsPresent = true }
            }, isAdmin: false, userId: Guid.NewGuid().ToString("N"));
            Assert.Equal(AttendanceBatchStatus.Forbidden, unauthorized.Status);

            await using var verifyContext = await _fixture.CreateContextAsync();
            Assert.Equal(1, await verifyContext.Attendances.CountAsync(a => a.StudentId == student.Id));
            var persisted = await verifyContext.Students.SingleAsync(s => s.Id == student.Id);
            Assert.Equal(1, persisted.TotalAttendedSessions);
        }

        [SkippableFact]
        public async Task Test11_WrappedUniqueViolation_IsMappedToConflictDuplicate()
        {
            _fixture.EnsureDockerOrSkip();

            await using var setupContext = await _fixture.CreateContextAsync();
            var testClass = new Class
            {
                ClassCode = $"ERR_{Guid.NewGuid():N}"[..20],
                ClassName = $"Error mapping class {Guid.NewGuid():N}"
            };
            var student = new Student
            {
                FullName = $"Error mapping student {Guid.NewGuid():N}",
                ParentName = "Parent",
                ParentPhoneNumber = "0912345678",
                Class = testClass,
                TotalPaidSessions = 10,
                TotalRemainingSessions = 10
            };
            setupContext.Students.Add(student);
            await setupContext.SaveChangesAsync();

            var service = new DuplicateSaveAttendanceService(_fixture.ContextFactory!);
            var result = await service.MarkAttendanceBatchAsync(new()
            {
                new() { StudentId = student.Id, ClassId = testClass.Id, Date = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc), IsPresent = true }
            }, true, null);

            Assert.Equal(AttendanceBatchStatus.ConflictDuplicate, result.Status);
            Assert.Equal(1, result.Attempts);
            await using var verifyContext = await _fixture.CreateContextAsync();
            Assert.Equal(0, await verifyContext.Attendances.CountAsync(a => a.StudentId == student.Id));
            var unchanged = await verifyContext.Students.SingleAsync(s => s.Id == student.Id);
            Assert.Equal(0, unchanged.TotalAttendedSessions);
        }

        private sealed class DuplicateSaveAttendanceService : AttendanceService
        {
            public DuplicateSaveAttendanceService(IDbContextFactory<AppDbContext> factory) : base(factory) { }

            protected override Task<int> SaveChangesAsync(AppDbContext context, CancellationToken cancellationToken)
                => throw new DbUpdateException(
                    "Synthetic unique violation",
                    new PostgresException("duplicate key", "ERROR", "ERROR", DbErrorHelper.UniqueViolation));
        }
    }
}
