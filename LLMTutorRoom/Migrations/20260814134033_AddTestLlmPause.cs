using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LLMTutorRoom.Migrations
{
    /// <inheritdoc />
    public partial class AddTestLlmPause : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TestLlmPauses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestId = table.Column<string>(type: "text", nullable: false),
                    ModelKey = table.Column<string>(type: "text", nullable: false),
                    PausedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestLlmPauses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestLlmPauses_PausedUntil",
                table: "TestLlmPauses",
                column: "PausedUntil");

            migrationBuilder.CreateIndex(
                name: "IX_TestLlmPauses_TestId_ModelKey",
                table: "TestLlmPauses",
                columns: new[] { "TestId", "ModelKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestLlmPauses");
        }
    }
}
