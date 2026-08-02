using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LLMTutorRoom.Migrations
{
    /// <inheritdoc />
    public partial class BackfillTaskCreatedAtOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "TestTasks" AS target
                SET "CreatedAt" = ordered."CreatedAt"
                FROM (
                    SELECT
                        "Id",
                        MIN("CreatedAt") OVER (PARTITION BY "CourseTestId")
                            + ((ROW_NUMBER() OVER (PARTITION BY "CourseTestId" ORDER BY ctid) - 1) * INTERVAL '1 millisecond') AS "CreatedAt"
                    FROM "TestTasks"
                ) AS ordered
                WHERE target."Id" = ordered."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
