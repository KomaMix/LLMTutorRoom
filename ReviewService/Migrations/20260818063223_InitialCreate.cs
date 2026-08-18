using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ReviewService.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "PendingSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttemptId = table.Column<int>(type: "integer", nullable: false),
                    TestId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TestRevision = table.Column<int>(type: "integer", nullable: false),
                    StudentUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    StudentName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Answers = table.Column<string>(type: "jsonb", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingSubmissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttemptId = table.Column<int>(type: "integer", nullable: false),
                    TestId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TestRevision = table.Column<int>(type: "integer", nullable: false),
                    TestTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TeacherUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    StudentUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    StudentName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ModelKeySnapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextRetryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProcessingLeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastEnqueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LlmQuotaReservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LlmQuotaReservationError = table.Column<string>(type: "text", nullable: false),
                    ProcessingAttempts = table.Column<int>(type: "integer", nullable: false),
                    ProcessingGeneration = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxScore = table.Column<decimal>(type: "numeric", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeacherModelAccesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeacherUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ModelKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PeriodSeconds = table.Column<int>(type: "integer", nullable: false),
                    MaxChecks = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherModelAccesses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeacherModelUsages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeacherUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ModelKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodSeconds = table.Column<int>(type: "integer", nullable: false),
                    UsedChecks = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherModelUsages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestReviewPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    TeacherUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TestTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ModelKeySnapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Tasks = table.Column<string>(type: "jsonb", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestReviewPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReviewTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReviewId = table.Column<int>(type: "integer", nullable: false),
                    TaskId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TaskType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TaskTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TaskPrompt = table.Column<string>(type: "text", nullable: false),
                    CheckMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StudentAnswer = table.Column<string>(type: "text", nullable: false),
                    WrongAnswerPenalty = table.Column<decimal>(type: "numeric", nullable: false),
                    AnswerOptions = table.Column<string>(type: "jsonb", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextRetryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxScore = table.Column<decimal>(type: "numeric", nullable: false),
                    Feedback = table.Column<string>(type: "text", nullable: false),
                    Findings = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewTasks_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ProcessedAt",
                table: "InboxMessages",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PendingSubmissions_AttemptId",
                table: "PendingSubmissions",
                column: "AttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingSubmissions_ReceivedAt",
                table: "PendingSubmissions",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PendingSubmissions_TestId_TestRevision",
                table: "PendingSubmissions",
                columns: new[] { "TestId", "TestRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_AttemptId",
                table: "Reviews",
                column: "AttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_Status_NextRetryAt_LastEnqueuedAt",
                table: "Reviews",
                columns: new[] { "Status", "NextRetryAt", "LastEnqueuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_Status_ProcessingLeaseExpiresAt",
                table: "Reviews",
                columns: new[] { "Status", "ProcessingLeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_StudentUserId",
                table: "Reviews",
                column: "StudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_StudentUserId_SubmittedAt_Id",
                table: "Reviews",
                columns: new[] { "StudentUserId", "SubmittedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_TeacherUserId",
                table: "Reviews",
                column: "TeacherUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_TeacherUserId_SubmittedAt_Id",
                table: "Reviews",
                columns: new[] { "TeacherUserId", "SubmittedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_TestId_TestRevision",
                table: "Reviews",
                columns: new[] { "TestId", "TestRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewTasks_ReviewId_TaskId",
                table: "ReviewTasks",
                columns: new[] { "ReviewId", "TaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherModelAccesses_TeacherUserId",
                table: "TeacherModelAccesses",
                column: "TeacherUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherModelAccesses_TeacherUserId_ModelKey",
                table: "TeacherModelAccesses",
                columns: new[] { "TeacherUserId", "ModelKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherModelUsages_TeacherUserId",
                table: "TeacherModelUsages",
                column: "TeacherUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherModelUsages_TeacherUserId_ModelKey_PeriodStart_Perio~",
                table: "TeacherModelUsages",
                columns: new[] { "TeacherUserId", "ModelKey", "PeriodStart", "PeriodSeconds" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestReviewPolicies_TestId_Revision",
                table: "TestReviewPolicies",
                columns: new[] { "TestId", "Revision" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxMessages");

            migrationBuilder.DropTable(
                name: "PendingSubmissions");

            migrationBuilder.DropTable(
                name: "ReviewTasks");

            migrationBuilder.DropTable(
                name: "TeacherModelAccesses");

            migrationBuilder.DropTable(
                name: "TeacherModelUsages");

            migrationBuilder.DropTable(
                name: "TestReviewPolicies");

            migrationBuilder.DropTable(
                name: "Reviews");
        }
    }
}
