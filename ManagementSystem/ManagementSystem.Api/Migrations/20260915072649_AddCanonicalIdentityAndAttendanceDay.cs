using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalIdentityAndAttendanceDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attendances_StudentId",
                table: "Attendances");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedFullName",
                table: "Students",
                type: "text",
                nullable: false,
                computedColumnSql: "lower(btrim(\"FullName\"))",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentPhoneCanonical",
                table: "Students",
                type: "text",
                nullable: false,
                computedColumnSql: "CASE\r\n                        WHEN \"ParentPhoneNumber\" IS NULL OR btrim(\"ParentPhoneNumber\") = '' THEN ''\r\n                        WHEN btrim(\"ParentPhoneNumber\") ~ '^\\+?[0-9 .()\\-]+$' THEN\r\n                            CASE\r\n                                WHEN regexp_replace(regexp_replace(btrim(\"ParentPhoneNumber\"), '^\\+', ''), '[ .()\\-]', '', 'g') ~ '^0[0-9]{9}$'\r\n                                    THEN regexp_replace(regexp_replace(btrim(\"ParentPhoneNumber\"), '^\\+', ''), '[ .()\\-]', '', 'g')\r\n                                WHEN regexp_replace(regexp_replace(btrim(\"ParentPhoneNumber\"), '^\\+', ''), '[ .()\\-]', '', 'g') ~ '^84[0-9]{9}$'\r\n                                    THEN '0' || substr(regexp_replace(regexp_replace(btrim(\"ParentPhoneNumber\"), '^\\+', ''), '[ .()\\-]', '', 'g'), 3)\r\n                                ELSE ''\r\n                            END\r\n                        ELSE ''\r\n                    END",
                stored: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AttendanceDay",
                table: "Attendances",
                type: "date",
                nullable: false,
                computedColumnSql: "(\"Date\" AT TIME ZONE 'UTC')::date",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "CanonicalPhone",
                table: "AspNetUsers",
                type: "text",
                nullable: true,
                computedColumnSql: "COALESCE(\r\n                        CASE\r\n                            WHEN \"PhoneNumber\" IS NOT NULL AND btrim(\"PhoneNumber\") ~ '^\\+?[0-9 .()\\-]+$' THEN\r\n                                CASE\r\n                                    WHEN regexp_replace(regexp_replace(btrim(\"PhoneNumber\"), '^\\+', ''), '[ .()\\-]', '', 'g') ~ '^0[0-9]{9}$'\r\n                                        THEN regexp_replace(regexp_replace(btrim(\"PhoneNumber\"), '^\\+', ''), '[ .()\\-]', '', 'g')\r\n                                    WHEN regexp_replace(regexp_replace(btrim(\"PhoneNumber\"), '^\\+', ''), '[ .()\\-]', '', 'g') ~ '^84[0-9]{9}$'\r\n                                        THEN '0' || substr(regexp_replace(regexp_replace(btrim(\"PhoneNumber\"), '^\\+', ''), '[ .()\\-]', '', 'g'), 3)\r\n                                    ELSE NULL\r\n                                END\r\n                            ELSE NULL\r\n                        END,\r\n                        CASE\r\n                            WHEN \"UserName\" IS NOT NULL AND btrim(\"UserName\") ~ '^\\+?[0-9 .()\\-]+$' THEN\r\n                                CASE\r\n                                    WHEN regexp_replace(regexp_replace(btrim(\"UserName\"), '^\\+', ''), '[ .()\\-]', '', 'g') ~ '^0[0-9]{9}$'\r\n                                        THEN regexp_replace(regexp_replace(btrim(\"UserName\"), '^\\+', ''), '[ .()\\-]', '', 'g')\r\n                                    WHEN regexp_replace(regexp_replace(btrim(\"UserName\"), '^\\+', ''), '[ .()\\-]', '', 'g') ~ '^84[0-9]{9}$'\r\n                                        THEN '0' || substr(regexp_replace(regexp_replace(btrim(\"UserName\"), '^\\+', ''), '[ .()\\-]', '', 'g'), 3)\r\n                                    ELSE NULL\r\n                                END\r\n                            ELSE NULL\r\n                        END\r\n                    )",
                stored: true);

            migrationBuilder.Sql(@"
DO $$
DECLARE
    dup_count integer;
    dup_details text;
BEGIN
    -- 1. Check for duplicate canonical phones in AspNetUsers
    SELECT count(*), string_agg(""CanonicalPhone"", ', ')
    INTO dup_count, dup_details
    FROM (
        SELECT ""CanonicalPhone""
        FROM ""AspNetUsers""
        WHERE ""CanonicalPhone"" IS NOT NULL
        GROUP BY ""CanonicalPhone""
        HAVING count(*) > 1
        LIMIT 5
    ) s;

    IF dup_count > 0 THEN
        RAISE EXCEPTION 'Migration preflight failed: Duplicate canonical phone(s) found in AspNetUsers: %. Resolve collisions before running migration.', dup_details;
    END IF;

    -- 2. Check for duplicate active student identities in Students
    SELECT count(*), string_agg(""NormalizedFullName"" || ':' || ""ParentPhoneCanonical"", ', ')
    INTO dup_count, dup_details
    FROM (
        SELECT ""NormalizedFullName"", ""ParentPhoneCanonical""
        FROM ""Students""
        WHERE ""IsDeleted"" = FALSE
        GROUP BY ""NormalizedFullName"", ""ParentPhoneCanonical""
        HAVING count(*) > 1
        LIMIT 5
    ) s;

    IF dup_count > 0 THEN
        RAISE EXCEPTION 'Migration preflight failed: Duplicate active student identity found in Students: %. Resolve active duplicates before running migration.', dup_details;
    END IF;

    -- 3. Check for duplicate attendance student/day groups
    SELECT count(*), string_agg(""StudentId""::text || ':' || ""AttendanceDay""::text, ', ')
    INTO dup_count, dup_details
    FROM (
        SELECT ""StudentId"", ""AttendanceDay""
        FROM ""Attendances""
        GROUP BY ""StudentId"", ""AttendanceDay""
        HAVING count(*) > 1
        LIMIT 5
    ) s;

    IF dup_count > 0 THEN
        RAISE EXCEPTION 'Migration preflight failed: Duplicate attendances found for student/day: %. Ambiguous attendance records must not be auto-merged.', dup_details;
    END IF;
END $$;
");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ParentPhoneCanonical",
                table: "Students",
                column: "ParentPhoneCanonical");

            migrationBuilder.CreateIndex(
                name: "UX_Students_ActiveIdentity",
                table: "Students",
                columns: new[] { "NormalizedFullName", "ParentPhoneCanonical" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Students_ParentPhoneNumber_Canonical",
                table: "Students",
                sql: "(\"ParentPhoneNumber\" IS NULL OR btrim(\"ParentPhoneNumber\") = '' OR \"ParentPhoneCanonical\" <> '')");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_AttendanceDay_ClassId",
                table: "Attendances",
                columns: new[] { "AttendanceDay", "ClassId" });

            migrationBuilder.CreateIndex(
                name: "UX_Attendances_StudentId_AttendanceDay",
                table: "Attendances",
                columns: new[] { "StudentId", "AttendanceDay" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_AspNetUsers_CanonicalPhone",
                table: "AspNetUsers",
                column: "CanonicalPhone",
                unique: true,
                filter: "\"CanonicalPhone\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AspNetUsers_PhoneAgreement",
                table: "AspNetUsers",
                sql: "(\r\n                        \"UserName\" IS NULL\r\n                        OR \"PhoneNumber\" IS NULL\r\n                        OR btrim(\"UserName\") = ''\r\n                        OR btrim(\"PhoneNumber\") = ''\r\n                        OR NOT (\"UserName\" ~ '^\\+?[0-9 .()\\-]+$' AND regexp_replace(regexp_replace(btrim(\"UserName\"), '^\\+', ''), '[ .()\\-]', '', 'g') ~ '^(0|84)[0-9]{9}$')\r\n                        OR NOT (\"PhoneNumber\" ~ '^\\+?[0-9 .()\\-]+$' AND regexp_replace(regexp_replace(btrim(\"PhoneNumber\"), '^\\+', ''), '[ .()\\-]', '', 'g') ~ '^(0|84)[0-9]{9}$')\r\n                        OR (\r\n                            CASE\r\n                                WHEN regexp_replace(regexp_replace(btrim(\"UserName\"), '^\\+', ''), '[ .()\\-]', '', 'g') ~ '^0[0-9]{9}$'\r\n                                    THEN regexp_replace(regexp_replace(btrim(\"UserName\"), '^\\+', ''), '[ .()\\-]', '', 'g')\r\n                                ELSE '0' || substr(regexp_replace(regexp_replace(btrim(\"UserName\"), '^\\+', ''), '[ .()\\-]', '', 'g'), 3)\r\n                            END\r\n                            =\r\n                            CASE\r\n                                WHEN regexp_replace(regexp_replace(btrim(\"PhoneNumber\"), '^\\+', ''), '[ .()\\-]', '', 'g') ~ '^0[0-9]{9}$'\r\n                                    THEN regexp_replace(regexp_replace(btrim(\"PhoneNumber\"), '^\\+', ''), '[ .()\\-]', '', 'g')\r\n                                ELSE '0' || substr(regexp_replace(regexp_replace(btrim(\"PhoneNumber\"), '^\\+', ''), '[ .()\\-]', '', 'g'), 3)\r\n                            END\r\n                        )\r\n                    )");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AspNetUsers_PhoneNumber_Canonical",
                table: "AspNetUsers",
                sql: "(\"PhoneNumber\" IS NULL OR btrim(\"PhoneNumber\") = '' OR \"CanonicalPhone\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_ParentPhoneCanonical",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "UX_Students_ActiveIdentity",
                table: "Students");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Students_ParentPhoneNumber_Canonical",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_AttendanceDay_ClassId",
                table: "Attendances");

            migrationBuilder.DropIndex(
                name: "UX_Attendances_StudentId_AttendanceDay",
                table: "Attendances");

            migrationBuilder.DropIndex(
                name: "UX_AspNetUsers_CanonicalPhone",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AspNetUsers_PhoneAgreement",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AspNetUsers_PhoneNumber_Canonical",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NormalizedFullName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ParentPhoneCanonical",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AttendanceDay",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "CanonicalPhone",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_StudentId",
                table: "Attendances",
                column: "StudentId");
        }
    }
}
