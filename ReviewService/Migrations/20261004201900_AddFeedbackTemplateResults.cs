using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReviewService.Migrations;

    public partial class AddFeedbackTemplateResults : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FeedbackTemplate",
                table: "TestReviewPolicies",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "FeedbackSections",
                table: "ReviewTasks",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "FeedbackTemplate",
                table: "Reviews",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.Sql(
                """
                UPDATE "TestReviewPolicies"
                SET "FeedbackTemplate" = '{"id":"7a9a6d92-d8a5-4f7c-9bd4-6f7f52694401","name":"Сбалансированная обратная связь","tone":"supportive","detailLevel":"standard","answerDisclosure":"explainApproach","customInstruction":"","sections":[{"id":"755f5c88-018e-44ed-9535-49af05215e01","title":"Что получилось","instruction":"Отметь конкретные сильные стороны ответа.","format":"list","maxItems":3,"isRequired":true},{"id":"755f5c88-018e-44ed-9535-49af05215e02","title":"Что улучшить","instruction":"Назови главные ошибки или пробелы и объясни их без готового полного ответа.","format":"list","maxItems":3,"isRequired":true},{"id":"755f5c88-018e-44ed-9535-49af05215e03","title":"Следующий шаг","instruction":"Дай один понятный следующий шаг для доработки ответа.","format":"text","maxItems":1,"isRequired":true}]}'::jsonb;

                UPDATE "Reviews"
                SET "FeedbackTemplate" = '{"id":"7a9a6d92-d8a5-4f7c-9bd4-6f7f52694401","name":"Сбалансированная обратная связь","tone":"supportive","detailLevel":"standard","answerDisclosure":"explainApproach","customInstruction":"","sections":[{"id":"755f5c88-018e-44ed-9535-49af05215e01","title":"Что получилось","instruction":"Отметь конкретные сильные стороны ответа.","format":"list","maxItems":3,"isRequired":true},{"id":"755f5c88-018e-44ed-9535-49af05215e02","title":"Что улучшить","instruction":"Назови главные ошибки или пробелы и объясни их без готового полного ответа.","format":"list","maxItems":3,"isRequired":true},{"id":"755f5c88-018e-44ed-9535-49af05215e03","title":"Следующий шаг","instruction":"Дай один понятный следующий шаг для доработки ответа.","format":"text","maxItems":1,"isRequired":true}]}'::jsonb;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeedbackTemplate",
                table: "TestReviewPolicies");

            migrationBuilder.DropColumn(
                name: "FeedbackSections",
                table: "ReviewTasks");

            migrationBuilder.DropColumn(
                name: "FeedbackTemplate",
                table: "Reviews");
        }
    }
