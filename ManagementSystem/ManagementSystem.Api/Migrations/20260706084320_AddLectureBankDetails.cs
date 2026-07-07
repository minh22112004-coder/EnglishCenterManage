using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLectureBankDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaskDescription",
                table: "WorkSchedules");

            migrationBuilder.RenameColumn(
                name: "Notes",
                table: "WorkSchedules",
                newName: "LectureNotice");

            migrationBuilder.AddColumn<string>(
                name: "AdminNotice",
                table: "WorkSchedules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTaskCompleted",
                table: "WorkSchedules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TimeSlots",
                table: "Classes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalSessions",
                table: "Classes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankNumber",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrCodeImage",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClassWorkSchedule",
                columns: table => new
                {
                    ClassesId = table.Column<int>(type: "integer", nullable: false),
                    WorkSchedulesId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassWorkSchedule", x => new { x.ClassesId, x.WorkSchedulesId });
                    table.ForeignKey(
                        name: "FK_ClassWorkSchedule_Classes_ClassesId",
                        column: x => x.ClassesId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassWorkSchedule_WorkSchedules_WorkSchedulesId",
                        column: x => x.WorkSchedulesId,
                        principalTable: "WorkSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassWorkSchedule_WorkSchedulesId",
                table: "ClassWorkSchedule",
                column: "WorkSchedulesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassWorkSchedule");

            migrationBuilder.DropColumn(
                name: "AdminNotice",
                table: "WorkSchedules");

            migrationBuilder.DropColumn(
                name: "IsTaskCompleted",
                table: "WorkSchedules");

            migrationBuilder.DropColumn(
                name: "TimeSlots",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "TotalSessions",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BankNumber",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "QrCodeImage",
                table: "AspNetUsers");

            migrationBuilder.RenameColumn(
                name: "LectureNotice",
                table: "WorkSchedules",
                newName: "Notes");

            migrationBuilder.AddColumn<string>(
                name: "TaskDescription",
                table: "WorkSchedules",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
