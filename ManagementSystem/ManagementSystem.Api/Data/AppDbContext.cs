using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ManagementSystem.Api.Models;
using System.Text.Json;
using System.Security.Claims;
using ManagementSystem.Api.Constants;

using ManagementSystem.Api.Common;

namespace ManagementSystem.Api.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor httpContextAccessor) : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<WorkSchedule> WorkSchedules { get; set; } = null!;
        public DbSet<Class> Classes { get; set; } = null!;
        public DbSet<Student> Students { get; set; } = null!;
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<ScheduleTask> ScheduleTasks { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            
            // Global Query Filters cho Soft Delete
            builder.Entity<Class>().HasQueryFilter(c => !c.IsDeleted);
            builder.Entity<Student>().HasQueryFilter(s => !s.IsDeleted);
            builder.Entity<Attendance>().HasQueryFilter(a => !a.Student.IsDeleted);

            // Mối quan hệ và DeleteBehavior an toàn (SC-01, SC-03, SC-10)
            builder.Entity<Student>()
                .HasOne(s => s.Class)
                .WithMany(c => c.Students)
                .HasForeignKey(s => s.ClassId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Attendance>()
                .HasOne(a => a.Student)
                .WithMany(s => s.Attendances)
                .HasForeignKey(a => a.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Attendance>()
                .HasOne(a => a.Class)
                .WithMany()
                .HasForeignKey(a => a.ClassId)
                .OnDelete(DeleteBehavior.SetNull);

            // AppUser canonical phone and constraints
            builder.Entity<AppUser>(entity =>
            {
                entity.Property(u => u.CanonicalPhone)
                    .HasComputedColumnSql(@"COALESCE(
                        CASE
                            WHEN ""PhoneNumber"" IS NOT NULL AND btrim(""PhoneNumber"") ~ '^\+?[0-9 .()\-]+$' THEN
                                CASE
                                    WHEN regexp_replace(regexp_replace(btrim(""PhoneNumber""), '^\+', ''), '[ .()\-]', '', 'g') ~ '^0[0-9]{9}$'
                                        THEN regexp_replace(regexp_replace(btrim(""PhoneNumber""), '^\+', ''), '[ .()\-]', '', 'g')
                                    WHEN regexp_replace(regexp_replace(btrim(""PhoneNumber""), '^\+', ''), '[ .()\-]', '', 'g') ~ '^84[0-9]{9}$'
                                        THEN '0' || substr(regexp_replace(regexp_replace(btrim(""PhoneNumber""), '^\+', ''), '[ .()\-]', '', 'g'), 3)
                                    ELSE NULL
                                END
                            ELSE NULL
                        END,
                        CASE
                            WHEN ""UserName"" IS NOT NULL AND btrim(""UserName"") ~ '^\+?[0-9 .()\-]+$' THEN
                                CASE
                                    WHEN regexp_replace(regexp_replace(btrim(""UserName""), '^\+', ''), '[ .()\-]', '', 'g') ~ '^0[0-9]{9}$'
                                        THEN regexp_replace(regexp_replace(btrim(""UserName""), '^\+', ''), '[ .()\-]', '', 'g')
                                    WHEN regexp_replace(regexp_replace(btrim(""UserName""), '^\+', ''), '[ .()\-]', '', 'g') ~ '^84[0-9]{9}$'
                                        THEN '0' || substr(regexp_replace(regexp_replace(btrim(""UserName""), '^\+', ''), '[ .()\-]', '', 'g'), 3)
                                    ELSE NULL
                                END
                            ELSE NULL
                        END
                    )", stored: true);

                entity.HasIndex(u => u.CanonicalPhone)
                    .IsUnique()
                    .HasDatabaseName("UX_AspNetUsers_CanonicalPhone")
                    .HasFilter("\"CanonicalPhone\" IS NOT NULL");

                entity.ToTable("AspNetUsers", t =>
                {
                    t.HasCheckConstraint("CK_AspNetUsers_PhoneNumber_Canonical", "(\"PhoneNumber\" IS NULL OR btrim(\"PhoneNumber\") = '' OR \"CanonicalPhone\" IS NOT NULL)");
                    t.HasCheckConstraint("CK_AspNetUsers_PhoneAgreement", @"(
                        ""UserName"" IS NULL
                        OR ""PhoneNumber"" IS NULL
                        OR btrim(""UserName"") = ''
                        OR btrim(""PhoneNumber"") = ''
                        OR NOT (""UserName"" ~ '^\+?[0-9 .()\-]+$' AND regexp_replace(regexp_replace(btrim(""UserName""), '^\+', ''), '[ .()\-]', '', 'g') ~ '^(0|84)[0-9]{9}$')
                        OR NOT (""PhoneNumber"" ~ '^\+?[0-9 .()\-]+$' AND regexp_replace(regexp_replace(btrim(""PhoneNumber""), '^\+', ''), '[ .()\-]', '', 'g') ~ '^(0|84)[0-9]{9}$')
                        OR (
                            CASE
                                WHEN regexp_replace(regexp_replace(btrim(""UserName""), '^\+', ''), '[ .()\-]', '', 'g') ~ '^0[0-9]{9}$'
                                    THEN regexp_replace(regexp_replace(btrim(""UserName""), '^\+', ''), '[ .()\-]', '', 'g')
                                ELSE '0' || substr(regexp_replace(regexp_replace(btrim(""UserName""), '^\+', ''), '[ .()\-]', '', 'g'), 3)
                            END
                            =
                            CASE
                                WHEN regexp_replace(regexp_replace(btrim(""PhoneNumber""), '^\+', ''), '[ .()\-]', '', 'g') ~ '^0[0-9]{9}$'
                                    THEN regexp_replace(regexp_replace(btrim(""PhoneNumber""), '^\+', ''), '[ .()\-]', '', 'g')
                                ELSE '0' || substr(regexp_replace(regexp_replace(btrim(""PhoneNumber""), '^\+', ''), '[ .()\-]', '', 'g'), 3)
                            END
                        )
                    )");
                });
            });

            // Student canonical phone, normalized name, and constraints
            builder.Entity<Student>(entity =>
            {
                entity.Property(s => s.NormalizedFullName)
                    .HasComputedColumnSql("lower(btrim(\"FullName\"))", stored: true);

                entity.Property(s => s.ParentPhoneCanonical)
                    .HasComputedColumnSql(@"CASE
                        WHEN ""ParentPhoneNumber"" IS NULL OR btrim(""ParentPhoneNumber"") = '' THEN ''
                        WHEN btrim(""ParentPhoneNumber"") ~ '^\+?[0-9 .()\-]+$' THEN
                            CASE
                                WHEN regexp_replace(regexp_replace(btrim(""ParentPhoneNumber""), '^\+', ''), '[ .()\-]', '', 'g') ~ '^0[0-9]{9}$'
                                    THEN regexp_replace(regexp_replace(btrim(""ParentPhoneNumber""), '^\+', ''), '[ .()\-]', '', 'g')
                                WHEN regexp_replace(regexp_replace(btrim(""ParentPhoneNumber""), '^\+', ''), '[ .()\-]', '', 'g') ~ '^84[0-9]{9}$'
                                    THEN '0' || substr(regexp_replace(regexp_replace(btrim(""ParentPhoneNumber""), '^\+', ''), '[ .()\-]', '', 'g'), 3)
                                ELSE ''
                            END
                        ELSE ''
                    END", stored: true);

                entity.HasIndex(s => s.ParentPhoneCanonical)
                    .HasDatabaseName("IX_Students_ParentPhoneCanonical");

                entity.HasIndex(s => new { s.NormalizedFullName, s.ParentPhoneCanonical })
                    .IsUnique()
                    .HasDatabaseName("UX_Students_ActiveIdentity")
                    .HasFilter("\"IsDeleted\" = FALSE");

                entity.ToTable("Students", t =>
                {
                    t.HasCheckConstraint("CK_Students_ParentPhoneNumber_Canonical", "(\"ParentPhoneNumber\" IS NULL OR btrim(\"ParentPhoneNumber\") = '' OR \"ParentPhoneCanonical\" <> '')");
                });
            });

            // Attendance AttendanceDay and indexes
            builder.Entity<Attendance>(entity =>
            {
                entity.Property(a => a.AttendanceDay)
                    .HasComputedColumnSql("(\"Date\" AT TIME ZONE 'UTC')::date", stored: true);

                entity.HasIndex(a => new { a.StudentId, a.AttendanceDay })
                    .IsUnique()
                    .HasDatabaseName("UX_Attendances_StudentId_AttendanceDay");

                entity.HasIndex(a => new { a.AttendanceDay, a.ClassId })
                    .HasDatabaseName("IX_Attendances_AttendanceDay_ClassId");
            });
        }

        public override int SaveChanges()
        {
            HandleCanonicalProperties();
            HandleSoftDelete();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            HandleCanonicalProperties();
            HandleSoftDelete();
            var auditEntries = OnBeforeSaveChanges();
            var result = await base.SaveChangesAsync(cancellationToken);
            await OnAfterSaveChangesAsync(auditEntries);
            return result;
        }

        private void HandleCanonicalProperties()
        {
            foreach (var entry in ChangeTracker.Entries<Student>())
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    entry.Entity.ParentPhoneCanonical = PhoneHelper.NormalizePhone(entry.Entity.ParentPhoneNumber);
                    entry.Entity.NormalizedFullName = (entry.Entity.FullName ?? string.Empty).Trim().ToLowerInvariant();
                }
            }

            foreach (var entry in ChangeTracker.Entries<AppUser>())
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    var phone = PhoneHelper.NormalizePhone(entry.Entity.PhoneNumber);
                    if (string.IsNullOrEmpty(phone))
                        phone = PhoneHelper.NormalizePhone(entry.Entity.UserName);
                    entry.Entity.CanonicalPhone = string.IsNullOrEmpty(phone) ? null : phone;
                }
            }

            foreach (var entry in ChangeTracker.Entries<Attendance>())
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    entry.Entity.AttendanceDay = DateOnly.FromDateTime(entry.Entity.Date);
                }
            }
        }

        private void HandleSoftDelete()
        {
            var entries = ChangeTracker.Entries<ISoftDelete>()
                .Where(e => e.State == EntityState.Deleted);

            foreach (var entry in entries)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = DateTime.UtcNow;
            }
        }

        private List<AuditEntry> OnBeforeSaveChanges()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            // Chỉ log nếu là Lecture (Tuỳ theo logic có thể log tất cả)
            var isLecture = httpContext?.User != null &&
                (httpContext.User.IsInRole(AppRoles.Lecture) ||
                 httpContext.User.FindAll(ClaimTypes.Role).Any(c => c.Value == AppRoles.Lecture));
            var userId = httpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!isLecture)
            {
                return new List<AuditEntry>();
            }

            ChangeTracker.DetectChanges();
            var auditEntries = new List<AuditEntry>();

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var auditEntry = new AuditEntry(entry)
                {
                    TableName = entry.Entity.GetType().Name,
                    UserId = userId,
                    Action = entry.State.ToString()
                };

                auditEntries.Add(auditEntry);

                foreach (var property in entry.Properties)
                {
                    string propertyName = property.Metadata.Name;

                    if (property.IsTemporary)
                    {
                        auditEntry.TemporaryProperties.Add(property);
                        continue;
                    }

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                            break;
                        case EntityState.Deleted:
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            break;
                        case EntityState.Modified:
                            if (property.IsModified)
                            {
                                auditEntry.OldValues[propertyName] = property.OriginalValue;
                                auditEntry.NewValues[propertyName] = property.CurrentValue;
                            }
                            break;
                    }
                }
            }

            foreach (var auditEntry in auditEntries.Where(_ => !_.HasTemporaryProperties))
            {
                AuditLogs.Add(auditEntry.ToAuditLog());
            }

            return auditEntries.Where(_ => _.HasTemporaryProperties).ToList();
        }

        private async Task OnAfterSaveChangesAsync(List<AuditEntry> auditEntries)
        {
            if (auditEntries == null || auditEntries.Count == 0)
                return;

            foreach (var auditEntry in auditEntries)
            {
                foreach (var prop in auditEntry.TemporaryProperties)
                {
                    if (prop.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                    else
                    {
                        auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                }
                AuditLogs.Add(auditEntry.ToAuditLog());
            }

            await base.SaveChangesAsync();
        }
    }
}
