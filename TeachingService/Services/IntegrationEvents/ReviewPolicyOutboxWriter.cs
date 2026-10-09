using System.Text.Json;
using System.Text.Json.Serialization;
using ReviewService.Contracts.Enums;
using ReviewService.Contracts.Events;
using ReviewService.Contracts.Messaging;
using Shared.Observability;
using TeachingService.Contracts.Enums;
using TeachingService.Data;
using TeachingService.Interfaces;
using TeachingService.Models;
using TeachingService.Helpers;
using TeachingCheckMode = TeachingService.Contracts.Enums.TestTaskCheckMode;
using TeachingTaskType = TeachingService.Contracts.Enums.TestTaskType;

namespace TeachingService.Services.IntegrationEvents
{
    public sealed class ReviewPolicyOutboxWriter : IReviewPolicyOutboxWriter
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        private readonly TeachingDbContext _dbContext;

        public ReviewPolicyOutboxWriter(TeachingDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public void StagePublishedRevision(CourseTest test, CourseTestVersion version)
        {
            if (version.CourseTestId != test.Id)
                throw new InvalidOperationException("The version does not belong to the supplied test.");

            if (version.Status != CourseTestStatus.Published)
                throw new InvalidOperationException("Only a published version can produce a review policy.");

            if (version.VersionNumber <= 0)
                throw new InvalidOperationException("A published version must have a positive version number.");

            var revision = version.VersionNumber;
            var eventId = Guid.NewGuid();
            var publishedAt = version.PublishedAt ?? DateTimeOffset.UtcNow;
            var feedbackTemplate = FeedbackTemplateJson.DeserializeSnapshot(
                    version.FeedbackTemplateSnapshotJson)
                ?? throw new InvalidOperationException("A feedback template snapshot is required.");

            var integrationEvent = new TestReviewPolicyPublishedV1
            {
                EventId = eventId,
                TestId = test.Id.ToString(),
                Revision = revision,
                TeacherUserId = test.TeacherUserId,
                TestTitle = version.Title,
                ModelId = version.LlmModelId,
                FeedbackTemplate = ToFeedbackTemplateSnapshot(feedbackTemplate),
                GradingExamples = ToGradingExamples(version.GradingExamples),
                Tasks = version.Tasks
                    .Where(task => !task.IsHidden)
                    .OrderBy(task => task.CreatedAt)
                    .ThenBy(task => task.Id)
                    .Select(ToSnapshot)
                    .ToList(),
                PublishedAt = publishedAt
            };
            var payload = JsonSerializer.Serialize(integrationEvent, JsonOptions);
            var traceContext = ProjectTelemetry.CaptureCurrentTraceContext();

            _dbContext.TestReviewPolicyRevisions.Add(new TestReviewPolicyRevision
            {
                TestId = test.Id,
                Revision = revision,
                EventId = eventId,
                Payload = payload,
                PublishedAt = publishedAt
            });
            _dbContext.IntegrationOutboxMessages.Add(new IntegrationOutboxMessage(
                eventId,
                nameof(TestReviewPolicyPublishedV1),
                ReviewIntegrationRoutes.TestReviewPolicyPublishedV1,
                payload,
                publishedAt,
                traceContext.TraceParent,
                traceContext.TraceState));
        }

        private static ReviewTaskPolicySnapshot ToSnapshot(TestTask task)
        {
            return new ReviewTaskPolicySnapshot
            {
                Id = task.Id.ToString(),
                Type = ToReviewTaskType(task.Type),
                CheckMode = ToReviewCheckMode(task.CheckMode),
                Title = task.Title,
                Prompt = task.Prompt,
                MaxPoints = task.MaxPoints,
                WrongAnswerPenalty = task.WrongAnswerPenalty,
                GradingExamples = ToGradingExamples(task.GradingExamples),
                Options = task.Options
                    .OrderBy(option => option.Id)
                    .Select(option => new ReviewAnswerOptionSnapshot
                    {
                        Id = option.Id.ToString(),
                        Text = option.Text,
                        IsCorrect = option.IsCorrect
                    })
                    .ToList()
            };
        }

        private static List<GradingExampleSnapshot> ToGradingExamples(
            IEnumerable<TeachingService.Models.GradingExample> examples)
        {
            return examples.Select(example => new GradingExampleSnapshot
            {
                TaskPrompt = example.TaskPrompt,
                StudentAnswer = example.StudentAnswer,
                Score = example.Score,
                MaxScore = example.MaxScore,
                Feedback = example.Feedback
            }).ToList();
        }

        private static FeedbackTemplateSnapshot ToFeedbackTemplateSnapshot(
            TeachingService.Contracts.Models.FeedbackTemplateDto template)
        {
            return new FeedbackTemplateSnapshot
            {
                Id = template.Id,
                Name = template.Name,
                Tone = template.Tone switch
                {
                    TeachingService.Contracts.Enums.FeedbackTone.Neutral => ReviewService.Contracts.Enums.FeedbackTone.Neutral,
                    TeachingService.Contracts.Enums.FeedbackTone.Supportive => ReviewService.Contracts.Enums.FeedbackTone.Supportive,
                    TeachingService.Contracts.Enums.FeedbackTone.Formal => ReviewService.Contracts.Enums.FeedbackTone.Formal,
                    TeachingService.Contracts.Enums.FeedbackTone.Direct => ReviewService.Contracts.Enums.FeedbackTone.Direct,
                    _ => throw new ArgumentOutOfRangeException(nameof(template.Tone))
                },
                DetailLevel = template.DetailLevel switch
                {
                    TeachingService.Contracts.Enums.FeedbackDetailLevel.Concise => ReviewService.Contracts.Enums.FeedbackDetailLevel.Concise,
                    TeachingService.Contracts.Enums.FeedbackDetailLevel.Standard => ReviewService.Contracts.Enums.FeedbackDetailLevel.Standard,
                    TeachingService.Contracts.Enums.FeedbackDetailLevel.Detailed => ReviewService.Contracts.Enums.FeedbackDetailLevel.Detailed,
                    _ => throw new ArgumentOutOfRangeException(nameof(template.DetailLevel))
                },
                AnswerDisclosure = template.AnswerDisclosure switch
                {
                    TeachingService.Contracts.Enums.FeedbackAnswerDisclosure.HintsOnly => ReviewService.Contracts.Enums.FeedbackAnswerDisclosure.HintsOnly,
                    TeachingService.Contracts.Enums.FeedbackAnswerDisclosure.ExplainApproach => ReviewService.Contracts.Enums.FeedbackAnswerDisclosure.ExplainApproach,
                    TeachingService.Contracts.Enums.FeedbackAnswerDisclosure.MayShowAnswer => ReviewService.Contracts.Enums.FeedbackAnswerDisclosure.MayShowAnswer,
                    _ => throw new ArgumentOutOfRangeException(nameof(template.AnswerDisclosure))
                },
                CustomInstruction = template.CustomInstruction,
                Sections = template.Sections.Select(section => new FeedbackTemplateSectionSnapshot
                {
                    Id = section.Id,
                    Title = section.Title,
                    Instruction = section.Instruction,
                    Format = section.Format switch
                    {
                        TeachingService.Contracts.Enums.FeedbackSectionFormat.Text => ReviewService.Contracts.Enums.FeedbackSectionFormat.Text,
                        TeachingService.Contracts.Enums.FeedbackSectionFormat.List => ReviewService.Contracts.Enums.FeedbackSectionFormat.List,
                        _ => throw new ArgumentOutOfRangeException(nameof(section.Format))
                    },
                    MaxItems = section.MaxItems,
                    IsRequired = section.IsRequired
                }).ToList()
            };
        }

        private static ReviewTaskType ToReviewTaskType(TeachingTaskType taskType)
        {
            return taskType switch
            {
                TeachingTaskType.SingleChoice => ReviewTaskType.SingleChoice,
                TeachingTaskType.MultipleChoice => ReviewTaskType.MultipleChoice,
                TeachingTaskType.FreeText => ReviewTaskType.FreeText,
                _ => throw new ArgumentOutOfRangeException(nameof(taskType), taskType, null)
            };
        }

        private static ReviewCheckMode ToReviewCheckMode(TeachingCheckMode checkMode)
        {
            return checkMode switch
            {
                TeachingCheckMode.Auto => ReviewCheckMode.Auto,
                TeachingCheckMode.Manual => ReviewCheckMode.Manual,
                TeachingCheckMode.Llm => ReviewCheckMode.Llm,
                _ => throw new ArgumentOutOfRangeException(nameof(checkMode), checkMode, null)
            };
        }

    }
}
