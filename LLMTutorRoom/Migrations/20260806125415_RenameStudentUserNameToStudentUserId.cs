using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LLMTutorRoom.Migrations
{
    public partial class RenameStudentUserNameToStudentUserId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StudentUserName",
                table: "TestAttempts",
                newName: "StudentUserId");

            migrationBuilder.RenameIndex(
                name: "IX_TestAttempts_TestId_StudentUserName",
                table: "TestAttempts",
                newName: "IX_TestAttempts_TestId_StudentUserId");

            migrationBuilder.RenameColumn(
                name: "StudentUserName",
                table: "SubmissionReviews",
                newName: "StudentUserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StudentUserId",
                table: "TestAttempts",
                newName: "StudentUserName");

            migrationBuilder.RenameIndex(
                name: "IX_TestAttempts_TestId_StudentUserId",
                table: "TestAttempts",
                newName: "IX_TestAttempts_TestId_StudentUserName");

            migrationBuilder.RenameColumn(
                name: "StudentUserId",
                table: "SubmissionReviews",
                newName: "StudentUserName");
        }
    }
}
