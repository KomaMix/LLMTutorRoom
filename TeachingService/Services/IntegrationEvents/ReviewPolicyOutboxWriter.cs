using System.Text.Json;
using System.Text.Json.Serialization;
using ReviewService.Contracts.Enums;
using ReviewService.Contracts.Events;
using ReviewService.Contracts.Messaging;
using TeachingService.Contracts.Enums;
using TeachingService.Data;
using TeachingService.Interfaces;
using TeachingService.Models;
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

            var integrationEvent = new TestReviewPolicyPublishedV1
            {
                EventId = eventId,
                TestId = test.Id.ToString(),
                Revision = revision,
                TeacherUserId = test.TeacherUserId,
                TestTitle = version.Title,
                ModelKey = version.LlmModelKey,
                Tasks = version.Tasks
                    .Where(task => !task.IsHidden)
                    .OrderBy(task => task.CreatedAt)
                    .ThenBy(task => task.Id)
                    .Select(ToSnapshot)
                    .ToList(),
                PublishedAt = publishedAt
            };
            var payload = JsonSerializer.Serialize(integrationEvent, JsonOptions);

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
                publishedAt));
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
