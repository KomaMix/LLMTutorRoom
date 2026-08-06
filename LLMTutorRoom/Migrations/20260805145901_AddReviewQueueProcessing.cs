using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LLMTutorRoom.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewQueueProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskReviewResults_SubmissionReviewId",
                table: "TaskReviewResults");

            migrationBuilder.AddColumn<string>(
                name: "CheckMode",
                table: "TestTasks",
                type: "text",
                nullable: false,
                defaultValue: "Auto");

            migrationBuilder.Sql(
                """
                UPDATE "TestTasks"
                SET "CheckMode" = CASE
                    WHEN "Type" = 'FreeText' THEN 'Llm'
                    ELSE 'Auto'
                END
                """);

            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "TaskReviewResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CheckMode",
                table: "TaskReviewResults",
                type: "text",
                nullable: false,
                defaultValue: "Auto");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "TaskReviewResults",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "TaskReviewResults",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextRetryAt",
                table: "TaskReviewResults",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "TaskReviewResults",
                type: "text",
                nullable: false,
                defaultValue: "Succeeded");

            migrationBuilder.AddColumn<int>(
                name: "AttemptId",
                table: "SubmissionReviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StudentName",
                table: "SubmissionReviews",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "StudentUserName",
                table: "SubmissionReviews",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "SubmissionReviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastEnqueuedAt",
                table: "SubmissionReviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "SubmissionReviews",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextRetryAt",
                table: "SubmissionReviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingAttempts",
                table: "SubmissionReviews",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProcessingLeaseExpiresAt",
                table: "SubmissionReviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QueuedAt",
                table: "SubmissionReviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAt",
                table: "SubmissionReviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskReviewResults_SubmissionReviewId_TaskId",
                table: "TaskReviewResults",
                columns: new[] { "SubmissionReviewId", "TaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionReviews_AttemptId",
                table: "SubmissionReviews",
                column: "AttemptId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionReviews_TestAttempts_AttemptId",
                table: "SubmissionReviews",
                column: "AttemptId",
                principalTable: "TestAttempts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionReviews_TestAttempts_AttemptId",
                table: "SubmissionReviews");

            migrationBuilder.DropIndex(
                name: "IX_TaskReviewResults_SubmissionReviewId_TaskId",
                table: "TaskReviewResults");

            migrationBuilder.DropIndex(
                name: "IX_SubmissionReviews_AttemptId",
                table: "SubmissionReviews");

            migrationBuilder.DropColumn(
                name: "CheckMode",
                table: "TestTasks");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "TaskReviewResults");

            migrationBuilder.DropColumn(
                name: "CheckMode",
                table: "TaskReviewResults");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "TaskReviewResults");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "TaskReviewResults");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "TaskReviewResults");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "TaskReviewResults");

            migrationBuilder.DropColumn(
                name: "AttemptId",
                table: "SubmissionReviews");

            migrationBuilder.DropColumn(
                name: "StudentUserName",
                table: "SubmissionReviews");

            migrationBuilder.AlterColumn<string>(
                name: "StudentName",
                table: "SubmissionReviews",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "SubmissionReviews");

            migrationBuilder.DropColumn(
                name: "LastEnqueuedAt",
                table: "SubmissionReviews");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "SubmissionReviews");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "SubmissionReviews");

            migrationBuilder.DropColumn(
                name: "ProcessingAttempts",
                table: "SubmissionReviews");

            migrationBuilder.DropColumn(
                name: "ProcessingLeaseExpiresAt",
                table: "SubmissionReviews");

            migrationBuilder.DropColumn(
                name: "QueuedAt",
                table: "SubmissionReviews");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "SubmissionReviews");

            migrationBuilder.CreateIndex(
                name: "IX_TaskReviewResults_SubmissionReviewId",
                table: "TaskReviewResults",
                column: "SubmissionReviewId");
        }
    }
}
