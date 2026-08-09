using LLMTutorRoom.Data;
using LLMTutorRoom.DTOs;
using LLMTutorRoom.Models;
using LLMTutorRoom.Services;
using LLMTutorRoom.Services.ReviewProcessing;
using LLMTutorRoom.Services.Teaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;

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
            var test = CreateTest(task);
            var attempt = await AddAttemptAsync(
                dbContext,
                test.Id,
                new Dictionary<string, string> { [task.Id] = "option-correct" });
            var service = CreateClassroomService(dbContext, publisher, test);

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
            var test = CreateTest(task);
            var attempt = await AddAttemptAsync(
                dbContext,
                test.Id,
                new Dictionary<string, string> { [task.Id] = "Развернутый ответ ученика." });
            var service = CreateClassroomService(dbContext, publisher, test);

            await service.SubmitAttemptAsync(
                attempt.Id,
                "student",
                "Student",
                CancellationToken.None);

            var review = await dbContext.SubmissionReviews
                .Include(item => item.TaskResults)
                .SingleAsync();

            Assert.Equal(SubmissionReviewStatus.Queued, review.Status);
            Assert.Equal("student", review.StudentUserId);
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
            var test = CreateTest(task);
            var attempt = await AddAttemptAsync(
                dbContext,
                test.Id,
                new Dictionary<string, string> { [task.Id] = "Ответ для ручной проверки." },
                TestAttemptStatus.Submitted);
            var review = new SubmissionReview
            {
                AttemptId = attempt.Id,
                TestId = "test-1",
                TestTitle = "Test",
                StudentUserId = "student",
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
                        TaskPrompt = task.Prompt,
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
            var test = CreateTest(task);
            await AddAttemptAsync(
                dbContext,
                test.Id,
                new Dictionary<string, string> { [task.Id] = "Ответ." },
                TestAttemptStatus.Submitted);
            var review = new SubmissionReview
            {
                TestId = "test-1",
                TestTitle = "Test",
                StudentUserId = "student",
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
                        TaskPrompt = task.Prompt,
                        CheckMode = TestTaskCheckMode.Manual,
                        Status = TaskReviewResultStatus.ManualReview,
                        MaxScore = task.MaxPoints
                    }
                }
            };
            dbContext.SubmissionReviews.Add(review);
            await dbContext.SaveChangesAsync();
            var service = CreateClassroomService(dbContext, publisher, test);

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
            FakeReviewQueuePublisher publisher,
            params CourseTestDto[] tests)
        {
            return new ClassroomService(
                dbContext,
                new FakeTeachingServiceClient(tests),
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
            string testId,
            Dictionary<string, string> answers,
            TestAttemptStatus status = TestAttemptStatus.InProgress)
        {
            var attempt = new TestAttempt
            {
                TestId = testId,
                StudentUserId = "student",
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

        private static CourseTestDto CreateTest(params TestTaskDto[] tasks)
        {
            return new CourseTestDto
            {
                Id = "test-1",
                Title = "Test",
                Subject = "Subject",
                Status = CourseTestStatus.Published,
                Deadline = DateTimeOffset.UtcNow.AddDays(1),
                TimeLimitMinutes = 45,
                TotalPoints = tasks
                    .Where(task => !task.IsHidden)
                    .Sum(task => task.MaxPoints),
                Tasks = tasks.ToList()
            };
        }

        private static TestTaskDto CreateSingleChoiceTask()
        {
            return new TestTaskDto
            {
                Id = "task-choice",
                Type = TestTaskType.SingleChoice,
                CheckMode = TestTaskCheckMode.Auto,
                Title = "Choice",
                Prompt = "Choose.",
                MaxPoints = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                CorrectOptionIds = new List<string> { "option-correct" },
                Options = new List<AnswerOptionDto>
                {
                    new()
                    {
                        Id = "option-correct",
                        Text = "Correct",
                        IsCorrect = true
                    },
                    new()
                    {
                        Id = "option-wrong",
                        Text = "Wrong",
                        IsCorrect = false
                    }
                }
            };
        }

        private static TestTaskDto CreateFreeTextTask(TestTaskCheckMode checkMode)
        {
            return new TestTaskDto
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

        private sealed class FakeTeachingServiceClient : ITeachingServiceClient
        {
            private readonly List<CourseTestDto> _tests;

            public FakeTeachingServiceClient(IEnumerable<CourseTestDto> tests)
            {
                _tests = tests.ToList();
            }

            public Task<List<CourseTestDto>> GetTestsAsync(
                bool publishedOnly,
                bool includeHidden,
                CancellationToken cancellationToken)
            {
                var tests = _tests.AsEnumerable();
                if (publishedOnly)
                    tests = tests.Where(test => test.Status == CourseTestStatus.Published);

                return Task.FromResult(tests.Select(test => CopyTest(test, includeHidden)).ToList());
            }

            public Task<CourseTestDto?> GetTestAsync(
                string testId,
                bool includeHidden,
                CancellationToken cancellationToken)
            {
                var test = _tests.SingleOrDefault(item => item.Id == testId);
                return Task.FromResult(test is null ? null : CopyTest(test, includeHidden));
            }

            private static CourseTestDto CopyTest(CourseTestDto test, bool includeHidden)
            {
                var tasks = includeHidden
                    ? test.Tasks
                    : test.Tasks.Where(task => !task.IsHidden).ToList();

                return new CourseTestDto
                {
                    Id = test.Id,
                    Title = test.Title,
                    Subject = test.Subject,
                    Status = test.Status,
                    Deadline = test.Deadline,
                    TimeLimitMinutes = test.TimeLimitMinutes,
                    Summary = test.Summary,
                    TotalPoints = tasks.Sum(task => task.MaxPoints),
                    Tasks = tasks.ToList()
                };
            }
        }
    }
}
