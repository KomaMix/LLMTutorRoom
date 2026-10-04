using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttemptService.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttemptSubmissionOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    TraceParent = table.Column<string>(type: "character varying(55)", maxLength: 55, nullable: true),
                    TraceState = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PublishAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextPublishAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptSubmissionOutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TestRevision = table.Column<int>(type: "integer", nullable: false),
                    TeacherUserId = table.Column<string>(type: "text", nullable: false),
                    StudentUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    StudentName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SubmissionReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Answers = table.Column<string>(type: "jsonb", nullable: false),
                    AllowedTaskIds = table.Column<string>(type: "jsonb", nullable: false),
                    StateRevision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestAttempts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttemptSubmissionOutboxMessages_AttemptId",
                table: "AttemptSubmissionOutboxMessages",
                column: "AttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttemptSubmissionOutboxMessages_PublishedAt_NextPublishAt",
                table: "AttemptSubmissionOutboxMessages",
                columns: new[] { "PublishedAt", "NextPublishAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TestAttempts_AssignmentId_StudentUserId",
                table: "TestAttempts",
                columns: new[] { "AssignmentId", "StudentUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestAttempts_GroupId",
                table: "TestAttempts",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TestAttempts_Status_EndsAt",
                table: "TestAttempts",
                columns: new[] { "Status", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TestAttempts_StudentUserId_StartedAt",
                table: "TestAttempts",
                columns: new[] { "StudentUserId", "StartedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_TestAttempts_TeacherUserId",
                table: "TestAttempts",
                column: "TeacherUserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttemptSubmissionOutboxMessages");

            migrationBuilder.DropTable(
                name: "TestAttempts");
        }
    }
}
