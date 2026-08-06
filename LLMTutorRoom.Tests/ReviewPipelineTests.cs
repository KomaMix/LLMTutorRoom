using LLMTutorRoom.Data;
using LLMTutorRoom.DTOs;
using LLMTutorRoom.Models;
using LLMTutorRoom.Services;
using LLMTutorRoom.Services.ReviewProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LLMTutorRoom.Tests
{
    public sealed class ReviewPipelineTests
    {
        [Fact]
        public async Task SubmitAttempt_WithOnlyAutoTasks_CompletesReviewWithoutPublishingQueueMessage()
        {
            await using var dbContext = CreateDbContext();
            var publisher = new FakeReviewQueuePublisher();
            var task = CreateSingleChoiceTask();
            var attempt = await AddAttemptAsync(
                dbContext,
                task,
                new Dictionary<string, string> { [task.Id] = "option-correct" });
            var service = CreateClassroomService(dbContext, publisher);

            var submittedAttempt = await service.SubmitAttemptAsync(
                attempt.Id,
                "student",
                "Student",
                CancellationToken.None);

            var review = await dbContext.SubmissionReviews
                .Include(item => item.TaskResults)
                .SingleAsync();

            Assert.NotNull(submittedAttempt);
            Assert.Equal(TestAttemptStatus.Submitted, submittedAttempt.Status);
            Assert.Equal(SubmissionReviewStatus.Checked, review.Status);
            Assert.Equal(task.MaxPoints, review.Score);
            Assert.Empty(publisher.PublishedMessages);
        }

        [Fact]
        public async Task SubmitAttempt_WithLlmTask_CreatesQueuedReviewAndPublishesMessage()
        {
            await using var dbContext = CreateDbContext();
            var publisher = new FakeReviewQueuePublisher();
            var task = CreateFreeTextTask(TestTaskCheckMode.Llm);
            var attempt = await AddAttemptAsync(
                dbContext,
                task,
                new Dictionary<string, string> { [task.Id] = "Развернутый ответ ученика." });
            var service = CreateClassroomService(dbContext, publisher);

            await service.SubmitAttemptAsync(
                attempt.Id,
                "student",
                "Student",
                CancellationToken.None);

            var review = await dbContext.SubmissionReviews
                .Include(item => item.TaskResults)
                .SingleAsync();

            Assert.Equal(SubmissionReviewStatus.Queued, review.Status);
            Assert.Equal("student", review.StudentUserName);
            Assert.NotNull(review.QueuedAt);
            Assert.NotNull(review.LastEnqueuedAt);
            Assert.Single(publisher.PublishedMessages);
            Assert.Equal(review.Id, publisher.PublishedMessages[0].Message.ReviewId);
            Assert.Equal(attempt.Id, publisher.PublishedMessages[0].Message.AttemptId);
            Assert.Equal(TaskReviewResultStatus.Pending, review.TaskResults.Single().Status);
        }

        [Fact]
        public async Task ProcessAsync_WhenLlmGatewayIsDisabled_MovesLlmTaskToManualReviewWithoutRetry()
        {
            await using var dbContext = CreateDbContext();
            var task = CreateFreeTextTask(TestTaskCheckMode.Llm);
            var attempt = await AddAttemptAsync(
                dbContext,
                task,
                new Dictionary<string, string> { [task.Id] = "Ответ для ручной проверки." },
                TestAttemptStatus.Submitted);
            var review = new SubmissionReview
            {
                AttemptId = attempt.Id,
                TestId = "test-1",
                TestTitle = "Test",
                StudentUserName = "student",
                StudentName = "Student",
                Status = SubmissionReviewStatus.Queued,
                ModelKey = "gemma3:12b",
                SubmittedAt = DateTimeOffset.UtcNow,
                QueuedAt = DateTimeOffset.UtcNow,
                MaxScore = task.MaxPoints,
                Summary = string.Empty,
                TaskResults = new List<TaskReviewResult>
                {
                    new()
                    {
                        TaskId = task.Id,
                        TaskTitle = task.Title,
                        CheckMode = TestTaskCheckMode.Llm,
                        Status = TaskReviewResultStatus.Pending,
                        MaxScore = task.MaxPoints
                    }
                }
            };
            dbContext.SubmissionReviews.Add(review);
            await dbContext.SaveChangesAsync();
            var processor = CreateProcessor(
                dbContext,
                new ReviewProcessingOptions { LlmGatewayEnabled = false });

            var outcome = await processor.ProcessAsync(
                new ReviewQueueMessage
                {
                    ReviewId = review.Id,
                    AttemptId = attempt.Id,
                    ModelKey = "gemma3:12b"
                },
                CancellationToken.None);

            var processedReview = await dbContext.SubmissionReviews
                .Include(item => item.TaskResults)
                .SingleAsync();
            var taskResult = processedReview.TaskResults.Single();

            Assert.Equal(ReviewProcessingOutcomeType.Completed, outcome.Type);
            Assert.Equal(SubmissionReviewStatus.ManualReview, processedReview.Status);
            Assert.Equal(TaskReviewResultStatus.ManualReview, taskResult.Status);
            Assert.Equal(0, taskResult.Attempts);
        }

        [Fact]
        public async Task UpdateManualTaskReview_WhenLastManualTaskIsScored_CompletesReview()
        {
            await using var dbContext = CreateDbContext();
            var publisher = new FakeReviewQueuePublisher();
            var task = CreateFreeTextTask(TestTaskCheckMode.Manual);
            await AddAttemptAsync(
                dbContext,
                task,
                new Dictionary<string, string> { [task.Id] = "Ответ." },
                TestAttemptStatus.Submitted);
            var review = new SubmissionReview
            {
                TestId = "test-1",
                TestTitle = "Test",
                StudentUserName = "student",
                StudentName = "Student",
                Status = SubmissionReviewStatus.ManualReview,
                SubmittedAt = DateTimeOffset.UtcNow,
                MaxScore = task.MaxPoints,
                Summary = string.Empty,
                TaskResults = new List<TaskReviewResult>
                {
                    new()
                    {
                        TaskId = task.Id,
                        TaskTitle = task.Title,
                        CheckMode = TestTaskCheckMode.Manual,
                        Status = TaskReviewResultStatus.ManualReview,
                        MaxScore = task.MaxPoints
                    }
                }
            };
            dbContext.SubmissionReviews.Add(review);
            await dbContext.SaveChangesAsync();
            var service = CreateClassroomService(dbContext, publisher);

            var updatedReview = await service.UpdateManualTaskReviewAsync(
                review.Id,
                task.Id,
                new ManualTaskReviewRequest
                {
                    Score = 2.5m,
                    Feedback = "Хороший ответ.",
                    Findings = new List<string> { "Есть аргументация." }
                },
                CancellationToken.None);

            Assert.NotNull(updatedReview);
            Assert.Equal(SubmissionReviewStatus.Checked, updatedReview.Status);
            Assert.Equal(2.5m, updatedReview.Score);
            Assert.NotNull(updatedReview.CompletedAt);
            Assert.Equal(TaskReviewResultStatus.Succeeded, updatedReview.TaskResults.Single().Status);
        }

        private static ClassroomService CreateClassroomService(
            TutorRoomDbContext dbContext,
            FakeReviewQueuePublisher publisher)
        {
            return new ClassroomService(
                dbContext,
                new ReviewScoringService(),
                publisher,
                Options.Create(new ReviewProcessingOptions
                {
                    LlmModelKey = "gemma3:12b"
                }),
                NullLogger<ClassroomService>.Instance);
        }

        private static ReviewJobProcessor CreateProcessor(
            TutorRoomDbContext dbContext,
            ReviewProcessingOptions processingOptions)
        {
            var rabbitMqOptions = Options.Create(new RabbitMqOptions());
            var topology = new RabbitMqReviewTopology(rabbitMqOptions);
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5200")
            };

            return new ReviewJobProcessor(
                dbContext,
                new ReviewScoringService(),
                new LlmGatewayReviewClient(httpClient, Options.Create(processingOptions)),
                topology,
                rabbitMqOptions,
                Options.Create(processingOptions),
                NullLogger<ReviewJobProcessor>.Instance);
        }

        private static TutorRoomDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<TutorRoomDbContext>()
                .UseInMemoryDatabase($"llmtutorroom-tests-{Guid.NewGuid():N}")
                .Options;

            return new TutorRoomDbContext(options);
        }

        private static async Task<TestAttempt> AddAttemptAsync(
            TutorRoomDbContext dbContext,
            TestTask task,
            Dictionary<string, string> answers,
            TestAttemptStatus status = TestAttemptStatus.InProgress)
        {
            dbContext.Tests.Add(new CourseTest
            {
                Id = "test-1",
                Title = "Test",
                Subject = "Subject",
                Status = CourseTestStatus.Published,
                Deadline = DateTimeOffset.UtcNow.AddDays(1),
                TimeLimitMinutes = 45,
                Tasks = new List<TestTask> { task }
            });

            var attempt = new TestAttempt
            {
                TestId = "test-1",
                StudentUserName = "student",
                Status = status,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                EndsAt = DateTimeOffset.UtcNow.AddMinutes(40),
                SubmittedAt = status == TestAttemptStatus.Submitted
                    ? DateTimeOffset.UtcNow
                    : null,
                AnswersJson = System.Text.Json.JsonSerializer.Serialize(
                    answers,
                    new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
            };

            dbContext.TestAttempts.Add(attempt);
            await dbContext.SaveChangesAsync();
            return attempt;
        }

        private static TestTask CreateSingleChoiceTask()
        {
            return new TestTask
            {
                Id = "task-choice",
                Type = TestTaskType.SingleChoice,
                CheckMode = TestTaskCheckMode.Auto,
                Title = "Choice",
                Prompt = "Choose.",
                MaxPoints = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                Options = new List<AnswerOption>
                {
                    new()
                    {
                        Id = "option-correct",
                        TestTaskId = "task-choice",
                        Text = "Correct",
                        IsCorrect = true
                    },
                    new()
                    {
                        Id = "option-wrong",
                        TestTaskId = "task-choice",
                        Text = "Wrong",
                        IsCorrect = false
                    }
                }
            };
        }

        private static TestTask CreateFreeTextTask(TestTaskCheckMode checkMode)
        {
            return new TestTask
            {
                Id = "task-free",
                Type = TestTaskType.FreeText,
                CheckMode = checkMode,
                Title = "Free text",
                Prompt = "Explain.",
                MaxPoints = 3,
                CreatedAt = DateTimeOffset.UtcNow
            };
        }

        private sealed class FakeReviewQueuePublisher : IReviewQueuePublisher
        {
            public List<(ReviewQueueMessage Message, int? RetryDelaySeconds)> PublishedMessages { get; } = new();

            public Task PublishAsync(
                ReviewQueueMessage message,
                int? retryDelaySeconds,
                CancellationToken cancellationToken)
            {
                PublishedMessages.Add((message, retryDelaySeconds));
                return Task.CompletedTask;
            }
        }
    }
}
