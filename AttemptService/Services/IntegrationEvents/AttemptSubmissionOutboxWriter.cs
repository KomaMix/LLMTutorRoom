using System.Text.Json;
using AttemptService.Data;
using AttemptService.Models;
using Microsoft.EntityFrameworkCore;
using ReviewService.Contracts.Events;
using Shared.Observability;

namespace AttemptService.Services.IntegrationEvents;

public class AttemptSubmissionOutboxWriter
{
    private const int MaxStudentNameLength = 500;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> StageAsync(
        AttemptDbContext dbContext,
        TestAttempt attempt,
        CancellationToken cancellationToken)
    {
        var existingOutbox = await dbContext.AttemptSubmissionOutboxMessages
            .SingleOrDefaultAsync(
                message => message.AttemptId == attempt.Id,
                cancellationToken);
        var eventId = existingOutbox?.Id ?? Guid.NewGuid();
        var submittedAt = attempt.SubmittedAt
            ?? throw new InvalidOperationException("Submitted attempt must have SubmittedAt.");
        var message = new AttemptSubmittedV1
        {
            EventId = eventId,
            AttemptId = attempt.Id,
            AssignmentId = attempt.AssignmentId,
            GroupId = attempt.GroupId,
            TestId = attempt.TestId,
            TestRevision = attempt.TestRevision,
            StudentUserId = attempt.StudentUserId,
            StudentName = NormalizeStudentName(attempt.StudentName),
            Answers = DeserializeAnswers(attempt.AnswersJson),
            SubmittedAt = submittedAt
        };
        var payload = JsonSerializer.Serialize(message, JsonOptions);
        var traceContext = ProjectTelemetry.CaptureCurrentTraceContext();

        if (existingOutbox is null)
        {
            dbContext.AttemptSubmissionOutboxMessages.Add(new AttemptSubmissionOutboxMessage
            {
                Id = eventId,
                AttemptId = attempt.Id,
                OccurredAt = submittedAt,
                PayloadJson = payload,
                TraceParent = traceContext.TraceParent,
                TraceState = traceContext.TraceState
            });
            return true;
        }

        if (existingOutbox.PublishedAt is not null)
        {
            return false;
        }

        existingOutbox.OccurredAt = submittedAt;
        existingOutbox.PayloadJson = payload;
        existingOutbox.PublishAttempts = 0;
        existingOutbox.NextPublishAt = null;
        existingOutbox.LastError = string.Empty;
        if (traceContext.TraceParent is not null)
        {
            existingOutbox.TraceParent = traceContext.TraceParent;
            existingOutbox.TraceState = traceContext.TraceState;
        }

        return true;
    }

    public static string NormalizeStudentName(string? studentUserName)
    {
        var normalizedName = string.IsNullOrWhiteSpace(studentUserName)
            ? "Студент"
            : studentUserName.Trim();
        return normalizedName.Length <= MaxStudentNameLength
            ? normalizedName
            : normalizedName[..MaxStudentNameLength];
    }

    private static Dictionary<string, string> DeserializeAnswers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
            ?? new Dictionary<string, string>();
    }
}
