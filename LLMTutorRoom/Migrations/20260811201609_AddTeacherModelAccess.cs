using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LLMTutorRoom.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherModelAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeacherModelAccesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeacherUserId = table.Column<string>(type: "text", nullable: false),
                    ModelKey = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PeriodSeconds = table.Column<int>(type: "integer", nullable: false),
                    MaxChecks = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    TeacherUserId = table.Column<string>(type: "text", nullable: false),
                    ModelKey = table.Column<string>(type: "text", nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodSeconds = table.Column<int>(type: "integer", nullable: false),
                    UsedChecks = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherModelUsages", x => x.Id);
                });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeacherModelAccesses");

            migrationBuilder.DropTable(
                name: "TeacherModelUsages");
        }
    }
}
