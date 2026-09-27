using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingService.Migrations
{
    public partial class AddGradingExamples : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GradingExamples",
                table: "TestVersions",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "GradingExamples",
                table: "TestTasks",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GradingExamples",
                table: "TestTasks");

            migrationBuilder.DropColumn(
                name: "GradingExamples",
                table: "TestVersions");
        }
    }
}
