using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingService.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherOwnedTestsAndLlmModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LlmModelKey",
                table: "Tests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TeacherUserId",
                table: "Tests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Tests_TeacherUserId",
                table: "Tests",
                column: "TeacherUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tests_TeacherUserId",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "LlmModelKey",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "TeacherUserId",
                table: "Tests");
        }
    }
}
