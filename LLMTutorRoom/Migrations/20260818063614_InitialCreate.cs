using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LLMTutorRoom.Migrations
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
                    AttemptId = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    PublishAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextPublishAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptSubmissionOutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestId = table.Column<string>(type: "text", nullable: false),
                    TestRevision = table.Column<int>(type: "integer", nullable: false),
                    StudentUserId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                name: "IX_TestAttempts_TestId_StudentUserId",
                table: "TestAttempts",
                columns: new[] { "TestId", "StudentUserId" },
                unique: true);
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
