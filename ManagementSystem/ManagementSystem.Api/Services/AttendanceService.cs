using Microsoft.EntityFrameworkCore;
using ManagementSystem.Api.Common;
using ManagementSystem.Api.Data;
using ManagementSystem.Api.Models;

namespace ManagementSystem.Api.Services
{
    public class AttendanceService : IAttendanceService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public AttendanceService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // Keep the attempt boundary testable: every retry must obtain and dispose a
        // completely new context, and persistence can be fault-injected in tests.
        protected virtual Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken)
            => _contextFactory.CreateDbContextAsync(cancellationToken);

        protected virtual Task<int> SaveChangesAsync(AppDbContext context, CancellationToken cancellationToken)
            => context.SaveChangesAsync(cancellationToken);

        public async Task<AttendanceBatchResult> MarkAttendanceBatchAsync(
            List<AttendanceDto> dtos,
            bool isAdmin,
            string? userId,
            CancellationToken cancellationToken = default)
        {
            if (dtos == null || !dtos.Any())
            {
                return new AttendanceBatchResult(AttendanceBatchStatus.BadRequest, "Không có dữ liệu điểm danh.");
            }

            if (!isAdmin && string.IsNullOrEmpty(userId))
            {
                return new AttendanceBatchResult(AttendanceBatchStatus.Forbidden);
            }

            // Step 1: Preflight validation - Convert to DateOnly and reject duplicate (StudentId, AttendanceDay)
            var clientItems = new List<(int StudentId, DateOnly AttendanceDay, DateTime DateUtc, bool IsPresent, int? DtoClassId)>();
            var seenStudentDays = new HashSet<(int StudentId, DateOnly Day)>();

            foreach (var dto in dtos)
            {
                var day = DateOnly.FromDateTime(dto.Date);
                var dateUtc = DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

                if (!seenStudentDays.Add((dto.StudentId, day)))
                {
                    return new AttendanceBatchResult(
                        AttendanceBatchStatus.BadRequest,
                        $"Dữ liệu điểm danh chứa bản ghi trùng lặp cho học sinh ID {dto.StudentId} vào ngày {day:yyyy-MM-dd}.");
                }

                clientItems.Add((dto.StudentId, day, dateUtc, dto.IsPresent, dto.ClassId));
            }

            var affectedStudentIds = clientItems.Select(i => i.StudentId).Distinct().OrderBy(id => id).ToList();

            const int maxRetries = 3;
            int retryCount = 0;

            while (true)
            {
                var (status, message, isTransient) = await ExecuteAttemptAsync(clientItems, affectedStudentIds, isAdmin, userId, cancellationToken);
                if (!isTransient)
                {
                    return new AttendanceBatchResult(status, message, retryCount + 1);
                }

                if (retryCount >= maxRetries)
                {
                    return new AttendanceBatchResult(
                        AttendanceBatchStatus.ConflictConcurrency,
                        "Xung đột đồng thời khi lưu điểm danh, vui lòng thử lại.",
                        retryCount + 1);
                }

                retryCount++;
                await Task.Delay(50 * retryCount, cancellationToken);
            }
        }

        public virtual async Task<(AttendanceBatchStatus Status, string? Message, bool IsTransient)> ExecuteAttemptAsync(
            List<(int StudentId, DateOnly AttendanceDay, DateTime DateUtc, bool IsPresent, int? DtoClassId)> clientItems,
            List<int> affectedStudentIds,
            bool isAdmin,
            string? userId,
            CancellationToken cancellationToken = default)
        {
            await using var context = await CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
            try
            {
                // Step 2: Lock active Student rows in ascending ID order to prevent concurrent inflation and deadlocks
                var students = await context.Students
                    .FromSqlRaw("SELECT * FROM \"Students\" WHERE \"Id\" = ANY({0}) AND \"IsDeleted\" = FALSE ORDER BY \"Id\" FOR UPDATE", affectedStudentIds)
                    .ToListAsync(cancellationToken);

                var studentMap = students.ToDictionary(s => s.Id);
                if (studentMap.Count != affectedStudentIds.Count)
                {
                    try { await transaction.RollbackAsync(cancellationToken); } catch { }
                    return (AttendanceBatchStatus.Forbidden, null, false);
                }

                // Step 3: Bulk load existing attendances by (StudentId, AttendanceDay)
                var distinctDays = clientItems.Select(i => i.AttendanceDay).Distinct().ToList();
                var existingAttendances = await context.Attendances
                    .Where(a => affectedStudentIds.Contains(a.StudentId) && distinctDays.Contains(a.AttendanceDay))
                    .ToListAsync(cancellationToken);

                var attendanceDict = existingAttendances.ToDictionary(a => (a.StudentId, a.AttendanceDay));

                // Step 4: Authorize class and schedule assignment in bulk
                var validatedItems = new List<(Student Student, Attendance? ExistingAttendance, int AuthoritativeClassId, DateOnly Day, DateTime DateUtc, bool IsPresent)>();
                var requiredSchedules = new HashSet<(int ClassId, DateOnly Day)>();

                foreach (var item in clientItems)
                {
                    var student = studentMap[item.StudentId];
                    attendanceDict.TryGetValue((item.StudentId, item.AttendanceDay), out var existing);

                    int authoritativeClassId;
                    if (existing != null)
                    {
                        if (existing.ClassId.HasValue && existing.ClassId.Value > 0)
                        {
                            authoritativeClassId = existing.ClassId.Value;
                        }
                        else
                        {
                            if (!student.ClassId.HasValue || student.ClassId.Value <= 0)
                            {
                                try { await transaction.RollbackAsync(cancellationToken); } catch { }
                                return (AttendanceBatchStatus.Forbidden, null, false);
                            }
                            authoritativeClassId = student.ClassId.Value;
                        }
                    }
                    else
                    {
                        if (!student.ClassId.HasValue || student.ClassId.Value <= 0)
                        {
                            try { await transaction.RollbackAsync(cancellationToken); } catch { }
                            return (AttendanceBatchStatus.Forbidden, null, false);
                        }
                        authoritativeClassId = student.ClassId.Value;
                    }

                    if (item.DtoClassId.HasValue && item.DtoClassId.Value > 0 && item.DtoClassId.Value != authoritativeClassId)
                    {
                        try { await transaction.RollbackAsync(cancellationToken); } catch { }
                        return (AttendanceBatchStatus.Forbidden, null, false);
                    }

                    if (!isAdmin)
                    {
                        requiredSchedules.Add((authoritativeClassId, item.AttendanceDay));
                    }

                    validatedItems.Add((student, existing, authoritativeClassId, item.AttendanceDay, item.DateUtc, item.IsPresent));
                }

                if (!isAdmin && requiredSchedules.Count > 0)
                {
                    var requiredClassIds = requiredSchedules.Select(s => s.ClassId).Distinct().ToList();
                    var minDate = requiredSchedules.Min(s => s.Day).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                    var maxDate = requiredSchedules.Max(s => s.Day).AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

                    var schedules = await context.WorkSchedules
                        .Where(w => w.UserId == userId && w.Date >= minDate && w.Date < maxDate)
                        .SelectMany(w => w.Classes.Where(c => requiredClassIds.Contains(c.Id)).Select(c => new { ClassId = c.Id, w.Date }))
                        .ToListAsync(cancellationToken);

                    var scheduledPairs = new HashSet<(int ClassId, DateOnly Day)>(
                        schedules.Select(s => (s.ClassId, DateOnly.FromDateTime(s.Date)))
                    );

                    foreach (var req in requiredSchedules)
                    {
                        if (!scheduledPairs.Contains(req))
                        {
                            try { await transaction.RollbackAsync(cancellationToken); } catch { }
                            return (AttendanceBatchStatus.Forbidden, null, false);
                        }
                    }
                }

                // Step 5: Aggregate present/absent deltas per student
                var deltaMap = new Dictionary<int, int>();
                foreach (var sId in affectedStudentIds)
                {
                    deltaMap[sId] = 0;
                }

                foreach (var v in validatedItems)
                {
                    if (v.ExistingAttendance != null)
                    {
                        if (!v.ExistingAttendance.ClassId.HasValue || v.ExistingAttendance.ClassId.Value <= 0)
                        {
                            v.ExistingAttendance.ClassId = v.AuthoritativeClassId;
                        }

                        if (v.ExistingAttendance.IsPresent != v.IsPresent)
                        {
                            deltaMap[v.Student.Id] += v.IsPresent ? 1 : -1;
                            v.ExistingAttendance.IsPresent = v.IsPresent;
                        }
                    }
                    else
                    {
                        var attendance = new Attendance
                        {
                            StudentId = v.Student.Id,
                            ClassId = v.AuthoritativeClassId,
                            Date = v.DateUtc,
                            AttendanceDay = v.Day,
                            IsPresent = v.IsPresent
                        };
                        context.Attendances.Add(attendance);

                        if (v.IsPresent)
                        {
                            deltaMap[v.Student.Id] += 1;
                        }
                    }
                }

                // Mutate each student's totals once
                foreach (var kvp in deltaMap)
                {
                    var student = studentMap[kvp.Key];
                    student.TotalAttendedSessions += kvp.Value;
                    student.TotalRemainingSessions = student.TotalPaidSessions - student.TotalAttendedSessions;
                }

                // Step 6: Notifications for low remaining sessions
                var lowStudents = students.Where(s => s.TotalRemainingSessions < 3 && !string.IsNullOrWhiteSpace(s.ParentPhoneCanonical)).ToList();
                if (lowStudents.Count > 0)
                {
                    var parentPhones = lowStudents.Select(s => s.ParentPhoneCanonical).Distinct().ToList();
                    var parentUsers = await context.Users
                        .Where(u => u.CanonicalPhone != null && parentPhones.Contains(u.CanonicalPhone))
                        .ToListAsync(cancellationToken);
                    var parentUserMap = parentUsers
                        .GroupBy(u => u.CanonicalPhone!, StringComparer.Ordinal)
                        .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

                    var parentUserIds = parentUsers.Select(u => u.Id).ToList();
                    var unreadNotifications = await context.Notifications
                        .Where(n => parentUserIds.Contains(n.UserId) && !n.IsRead)
                        .ToListAsync(cancellationToken);

                    foreach (var student in lowStudents)
                    {
                        if (parentUserMap.TryGetValue(student.ParentPhoneCanonical, out var parentUser))
                        {
                            bool alreadyNotified = unreadNotifications.Any(n => n.UserId == parentUser.Id && n.Message.Contains(student.FullName));
                            if (!alreadyNotified)
                            {
                                string message = student.TotalRemainingSessions < 0
                                    ? $"Học sinh {student.FullName} đã học lố {-student.TotalRemainingSessions} buổi. Quý phụ huynh vui lòng hoàn thành đóng học phí."
                                    : $"Học sinh {student.FullName} chỉ còn {student.TotalRemainingSessions} buổi học. Quý phụ huynh vui lòng đóng thêm học phí để không gián đoạn việc học.";

                                context.Notifications.Add(new Notification
                                {
                                    UserId = parentUser.Id,
                                    Message = message,
                                    IsRead = false,
                                    HidePopup = false,
                                    CreatedAt = DateTime.UtcNow
                                });
                            }
                        }
                    }
                }

                await SaveChangesAsync(context, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return (AttendanceBatchStatus.Success, "Lưu điểm danh thành công.", false);
            }
            catch (Exception ex) when (DbErrorHelper.IsUniqueViolation(ex))
            {
                try { await transaction.RollbackAsync(cancellationToken); } catch { }
                return (AttendanceBatchStatus.ConflictDuplicate, "Điểm danh bị xung đột khóa duy nhất (đã tồn tại bản ghi điểm danh cho học sinh trong ngày này).", false);
            }
            catch (Exception ex) when (DbErrorHelper.IsTransientConcurrencyError(ex))
            {
                try { await transaction.RollbackAsync(cancellationToken); } catch { }
                return (AttendanceBatchStatus.ConflictConcurrency, null, true);
            }
            catch (Exception)
            {
                try { await transaction.RollbackAsync(cancellationToken); } catch { }
                return (AttendanceBatchStatus.InternalError, "Đã xảy ra lỗi khi lưu điểm danh.", false);
            }
        }
    }
}
