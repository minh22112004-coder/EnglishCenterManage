using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTotalAttendedAndPaidToStudent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TotalAttendedSessions",
                table: "Students",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalPaidSessions",
                table: "Students",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalAttendedSessions",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "TotalPaidSessions",
                table: "Students");
        }
    }
}
