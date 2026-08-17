using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingService.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RoutingKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextPublishAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishAttempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationOutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherUserId = table.Column<string>(type: "text", nullable: false),
                    NextVersionNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestReviewPolicyRevisions",
                columns: table => new
                {
                    TestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestReviewPolicyRevisions", x => new { x.TestId, x.Revision });
                    table.ForeignKey(
                        name: "FK_TestReviewPolicyRevisions_Tests_TestId",
                        column: x => x.TestId,
                        principalTable: "Tests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseTestId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ContentRevision = table.Column<int>(type: "integer", nullable: false),
                    VersionSlot = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeLimitMinutes = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    LlmModelKey = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestVersions_Tests_CourseTestId",
                        column: x => x.CourseTestId,
                        principalTable: "Tests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseTestVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    CheckMode = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    MaxPoints = table.Column<decimal>(type: "numeric", nullable: false),
                    WrongAnswerPenalty = table.Column<decimal>(type: "numeric", nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestTasks_TestVersions_CourseTestVersionId",
                        column: x => x.CourseTestVersionId,
                        principalTable: "TestVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnswerOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TestTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnswerOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnswerOptions_TestTasks_TestTaskId",
                        column: x => x.TestTaskId,
                        principalTable: "TestTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerOptions_TestTaskId",
                table: "AnswerOptions",
                column: "TestTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationOutboxMessages_PublishedAt_NextPublishAttemptAt",
                table: "IntegrationOutboxMessages",
                columns: new[] { "PublishedAt", "NextPublishAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TestReviewPolicyRevisions_EventId",
                table: "TestReviewPolicyRevisions",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tests_TeacherUserId",
                table: "Tests",
                column: "TeacherUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TestTasks_CourseTestVersionId",
                table: "TestTasks",
                column: "CourseTestVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_TestVersions_CourseTestId_VersionNumber",
                table: "TestVersions",
                columns: new[] { "CourseTestId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestVersions_CourseTestId_VersionSlot",
                table: "TestVersions",
                columns: new[] { "CourseTestId", "VersionSlot" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnswerOptions");

            migrationBuilder.DropTable(
                name: "IntegrationOutboxMessages");

            migrationBuilder.DropTable(
                name: "TestReviewPolicyRevisions");

            migrationBuilder.DropTable(
                name: "TestTasks");

            migrationBuilder.DropTable(
                name: "TestVersions");

            migrationBuilder.DropTable(
                name: "Tests");
        }
    }
}
