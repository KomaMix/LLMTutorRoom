using System.Text.Json;
using AttemptService.Contracts.Enums;
using AttemptService.Models;
using AttemptService.Options;
using AttemptService.Services;
using AttemptService.Services.IntegrationEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReviewService.Contracts.Events;

namespace AttemptService.Tests;

public class AttemptDeadlineSubmissionServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SubmitDueAttempts_DrainsBatchesAndCreatesSubmissionEvents()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var now = DateTimeOffset.UtcNow;
        var dueAttempts = Enumerable.Range(1, 5)
            .Select(index => CreateAttempt(
                Guid.NewGuid(),
                TestAttemptStatus.InProgress,
                now.AddMinutes(-index),
                stateRevision: index))
            .ToList();
        var originalRevisions = dueAttempts.ToDictionary(
            attempt => attempt.Id,
            attempt => attempt.StateRevision);
        var futureAttempt = CreateAttempt(
            Guid.NewGuid(),
            TestAttemptStatus.InProgress,
            now.AddMinutes(20),
            stateRevision: 3);
        var submittedAttempt = CreateAttempt(
            Guid.NewGuid(),
            TestAttemptStatus.Submitted,
            now.AddMinutes(-20),
            stateRevision: 4);

        await using (var dbContext = factory.CreateDbContext())
        {
            dbContext.TestAttempts.AddRange(dueAttempts);
            dbContext.TestAttempts.Add(futureAttempt);
            dbContext.TestAttempts.Add(submittedAttempt);
            await dbContext.SaveChangesAsync();
        }

        var service = new AttemptDeadlineSubmissionService(
            factory,
            new AttemptSubmissionOutboxWriter(),
            Microsoft.Extensions.Options.Options.Create(
                new AttemptDeadlineSubmissionOptions { BatchSize = 2 }),
            NullLogger<AttemptDeadlineSubmissionService>.Instance);

        var submittedCount = await service.SubmitDueAttemptsAsync(CancellationToken.None);

        Assert.Equal(5, submittedCount);
        await using var verificationContext = factory.CreateDbContext();
        var dueAttemptIds = dueAttempts.Select(attempt => attempt.Id).ToList();
        var storedDueAttempts = await verificationContext.TestAttempts
            .Where(attempt => dueAttemptIds.Contains(attempt.Id))
            .OrderBy(attempt => attempt.Id)
            .ToListAsync();
        Assert.All(storedDueAttempts, attempt =>
        {
            Assert.Equal(TestAttemptStatus.Submitted, attempt.Status);
            Assert.Equal(AttemptSubmissionReason.Deadline, attempt.SubmissionReason);
            Assert.Equal(attempt.EndsAt, attempt.SubmittedAt);
            Assert.Equal(originalRevisions[attempt.Id] + 1, attempt.StateRevision);
        });

        var outboxMessages = await verificationContext.AttemptSubmissionOutboxMessages
            .OrderBy(message => message.AttemptId)
            .ToListAsync();
        Assert.Equal(5, outboxMessages.Count);
        foreach (var outboxMessage in outboxMessages)
        {
            var attempt = storedDueAttempts.Single(item => item.Id == outboxMessage.AttemptId);
            var submissionEvent = Assert.IsType<AttemptSubmittedV1>(
                JsonSerializer.Deserialize<AttemptSubmittedV1>(outboxMessage.PayloadJson, JsonOptions));
            Assert.Equal(attempt.EndsAt, outboxMessage.OccurredAt);
            Assert.Equal(attempt.EndsAt, submissionEvent.SubmittedAt);
            Assert.Equal(attempt.StudentUserId, submissionEvent.StudentUserId);
            Assert.Equal(attempt.StudentName, submissionEvent.StudentName);
            Assert.Equal("saved-answer", submissionEvent.Answers["task-a"]);
        }

        var storedFutureAttempt = await verificationContext.TestAttempts
            .SingleAsync(attempt => attempt.Id == futureAttempt.Id);
        var storedSubmittedAttempt = await verificationContext.TestAttempts
            .SingleAsync(attempt => attempt.Id == submittedAttempt.Id);
        Assert.Equal(TestAttemptStatus.InProgress, storedFutureAttempt.Status);
        Assert.Equal(3, storedFutureAttempt.StateRevision);
        Assert.Equal(TestAttemptStatus.Submitted, storedSubmittedAttempt.Status);
        Assert.Equal(4, storedSubmittedAttempt.StateRevision);
    }

    private static TestAttempt CreateAttempt(
        Guid testId,
        TestAttemptStatus status,
        DateTimeOffset endsAt,
        int stateRevision)
    {
        return new TestAttempt
        {
            Id = Guid.NewGuid(),
            AssignmentId = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            TestId = testId.ToString("D"),
            TestRevision = 1,
            TeacherUserId = "teacher-1",
            StudentUserId = "student-1",
            StudentName = "Alice Student",
            Status = status,
            StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
            EndsAt = endsAt,
            SubmittedAt = status == TestAttemptStatus.Submitted
                ? DateTimeOffset.UtcNow.AddMinutes(-10)
                : null,
            SubmissionReason = status == TestAttemptStatus.Submitted
                ? AttemptSubmissionReason.Student
                : null,
            AnswersJson = "{\"task-a\":\"saved-answer\"}",
            AllowedTaskIdsJson = "[\"task-a\"]",
            StateRevision = stateRevision
        };
    }
}
