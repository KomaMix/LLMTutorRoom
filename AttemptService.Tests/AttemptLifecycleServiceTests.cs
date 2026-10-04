using System.Diagnostics;
using System.Text.Json;
using AttemptService.Contracts.Enums;
using AttemptService.Contracts.Responses;
using AttemptService.Enums;
using AttemptService.Models;
using AttemptService.Options;
using AttemptService.Services;
using AttemptService.Services.IntegrationEvents;
using Microsoft.EntityFrameworkCore;
using ReviewService.Contracts.Events;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;
using TeachingService.Contracts.Responses;

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
            timeLimitMinutes: 45,
            "task-a");
        var distantDeadlineAssignment = CreateAssignment(
            distantDeadlineTest,
            DateTimeOffset.UtcNow.AddHours(3));
        var nearDeadline = DateTimeOffset.UtcNow.AddMinutes(5);
        var nearDeadlineTest = CreatePublishedTest(
            Guid.NewGuid(),
            versionNumber: 4,
            timeLimitMinutes: 60,
            "task-b");
        var nearDeadlineAssignment = CreateAssignment(nearDeadlineTest, nearDeadline);
        teachingClient.Assignments.Add(distantDeadlineAssignment.Id, distantDeadlineAssignment);
        teachingClient.Assignments.Add(nearDeadlineAssignment.Id, nearDeadlineAssignment);
        var service = CreateService(factory, teachingClient);

        var beforeStart = DateTimeOffset.UtcNow;
        var timeLimitedResult = await service.StartAttemptAsync(
            distantDeadlineAssignment.Id,
            "student-1",
            "Alice Student",
            CancellationToken.None);
        var afterStart = DateTimeOffset.UtcNow;
        var deadlineLimitedResult = await service.StartAttemptAsync(
            nearDeadlineAssignment.Id,
            "student-1",
            "Alice Student",
            CancellationToken.None);

        Assert.Equal(AttemptOperationStatus.Created, timeLimitedResult.Status);
        var timeLimitedAttempt = Assert.IsType<TestAttemptResponse>(timeLimitedResult.Value);
        Assert.NotEqual(Guid.Empty, timeLimitedAttempt.Id);
        Assert.Equal(distantDeadlineAssignment.Id, timeLimitedAttempt.AssignmentId);
        Assert.Equal(distantDeadlineAssignment.GroupId, timeLimitedAttempt.GroupId);
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
        Assert.Equal(nearDeadlineAssignment.Id, teachingClient.LastAssignmentId);
        Assert.Equal("student-1", teachingClient.LastStudentUserId);
        await using var dbContext = factory.CreateDbContext();
        var storedAttempt = await dbContext.TestAttempts
            .SingleAsync(item => item.Id == timeLimitedAttempt.Id);
        Assert.Equal("Alice Student", storedAttempt.StudentName);
    }

    [Fact]
    public async Task StartAttempt_WhenAssignmentIsNotAvailable_ReturnsNotFoundWithoutCreatingAttempt()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var teachingClient = new StubTeachingServiceClient();
        var test = CreatePublishedTest(
            Guid.NewGuid(),
            versionNumber: 8,
            timeLimitMinutes: 30,
            "task-a");
        var assignment = CreateAssignment(
            test,
            DateTimeOffset.UtcNow.AddHours(2),
            DateTimeOffset.UtcNow.AddMinutes(10));
        teachingClient.Assignments.Add(assignment.Id, assignment);
        var service = CreateService(factory, teachingClient);

        var result = await service.StartAttemptAsync(
            assignment.Id,
            "student-1",
            "Alice Student",
            CancellationToken.None);

        Assert.Equal(AttemptOperationStatus.NotFound, result.Status);
        Assert.Null(result.Value);
        await using var dbContext = factory.CreateDbContext();
        Assert.Empty(dbContext.TestAttempts);
    }

    [Fact]
    public async Task StartAttempt_WhenAssignmentDeadlinePassed_ReturnsNotFoundWithoutCreatingAttempt()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var teachingClient = new StubTeachingServiceClient();
        var test = CreatePublishedTest(
            Guid.NewGuid(),
            versionNumber: 3,
            timeLimitMinutes: 30,
            "task-a");
        var assignment = CreateAssignment(test, DateTimeOffset.UtcNow.AddMinutes(-1));
        teachingClient.Assignments.Add(assignment.Id, assignment);
        var service = CreateService(factory, teachingClient);

        var result = await service.StartAttemptAsync(
            assignment.Id,
            "student-1",
            "Alice Student",
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
            timeLimitMinutes: 20,
            "task-a");
        var assignment = CreateAssignment(test, DateTimeOffset.UtcNow.AddHours(1));
        teachingClient.Assignments.Add(assignment.Id, assignment);
        var service = CreateService(factory, teachingClient);

        var firstResult = await service.StartAttemptAsync(
            assignment.Id,
            "student-1",
            "Alice Student",
            CancellationToken.None);
        var repeatedResult = await service.StartAttemptAsync(
            assignment.Id,
            "student-1",
            "Alice Student",
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
    public async Task StartAttempt_AllowsSameTestThroughDifferentAssignments()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var teachingClient = new StubTeachingServiceClient();
        var test = CreatePublishedTest(
            Guid.NewGuid(),
            versionNumber: 2,
            timeLimitMinutes: 20,
            "task-a");
        var firstAssignment = CreateAssignment(test, DateTimeOffset.UtcNow.AddHours(1));
        var secondAssignment = CreateAssignment(test, DateTimeOffset.UtcNow.AddHours(2));
        teachingClient.Assignments.Add(firstAssignment.Id, firstAssignment);
        teachingClient.Assignments.Add(secondAssignment.Id, secondAssignment);
        var service = CreateService(factory, teachingClient);

        var firstResult = await service.StartAttemptAsync(
            firstAssignment.Id,
            "student-1",
            "Alice Student",
            CancellationToken.None);
        var secondResult = await service.StartAttemptAsync(
            secondAssignment.Id,
            "student-1",
            "Alice Student",
            CancellationToken.None);

        Assert.Equal(AttemptOperationStatus.Created, firstResult.Status);
        Assert.Equal(AttemptOperationStatus.Created, secondResult.Status);
        Assert.NotEqual(firstResult.Value!.Id, secondResult.Value!.Id);
        await using var dbContext = factory.CreateDbContext();
        Assert.Equal(2, await dbContext.TestAttempts.CountAsync());
    }

    [Fact]
    public async Task SaveAnswers_KeepsOnlyTasksCapturedWhenAttemptStarted()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var teachingClient = new StubTeachingServiceClient();
        var test = CreatePublishedTest(
            Guid.NewGuid(),
            versionNumber: 3,
            timeLimitMinutes: 30,
            "task-a",
            "task-b",
            "task-a");
        var assignment = CreateAssignment(test, DateTimeOffset.UtcNow.AddHours(1));
        teachingClient.Assignments.Add(assignment.Id, assignment);
        var service = CreateService(factory, teachingClient);
        var startResult = await service.StartAttemptAsync(
            assignment.Id,
            "student-1",
            "Alice Student",
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
    public async Task SaveAnswers_WhenDeadlinePassed_SubmitsSavedAnswersAndCreatesOutboxMessage()
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
        Assert.Equal(TestAttemptStatus.Submitted, currentAttempt.Status);
        Assert.Equal(AttemptSubmissionReason.Deadline, currentAttempt.SubmissionReason);
        Assert.Equal(attempt.EndsAt, currentAttempt.SubmittedAt);
        Assert.Equal("old-answer", currentAttempt.Answers["task-a"]);

        await using var dbContext = factory.CreateDbContext();
        var storedAttempt = await dbContext.TestAttempts.SingleAsync();
        var outboxMessage = await dbContext.AttemptSubmissionOutboxMessages.SingleAsync();
        var submissionEvent = Assert.IsType<AttemptSubmittedV1>(
            JsonSerializer.Deserialize<AttemptSubmittedV1>(outboxMessage.PayloadJson, JsonOptions));
        Assert.Equal(TestAttemptStatus.Submitted, storedAttempt.Status);
        Assert.Equal(AttemptSubmissionReason.Deadline, storedAttempt.SubmissionReason);
        Assert.Equal(storedAttempt.EndsAt, storedAttempt.SubmittedAt);
        Assert.Equal(1, storedAttempt.StateRevision);
        Assert.Equal("old-answer", submissionEvent.Answers["task-a"]);
        Assert.Equal("Alice Student", submissionEvent.StudentName);
        Assert.Equal(storedAttempt.EndsAt, submissionEvent.SubmittedAt);
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
            timeLimitMinutes: 30,
            "task-a",
            "task-b");
        var assignment = CreateAssignment(test, DateTimeOffset.UtcNow.AddHours(1));
        teachingClient.Assignments.Add(assignment.Id, assignment);
        var service = CreateService(factory, teachingClient);
        var startResult = await service.StartAttemptAsync(
            assignment.Id,
            "student-1",
            "Alice Student",
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

        using var submissionActivity = new Activity("submit-attempt")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        submissionActivity.TraceStateString = "vendor=test";
        var expectedTraceParent = submissionActivity.Id;
        var expectedTraceState = submissionActivity.TraceStateString;
        var firstSubmit = await service.SubmitAttemptAsync(
            attempt.Id,
            "student-1",
            "  Alice Student  ",
            CancellationToken.None);
        submissionActivity.Stop();

        Assert.Equal(AttemptOperationStatus.Success, firstSubmit.Status);
        var submittedAttempt = Assert.IsType<TestAttemptResponse>(firstSubmit.Value);
        Assert.Equal(TestAttemptStatus.Submitted, submittedAttempt.Status);
        Assert.Equal(AttemptSubmissionReason.Student, submittedAttempt.SubmissionReason);
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
            Assert.Equal(AttemptSubmissionReason.Student, storedAttempt.SubmissionReason);
            Assert.Equal(2, storedAttempt.StateRevision);
            Assert.Equal(storedAttempt.SubmittedAt, outboxMessage.OccurredAt);
            Assert.Equal(expectedTraceParent, outboxMessage.TraceParent);
            Assert.Equal(expectedTraceState, outboxMessage.TraceState);
            Assert.Equal(outboxMessage.Id, submissionEvent.EventId);
            Assert.Equal(storedAttempt.Id, submissionEvent.AttemptId);
            Assert.Equal(assignment.Id, submissionEvent.AssignmentId);
            Assert.Equal(assignment.GroupId, submissionEvent.GroupId);
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
        Assert.Equal(expectedTraceParent, repeatedOutbox.TraceParent);
        Assert.Equal(expectedTraceState, repeatedOutbox.TraceState);
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
    public async Task SubmitAttempt_WhenDeadlinePassed_AutomaticallySubmitsSavedAnswers()
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

        Assert.Equal(AttemptOperationStatus.Success, result.Status);
        var currentAttempt = Assert.IsType<TestAttemptResponse>(result.Value);
        Assert.Equal(TestAttemptStatus.Submitted, currentAttempt.Status);
        Assert.Equal(AttemptSubmissionReason.Deadline, currentAttempt.SubmissionReason);
        Assert.Equal(attempt.EndsAt, currentAttempt.SubmittedAt);

        await using var dbContext = factory.CreateDbContext();
        var storedAttempt = await dbContext.TestAttempts.SingleAsync();
        var outboxMessage = await dbContext.AttemptSubmissionOutboxMessages.SingleAsync();
        var submissionEvent = Assert.IsType<AttemptSubmittedV1>(
            JsonSerializer.Deserialize<AttemptSubmittedV1>(outboxMessage.PayloadJson, JsonOptions));
        Assert.Equal(TestAttemptStatus.Submitted, storedAttempt.Status);
        Assert.Equal(AttemptSubmissionReason.Deadline, storedAttempt.SubmissionReason);
        Assert.Equal(storedAttempt.EndsAt, storedAttempt.SubmittedAt);
        Assert.Equal(1, storedAttempt.StateRevision);
        Assert.Equal(storedAttempt.EndsAt, submissionEvent.SubmittedAt);
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
        int timeLimitMinutes,
        params string[] taskIds)
    {
        return new CourseTestDto
        {
            Id = id.ToString("D"),
            TeacherUserId = "teacher-1",
            Status = CourseTestStatus.Published,
            VersionNumber = versionNumber,
            TimeLimitMinutes = timeLimitMinutes,
            Tasks = taskIds.Select(taskId => new TestTaskDto { Id = taskId }).ToList()
        };
    }

    private static TestAssignmentResponse CreateAssignment(
        CourseTestDto test,
        DateTimeOffset deadline,
        DateTimeOffset? availableFrom = null)
    {
        return new TestAssignmentResponse
        {
            Id = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            GroupName = "Group 1",
            TeacherUserId = test.TeacherUserId,
            AvailableFrom = availableFrom ?? DateTimeOffset.UtcNow.AddMinutes(-10),
            Deadline = deadline,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20),
            Test = test
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
            new AttemptSubmissionOutboxWriter(),
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
            AssignmentId = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            TestId = testId.ToString("D"),
            TestRevision = 1,
            TeacherUserId = "teacher-1",
            StudentUserId = studentUserId,
            StudentName = "Alice Student",
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
