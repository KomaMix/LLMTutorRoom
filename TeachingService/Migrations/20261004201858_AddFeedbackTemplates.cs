using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingService.Migrations;

    public partial class AddFeedbackTemplates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FeedbackTemplateId",
                table: "TestVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeedbackTemplateSnapshot",
                table: "TestVersions",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.Sql(
                """
                UPDATE "TestVersions"
                SET "FeedbackTemplateSnapshot" = '{"id":"7a9a6d92-d8a5-4f7c-9bd4-6f7f52694401","name":"Сбалансированная обратная связь","description":"Краткий разбор сильных сторон, ошибок и следующего шага без готового ответа.","isSystem":true,"tone":"supportive","detailLevel":"standard","answerDisclosure":"explainApproach","customInstruction":"","sections":[{"id":"755f5c88-018e-44ed-9535-49af05215e01","title":"Что получилось","instruction":"Отметь конкретные сильные стороны ответа.","format":"list","maxItems":3,"isRequired":true},{"id":"755f5c88-018e-44ed-9535-49af05215e02","title":"Что улучшить","instruction":"Назови главные ошибки или пробелы и объясни их без готового полного ответа.","format":"list","maxItems":3,"isRequired":true},{"id":"755f5c88-018e-44ed-9535-49af05215e03","title":"Следующий шаг","instruction":"Дай один понятный следующий шаг для доработки ответа.","format":"text","maxItems":1,"isRequired":true}],"revision":1,"createdAt":"1970-01-01T00:00:00+00:00","updatedAt":"1970-01-01T00:00:00+00:00"}'::jsonb;
                """);

            migrationBuilder.CreateTable(
                name: "FeedbackTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Tone = table.Column<string>(type: "text", nullable: false),
                    DetailLevel = table.Column<string>(type: "text", nullable: false),
                    AnswerDisclosure = table.Column<string>(type: "text", nullable: false),
                    CustomInstruction = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Sections = table.Column<string>(type: "jsonb", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackTemplates_TeacherUserId",
                table: "FeedbackTemplates",
                column: "TeacherUserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedbackTemplates");

            migrationBuilder.DropColumn(
                name: "FeedbackTemplateId",
                table: "TestVersions");

            migrationBuilder.DropColumn(
                name: "FeedbackTemplateSnapshot",
                table: "TestVersions");
        }
    }
