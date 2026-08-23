using System.Text.Json;
using AttemptService.Contracts.Enums;
using AttemptService.Contracts.Responses;
using AttemptService.Enums;
using AttemptService.Models;
using AttemptService.Options;
using AttemptService.Services;
using Microsoft.EntityFrameworkCore;
using ReviewService.Contracts.Events;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;

namespace AttemptService.Tests;

public sealed class AttemptLifecycleServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task StartAttempt_CopiesPublishedVersionAndUsesEarlierEndBoundary()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var teachingClient = new StubTeachingServiceClient();
        var distantDeadlineTest = CreatePublishedTest(
            Guid.NewGuid(),
            versionNumber: 7,
            deadline: DateTimeOffset.UtcNow.AddHours(3),
            timeLimitMinutes: 45,
            "task-a");
        var nearDeadline = DateTimeOffset.UtcNow.AddMinutes(5);
        var nearDeadlineTest = CreatePublishedTest(
            Guid.NewGuid(),
            versionNumber: 4,
            deadline: nearDeadline,
            timeLimitMinutes: 60,
            "task-b");
        teachingClient.Tests.Add(distantDeadlineTest.Id, distantDeadlineTest);
        teachingClient.Tests.Add(nearDeadlineTest.Id, nearDeadlineTest);
        var service = CreateService(factory, teachingClient);

        var beforeStart = DateTimeOffset.UtcNow;
        var timeLimitedResult = await service.StartAttemptAsync(
            Guid.Parse(distantDeadlineTest.Id),
            "student-1",
            expectedVersionNumber: 7,
            CancellationToken.None);
        var afterStart = DateTimeOffset.UtcNow;
        var deadlineLimitedResult = await service.StartAttemptAsync(
            Guid.Parse(nearDeadlineTest.Id),
            "student-1",
            expectedVersionNumber: 4,
            CancellationToken.None);

        Assert.Equal(AttemptOperationStatus.Created, timeLimitedResult.Status);
        var timeLimitedAttempt = Assert.IsType<TestAttemptResponse>(timeLimitedResult.Value);
        Assert.NotEqual(Guid.Empty, timeLimitedAttempt.Id);
        Assert.Equal(distantDeadlineTest.Id, timeLimitedAttempt.TestId);
        Assert.Equal(7, timeLimitedAttempt.TestRevision);
        Assert.InRange(timeLimitedAttempt.StartedAt, beforeStart, afterStart);
        Assert.InRange(
            timeLimitedAttempt.EndsAt,
            beforeStart.AddMinutes(45),
            afterStart.AddMinutes(45));

        Assert.Equal(AttemptOperationStatus.Created, deadlineLimitedResult.Status);
        var deadlineLimitedAttempt = Assert.IsType<TestAttemptResponse>(deadlineLimitedResult.Value);
        Assert.Equal(4, deadlineLimitedAttempt.TestRevision);
        Assert.Equal(nearDeadline, deadlineLimitedAttempt.EndsAt);
        Assert.False(teachingClient.LastIncludeHidden);
        Assert.Null(teachingClient.LastVersionNumber);
    }

    [Fact]
    public async Task StartAttempt_ReturnsVersionConflictWithoutCreatingAttempt()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var teachingClient = new StubTeachingServiceClient();
        var test = CreatePublishedTest(
            Guid.NewGuid(),
            versionNumber: 8,
            deadline: DateTimeOffset.UtcNow.AddHours(1),
            timeLimitMinutes: 30,
            "task-a");
        teachingClient.Tests.Add(test.Id, test);
        var service = CreateService(factory, teachingClient);

        var result = await service.StartAttemptAsync(
            Guid.Parse(test.Id),
            "student-1",
            expectedVersionNumber: 7,
            CancellationToken.None);

        Assert.Equal(AttemptOperationStatus.VersionConflict, result.Status);
        Assert.Null(result.Value);
        await using var dbContext = factory.CreateDbContext();
        Assert.Empty(dbContext.TestAttempts);
    }

    [Fact]
    public async Task StartAttempt_WhenTestDeadlinePassed_ReturnsNotFoundWithoutCreatingAttempt()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var teachingClient = new StubTeachingServiceClient();
        var test = CreatePublishedTest(
            Guid.NewGuid(),
            versionNumber: 3,
            deadline: DateTimeOffset.UtcNow.AddMinutes(-1),
            timeLimitMinutes: 30,
            "task-a");
        teachingClient.Tests.Add(test.Id, test);
        var service = CreateService(factory, teachingClient);

        var result = await service.StartAttemptAsync(
            Guid.Parse(test.Id),
            "student-1",
            expectedVersionNumber: 3,
            CancellationToken.None);

        Assert.Equal(AttemptOperationStatus.NotFound, result.Status);
        Assert.Null(result.Value);
        await using var dbContext = factory.CreateDbContext();
        Assert.Empty(dbContext.TestAttempts);
    }

    [Fact]
    public async Task StartAttempt_WhenAttemptAlreadyExists_ReturnsItWithoutDuplicateOrTeachingCall()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var teachingClient = new StubTeachingServiceClient();
        var test = CreatePublishedTest(
            Guid.NewGuid(),
            versionNumber: 2,
            deadline: DateTimeOffset.UtcNow.AddHours(1),
            timeLimitMinutes: 20,
            "task-a");
        teachingClient.Tests.Add(test.Id, test);
        var service = CreateService(factory, teachingClient);

        var firstResult = await service.StartAttemptAsync(
            Guid.Parse(test.Id),
            "student-1",
            expectedVersionNumber: 2,
            CancellationToken.None);
        var repeatedResult = await service.StartAttemptAsync(
            Guid.Parse(test.Id),
            "student-1",
            expectedVersionNumber: 999,
            CancellationToken.None);

        Assert.Equal(AttemptOperationStatus.Created, firstResult.Status);
        Assert.Equal(AttemptOperationStatus.Success, repeatedResult.Status);
        var firstAttempt = Assert.IsType<TestAttemptResponse>(firstResult.Value);
        var repeatedAttempt = Assert.IsType<TestAttemptResponse>(repeatedResult.Value);
        Assert.Equal(firstAttempt.Id, repeatedAttempt.Id);
        Assert.Equal(firstAttempt.TestRevision, repeatedAttempt.TestRevision);
        Assert.Equal(1, teachingClient.RequestCount);
        await using var dbContext = factory.CreateDbContext();
        Assert.Single(dbContext.TestAttempts);
    }

    [Fact]
    public async Task SaveAnswers_KeepsOnlyTasksCapturedWhenAttemptStarted()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var teachingClient = new StubTeachingServiceClient();
        var test = CreatePublishedTest(
            Guid.NewGuid(),
            versionNumber: 3,
            deadline: DateTimeOffset.UtcNow.AddHours(1),
            timeLimitMinutes: 30,
            "task-a",
            "task-b",
            "task-a");
        teachingClient.Tests.Add(test.Id, test);
        var service = CreateService(factory, teachingClient);
        var startResult = await service.StartAttemptAsync(
            Guid.Parse(test.Id),
            "student-1",
            expectedVersionNumber: 3,
            CancellationToken.None);
        var attempt = Assert.IsType<TestAttemptResponse>(startResult.Value);

        var saveResult = await service.SaveAnswersAsync(
            attempt.Id,
            "student-1",
            new Dictionary<string, string>
            {
                ["task-a"] = "answer-a",
                ["task-b"] = "answer-b",
                ["task-not-in-snapshot"] = "must-not-be-saved"
            },
            CancellationToken.None);

        Assert.Equal(AttemptOperationStatus.Success, saveResult.Status);
        var savedAttempt = Assert.IsType<TestAttemptResponse>(saveResult.Value);
        Assert.Equal(2, savedAttempt.Answers.Count);
        Assert.Equal("answer-a", savedAttempt.Answers["task-a"]);
        Assert.Equal("answer-b", savedAttempt.Answers["task-b"]);
        Assert.DoesNotContain("task-not-in-snapshot", savedAttempt.Answers);

        await using var dbContext = factory.CreateDbContext();
        var storedAttempt = await dbContext.TestAttempts.SingleAsync();
        var allowedTaskIds = JsonSerializer.Deserialize<List<string>>(
            storedAttempt.AllowedTaskIdsJson,
            JsonOptions);
        Assert.Equal(new[] { "task-a", "task-b" }, allowedTaskIds);
        Assert.Equal(1, storedAttempt.StateRevision);
    }

    [Fact]
    public async Task SaveAnswers_WhenDeadlinePassed_ExpiresAttemptAndDoesNotCreateOutboxMessage()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var attempt = CreateStoredAttempt(
            Guid.NewGuid(),
            "student-1",
            DateTimeOffset.UtcNow.AddMinutes(-1));
        attempt.AnswersJson = JsonSerializer.Serialize(
            new Dictionary<string, string> { ["task-a"] = "old-answer" },
            JsonOptions);
        await SeedAttemptsAsync(factory, attempt);
        var service = CreateService(factory, new StubTeachingServiceClient());

        var result = await service.SaveAnswersAsync(
            attempt.Id,
            "student-1",
            new Dictionary<string, string> { ["task-a"] = "new-answer" },
            CancellationToken.None);

        Assert.Equal(AttemptOperationStatus.Conflict, result.Status);
        var currentAttempt = Assert.IsType<TestAttemptResponse>(result.Value);
        Assert.Equal(TestAttemptStatus.Expired, currentAttempt.Status);
        Assert.Equal("old-answer", currentAttempt.Answers["task-a"]);

        await using var dbContext = factory.CreateDbContext();
        var storedAttempt = await dbContext.TestAttempts.SingleAsync();
        Assert.Equal(TestAttemptStatus.Expired, storedAttempt.Status);
        Assert.Equal(1, storedAttempt.StateRevision);
        Assert.Empty(dbContext.AttemptSubmissionOutboxMessages);
    }

    [Fact]
    public async Task SaveAnswers_WhenAnswerCountExceedsLimit_ReturnsInvalidInputWithoutChangingAttempt()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var attempt = CreateStoredAttempt(
            Guid.NewGuid(),
            "student-1",
            DateTimeOffset.UtcNow.AddMinutes(10));
        const string existingAnswersJson = "{\"task-a\":\"saved-answer\"}";
        attempt.AnswersJson = existingAnswersJson;
        attempt.AllowedTaskIdsJson = "[\"task-a\",\"task-b\",\"task-c\"]";
        await SeedAttemptsAsync(factory, attempt);
        var service = CreateService(factory, new StubTeachingServiceClient(), new AttemptInputLimitsOptions
        {
            MaxAnswerCount = 2,
            MaxAnswerLength = 20,
            MaxTotalAnswerLength = 40
        });

        var result = await service.SaveAnswersAsync(
            attempt.Id,
            "student-1",
            new Dictionary<string, string>
            {
                ["task-a"] = "answer-a",
                ["task-b"] = "answer-b",
                ["task-c"] = "answer-c"
            },
            CancellationToken.None);

        Assert.Equal(AttemptOperationStatus.InvalidInput, result.Status);
        Assert.Null(result.Value);
        await AssertAttemptWasNotChangedAsync(factory, attempt.Id, existingAnswersJson);
    }

    [Fact]
    public async Task SaveAnswers_WhenSingleAnswerExceedsLengthLimit_ReturnsInvalidInputWithoutChangingAttempt()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var attempt = CreateStoredAttempt(
            Guid.NewGuid(),
            "student-1",
            DateTimeOffset.UtcNow.AddMinutes(10));
        const string existingAnswersJson = "{\"task-a\":\"saved-answer\"}";
        attempt.AnswersJson = existingAnswersJson;
        await SeedAttemptsAsync(factory, attempt);
        var service = CreateService(factory, new StubTeachingServiceClient(), new AttemptInputLimitsOptions
        {
            MaxAnswerCount = 2,
            MaxAnswerLength = 5,
            MaxTotalAnswerLength = 10
        });

        var result = await service.SaveAnswersAsync(
            attempt.Id,
            "student-1",
            new Dictionary<string, string> { ["task-a"] = "123456" },
            CancellationToken.None);

        Assert.Equal(AttemptOperationStatus.InvalidInput, result.Status);
        Assert.Null(result.Value);
        await AssertAttemptWasNotChangedAsync(factory, attempt.Id, existingAnswersJson);
    }

    [Fact]
    public async Task SaveAnswers_WhenTotalAnswerLengthExceedsLimit_ReturnsInvalidInputWithoutChangingAttempt()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var attempt = CreateStoredAttempt(
            Guid.NewGuid(),
            "student-1",
            DateTimeOffset.UtcNow.AddMinutes(10));
        const string existingAnswersJson = "{\"task-a\":\"saved-answer\"}";
        attempt.AnswersJson = existingAnswersJson;
        attempt.AllowedTaskIdsJson = "[\"task-a\",\"task-b\"]";
        await SeedAttemptsAsync(factory, attempt);
        var service = CreateService(factory, new StubTeachingServiceClient(), new AttemptInputLimitsOptions
        {
            MaxAnswerCount = 2,
            MaxAnswerLength = 5,
            MaxTotalAnswerLength = 8
        });

        var result = await service.SaveAnswersAsync(
            attempt.Id,
            "student-1",
            new Dictionary<string, string>
            {
                ["task-a"] = "12345",
                ["task-b"] = "1234"
            },
            CancellationToken.None);

        Assert.Equal(AttemptOperationStatus.InvalidInput, result.Status);
        Assert.Null(result.Value);
        await AssertAttemptWasNotChangedAsync(factory, attempt.Id, existingAnswersJson);
    }

    [Fact]
    public async Task SaveAnswers_WhenAnswerValueIsNull_ReturnsInvalidInputWithoutChangingAttempt()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var attempt = CreateStoredAttempt(
            Guid.NewGuid(),
            "student-1",
            DateTimeOffset.UtcNow.AddMinutes(10));
        const string existingAnswersJson = "{\"task-a\":\"saved-answer\"}";
        attempt.AnswersJson = existingAnswersJson;
        await SeedAttemptsAsync(factory, attempt);
        var service = CreateService(factory, new StubTeachingServiceClient());

        var result = await service.SaveAnswersAsync(
            attempt.Id,
            "student-1",
            new Dictionary<string, string> { ["task-a"] = null! },
            CancellationToken.None);

        Assert.Equal(AttemptOperationStatus.InvalidInput, result.Status);
        Assert.Null(result.Value);
        await AssertAttemptWasNotChangedAsync(factory, attempt.Id, existingAnswersJson);
    }

    [Fact]
    public async Task SubmitAttempt_StoresFinalAnswersAndSingleOutboxEventAtomically()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var teachingClient = new StubTeachingServiceClient();
        var test = CreatePublishedTest(
            Guid.NewGuid(),
            versionNumber: 6,
            deadline: DateTimeOffset.UtcNow.AddHours(1),
            timeLimitMinutes: 30,
            "task-a",
            "task-b");
        teachingClient.Tests.Add(test.Id, test);
        var service = CreateService(factory, teachingClient);
        var startResult = await service.StartAttemptAsync(
            Guid.Parse(test.Id),
            "student-1",
            expectedVersionNumber: 6,
            CancellationToken.None);
        var attempt = Assert.IsType<TestAttemptResponse>(startResult.Value);
        await service.SaveAnswersAsync(
            attempt.Id,
            "student-1",
            new Dictionary<string, string>
            {
                ["task-a"] = "final-a",
                ["task-b"] = "final-b",
                ["forged-task"] = "ignored"
            },
            CancellationToken.None);

        var firstSubmit = await service.SubmitAttemptAsync(
            attempt.Id,
            "student-1",
            "  Alice Student  ",
            CancellationToken.None);

        Assert.Equal(AttemptOperationStatus.Success, firstSubmit.Status);
        var submittedAttempt = Assert.IsType<TestAttemptResponse>(firstSubmit.Value);
        Assert.Equal(TestAttemptStatus.Submitted, submittedAttempt.Status);
        Assert.NotNull(submittedAttempt.SubmittedAt);

        Guid eventId;
        string payload;
        await using (var dbContext = factory.CreateDbContext())
        {
            var storedAttempt = await dbContext.TestAttempts.SingleAsync();
            var outboxMessage = await dbContext.AttemptSubmissionOutboxMessages.SingleAsync();
            var submissionEvent = Assert.IsType<AttemptSubmittedV1>(
                JsonSerializer.Deserialize<AttemptSubmittedV1>(outboxMessage.PayloadJson, JsonOptions));

            Assert.Equal(TestAttemptStatus.Submitted, storedAttempt.Status);
            Assert.Equal(2, storedAttempt.StateRevision);
            Assert.Equal(storedAttempt.SubmittedAt, outboxMessage.OccurredAt);
            Assert.Equal(outboxMessage.Id, submissionEvent.EventId);
            Assert.Equal(storedAttempt.Id, submissionEvent.AttemptId);
            Assert.Equal(test.Id, submissionEvent.TestId);
            Assert.Equal(6, submissionEvent.TestRevision);
            Assert.Equal("student-1", submissionEvent.StudentUserId);
            Assert.Equal("Alice Student", submissionEvent.StudentName);
            Assert.Equal(storedAttempt.SubmittedAt, submissionEvent.SubmittedAt);
            Assert.Equal(2, submissionEvent.Answers.Count);
            Assert.Equal("final-a", submissionEvent.Answers["task-a"]);
            Assert.Equal("final-b", submissionEvent.Answers["task-b"]);
            Assert.DoesNotContain("forged-task", submissionEvent.Answers);

            eventId = outboxMessage.Id;
            payload = outboxMessage.PayloadJson;
        }

        var repeatedSubmit = await service.SubmitAttemptAsync(
            attempt.Id,
            "student-1",
            "Different Name",
            CancellationToken.None);

        Assert.Equal(AttemptOperationStatus.Success, repeatedSubmit.Status);
        await using var verificationContext = factory.CreateDbContext();
        var repeatedAttempt = await verificationContext.TestAttempts.SingleAsync();
        var repeatedOutbox = await verificationContext.AttemptSubmissionOutboxMessages.SingleAsync();
        Assert.Equal(2, repeatedAttempt.StateRevision);
        Assert.Equal(eventId, repeatedOutbox.Id);
        Assert.Equal(payload, repeatedOutbox.PayloadJson);
    }

    [Fact]
    public async Task SubmitAttempt_WhenAlreadySubmittedAndPublished_ReturnsCurrentAttempt()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var attempt = CreateStoredAttempt(
            Guid.NewGuid(),
            "student-1",
            DateTimeOffset.UtcNow.AddMinutes(10));
        attempt.Status = TestAttemptStatus.Submitted;
        attempt.SubmittedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        attempt.StateRevision = 1;
        var outbox = CreatePublishedOutbox(attempt);
        await using (var seedContext = factory.CreateDbContext())
        {
            seedContext.TestAttempts.Add(attempt);
            seedContext.AttemptSubmissionOutboxMessages.Add(outbox);
            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(factory, new StubTeachingServiceClient());
        var result = await service.SubmitAttemptAsync(
            attempt.Id,
            "student-1",
            "Alice",
            CancellationToken.None);

        Assert.Equal(AttemptOperationStatus.Success, result.Status);
        var currentAttempt = Assert.IsType<TestAttemptResponse>(result.Value);
        Assert.Equal(TestAttemptStatus.Submitted, currentAttempt.Status);
        Assert.Equal(attempt.SubmittedAt, currentAttempt.SubmittedAt);
        await using var verificationContext = factory.CreateDbContext();
        Assert.Equal(1, (await verificationContext.TestAttempts.SingleAsync()).StateRevision);
        Assert.Equal(outbox.Id, (await verificationContext.AttemptSubmissionOutboxMessages.SingleAsync()).Id);
    }

    [Fact]
    public async Task SubmitAttempt_WhenPublishedOutboxAppearsForInProgressAttempt_ReturnsConflict()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var attempt = CreateStoredAttempt(
            Guid.NewGuid(),
            "student-1",
            DateTimeOffset.UtcNow.AddMinutes(10));
        var outbox = CreatePublishedOutbox(attempt);
        await using (var seedContext = factory.CreateDbContext())
        {
            seedContext.TestAttempts.Add(attempt);
            seedContext.AttemptSubmissionOutboxMessages.Add(outbox);
            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(factory, new StubTeachingServiceClient());
        var result = await service.SubmitAttemptAsync(
            attempt.Id,
            "student-1",
            "Alice",
            CancellationToken.None);

        Assert.Equal(AttemptOperationStatus.Conflict, result.Status);
        Assert.Null(result.Value);
        await using var verificationContext = factory.CreateDbContext();
        var storedAttempt = await verificationContext.TestAttempts.SingleAsync();
        Assert.Equal(TestAttemptStatus.InProgress, storedAttempt.Status);
        Assert.Null(storedAttempt.SubmittedAt);
        Assert.Equal(outbox.Id, (await verificationContext.AttemptSubmissionOutboxMessages.SingleAsync()).Id);
    }

    [Fact]
    public async Task SubmitAttempt_WhenDeadlinePassed_ExpiresAttemptWithoutSubmissionEvent()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var attempt = CreateStoredAttempt(
            Guid.NewGuid(),
            "student-1",
            DateTimeOffset.UtcNow.AddMinutes(-1));
        await SeedAttemptsAsync(factory, attempt);
        var service = CreateService(factory, new StubTeachingServiceClient());

        var result = await service.SubmitAttemptAsync(
            attempt.Id,
            "student-1",
            "Alice",
            CancellationToken.None);

        Assert.Equal(AttemptOperationStatus.Conflict, result.Status);
        var currentAttempt = Assert.IsType<TestAttemptResponse>(result.Value);
        Assert.Equal(TestAttemptStatus.Expired, currentAttempt.Status);
        Assert.Null(currentAttempt.SubmittedAt);

        await using var dbContext = factory.CreateDbContext();
        var storedAttempt = await dbContext.TestAttempts.SingleAsync();
        Assert.Equal(TestAttemptStatus.Expired, storedAttempt.Status);
        Assert.Null(storedAttempt.SubmittedAt);
        Assert.Equal(1, storedAttempt.StateRevision);
        Assert.Empty(dbContext.AttemptSubmissionOutboxMessages);
    }

    [Fact]
    public async Task GetStudentAttempts_ReturnsOnlyOwnedAttemptsInDescendingStartOrder()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var olderOwned = CreateStoredAttempt(
            Guid.NewGuid(),
            "student-1",
            DateTimeOffset.UtcNow.AddHours(1),
            DateTimeOffset.UtcNow.AddMinutes(-10));
        var newerOwned = CreateStoredAttempt(
            Guid.NewGuid(),
            "student-1",
            DateTimeOffset.UtcNow.AddHours(1),
            DateTimeOffset.UtcNow.AddMinutes(-1));
        var otherStudent = CreateStoredAttempt(
            Guid.NewGuid(),
            "student-2",
            DateTimeOffset.UtcNow.AddHours(1),
            DateTimeOffset.UtcNow);
        await SeedAttemptsAsync(factory, olderOwned, newerOwned, otherStudent);
        var service = CreateService(factory, new StubTeachingServiceClient());

        var attempts = await service.GetStudentAttemptsAsync(
            "student-1",
            CancellationToken.None);

        Assert.Equal(2, attempts.Count);
        Assert.Equal(newerOwned.Id, attempts[0].Id);
        Assert.Equal(olderOwned.Id, attempts[1].Id);
        Assert.DoesNotContain(attempts, item => item.Id == otherStudent.Id);
    }

    [Fact]
    public async Task GetAttempt_ReturnsAttemptOnlyToItsOwner()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var attempt = CreateStoredAttempt(
            Guid.NewGuid(),
            "student-1",
            DateTimeOffset.UtcNow.AddHours(1));
        await SeedAttemptsAsync(factory, attempt);
        var service = CreateService(factory, new StubTeachingServiceClient());

        var ownedResult = await service.GetAttemptAsync(
            attempt.Id,
            "student-1",
            CancellationToken.None);
        var foreignResult = await service.GetAttemptAsync(
            attempt.Id,
            "student-2",
            CancellationToken.None);

        Assert.NotNull(ownedResult);
        Assert.Equal(attempt.Id, ownedResult.Id);
        Assert.Null(foreignResult);
    }

    private static CourseTestDto CreatePublishedTest(
        Guid id,
        int versionNumber,
        DateTimeOffset deadline,
        int timeLimitMinutes,
        params string[] taskIds)
    {
        return new CourseTestDto
        {
            Id = id.ToString("D"),
            Status = CourseTestStatus.Published,
            VersionNumber = versionNumber,
            Deadline = deadline,
            TimeLimitMinutes = timeLimitMinutes,
            Tasks = taskIds.Select(taskId => new TestTaskDto { Id = taskId }).ToList()
        };
    }

    private static AttemptLifecycleService CreateService(
        TestAttemptDbContextFactory factory,
        StubTeachingServiceClient teachingClient,
        AttemptInputLimitsOptions? inputLimits = null)
    {
        return new AttemptLifecycleService(
            factory,
            teachingClient,
            Microsoft.Extensions.Options.Options.Create(
                inputLimits ?? new AttemptInputLimitsOptions()));
    }

    private static async Task AssertAttemptWasNotChangedAsync(
        TestAttemptDbContextFactory factory,
        Guid attemptId,
        string expectedAnswersJson)
    {
        await using var dbContext = factory.CreateDbContext();
        var storedAttempt = await dbContext.TestAttempts.SingleAsync(item => item.Id == attemptId);
        Assert.Equal(expectedAnswersJson, storedAttempt.AnswersJson);
        Assert.Equal(TestAttemptStatus.InProgress, storedAttempt.Status);
        Assert.Equal(0, storedAttempt.StateRevision);
        Assert.Null(storedAttempt.SubmittedAt);
        Assert.Empty(dbContext.AttemptSubmissionOutboxMessages);
    }

    private static TestAttempt CreateStoredAttempt(
        Guid testId,
        string studentUserId,
        DateTimeOffset endsAt,
        DateTimeOffset? startedAt = null)
    {
        return new TestAttempt
        {
            Id = Guid.NewGuid(),
            TestId = testId.ToString("D"),
            TestRevision = 1,
            StudentUserId = studentUserId,
            Status = TestAttemptStatus.InProgress,
            StartedAt = startedAt ?? DateTimeOffset.UtcNow.AddMinutes(-5),
            EndsAt = endsAt,
            AnswersJson = "{}",
            AllowedTaskIdsJson = "[\"task-a\"]"
        };
    }

    private static AttemptSubmissionOutboxMessage CreatePublishedOutbox(TestAttempt attempt)
    {
        return new AttemptSubmissionOutboxMessage
        {
            Id = Guid.NewGuid(),
            AttemptId = attempt.Id,
            OccurredAt = attempt.SubmittedAt ?? DateTimeOffset.UtcNow.AddMinutes(-1),
            PayloadJson = "{}",
            PublishedAt = DateTimeOffset.UtcNow
        };
    }

    private static async Task SeedAttemptsAsync(
        TestAttemptDbContextFactory factory,
        params TestAttempt[] attempts)
    {
        await using var dbContext = factory.CreateDbContext();
        dbContext.TestAttempts.AddRange(attempts);
        await dbContext.SaveChangesAsync();
    }
}
