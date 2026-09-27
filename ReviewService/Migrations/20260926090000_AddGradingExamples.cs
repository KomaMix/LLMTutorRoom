using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReviewService.Migrations
{
    public partial class AddGradingExamples : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GradingExamples",
                table: "Reviews",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "GradingExamples",
                table: "ReviewTasks",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "GradingExamples",
                table: "TestReviewPolicies",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GradingExamples",
                table: "TestReviewPolicies");

            migrationBuilder.DropColumn(
                name: "GradingExamples",
                table: "ReviewTasks");

            migrationBuilder.DropColumn(
                name: "GradingExamples",
                table: "Reviews");
        }
    }
}
