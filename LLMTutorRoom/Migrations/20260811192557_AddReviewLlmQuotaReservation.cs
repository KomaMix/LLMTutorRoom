using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LLMTutorRoom.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewLlmQuotaReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LlmQuotaReservationError",
                table: "SubmissionReviews",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LlmQuotaReservedAt",
                table: "SubmissionReviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeacherUserId",
                table: "SubmissionReviews",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionReviews_TeacherUserId",
                table: "SubmissionReviews",
                column: "TeacherUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubmissionReviews_TeacherUserId",
                table: "SubmissionReviews");

            migrationBuilder.DropColumn(
                name: "LlmQuotaReservationError",
                table: "SubmissionReviews");

            migrationBuilder.DropColumn(
                name: "LlmQuotaReservedAt",
                table: "SubmissionReviews");

            migrationBuilder.DropColumn(
                name: "TeacherUserId",
                table: "SubmissionReviews");
        }
    }
}
