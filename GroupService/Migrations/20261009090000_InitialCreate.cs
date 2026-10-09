using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroupService.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StudyGroups",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                TeacherUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Revision = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StudyGroups", group => group.Id);
            });

        migrationBuilder.CreateTable(
            name: "StudyGroupStudents",
            columns: table => new
            {
                StudyGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                StudentUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StudyGroupStudents", student => new { student.StudyGroupId, student.StudentUserId });
                table.ForeignKey(
                    name: "FK_StudyGroupStudents_StudyGroups_StudyGroupId",
                    column: student => student.StudyGroupId,
                    principalTable: "StudyGroups",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_StudyGroups_TeacherUserId",
            table: "StudyGroups",
            column: "TeacherUserId");

        migrationBuilder.CreateIndex(
            name: "IX_StudyGroupStudents_StudentUserId",
            table: "StudyGroupStudents",
            column: "StudentUserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StudyGroupStudents");
        migrationBuilder.DropTable(name: "StudyGroups");
    }
}
