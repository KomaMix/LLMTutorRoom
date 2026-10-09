using System.Text.Json;
using AttemptService.Contracts.Enums;
using AttemptService.Contracts.Responses;
using AttemptService.Data;
using AttemptService.Enums;
using AttemptService.Interfaces;
using AttemptService.Models;
using AttemptService.Options;
using AttemptService.Services.IntegrationEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AttemptService.Services;

public class AttemptLifecycleService : IAttemptLifecycleService
{
    private const int MaxMutationAttempts = 5;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<AttemptDbContext> _dbContextFactory;
    private readonly ITeachingServiceClient _teachingServiceClient;
    private readonly AttemptSubmissionOutboxWriter _outboxWriter;
    private readonly AttemptInputLimitsOptions _inputLimits;

    public AttemptLifecycleService(
        IDbContextFactory<AttemptDbContext> dbContextFactory,
        ITeachingServiceClient teachingServiceClient,
        AttemptSubmissionOutboxWriter outboxWriter,
        IOptions<AttemptInputLimitsOptions> inputLimitsOptions)
    {
        _dbContextFactory = dbContextFactory;
        _teachingServiceClient = teachingServiceClient;
        _outboxWriter = outboxWriter;
        _inputLimits = inputLimitsOptions.Value;
    }

    public async Task<List<TestAttemptResponse>> GetStudentAttemptsAsync(
        string studentUserId,
        CancellationToken cancellationToken)
    {
        await SubmitDueStudentAttemptsAsync(studentUserId, cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var attempts = await dbContext.TestAttempts
            .AsNoTracking()
            .Where(attempt => attempt.StudentUserId == studentUserId)
            .OrderByDescending(attempt => attempt.StartedAt)
            .ToListAsync(cancellationToken);

        if (attempts.Any(attempt => attempt.Status == TestAttemptStatus.InProgress))
        {
            var assignments = await _teachingServiceClient.GetStudentAssignmentsAsync(
                studentUserId,
                cancellationToken);
            var availableAssignmentIds = assignments.Select(assignment => assignment.Id).ToHashSet();
            attempts = attempts
                .Where(attempt => attempt.Status != TestAttemptStatus.InProgress
                    || availableAssignmentIds.Contains(attempt.AssignmentId))
                .ToList();
        }

        return attempts.Select(ToResponse).ToList();
    }

    public async Task<TestAttemptResponse?> GetAttemptAsync(
        Guid attemptId,
        string studentUserId,
        CancellationToken cancellationToken)
    {
        await SubmitDueStudentAttemptsAsync(studentUserId, cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var attempt = await dbContext.TestAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == attemptId && item.StudentUserId == studentUserId,
                cancellationToken);
        if (attempt is null)
        {
            return null;
        }

        if (attempt.Status == TestAttemptStatus.InProgress
            && await _teachingServiceClient.GetStudentAssignmentAsync(
                attempt.AssignmentId,
                studentUserId,
                cancellationToken) is null)
        {
            return null;
        }

        return ToResponse(attempt);
    }

    public async Task<List<TestAttemptResponse>> GetTeacherAttemptsAsync(
        string teacherUserId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var attempts = await dbContext.TestAttempts
            .AsNoTracking()
            .Where(attempt => attempt.TeacherUserId == teacherUserId)
            .OrderByDescending(attempt => attempt.StartedAt)
            .ToListAsync(cancellationToken);

        return attempts.Select(ToResponse).ToList();
    }

    public async Task<Result<AttemptOperationStatus, TestAttemptResponse>> StartAttemptAsync(
        Guid assignmentId,
        string studentUserId,
        string studentUserName,
        CancellationToken cancellationToken)
    {
        await SubmitDueStudentAttemptsAsync(studentUserId, cancellationToken);

        var assignment = await _teachingServiceClient.GetStudentAssignmentAsync(
            assignmentId,
            studentUserId,
            cancellationToken);
        if (assignment is null || assignment.Test.VersionNumber <= 0)
        {
            return new Result<AttemptOperationStatus, TestAttemptResponse>
            {
                Status = AttemptOperationStatus.NotFound
            };
        }

        await using (var readContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var existingAttempt = await readContext.TestAttempts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    attempt => attempt.AssignmentId == assignmentId
                        && attempt.StudentUserId == studentUserId,
                    cancellationToken);
            if (existingAttempt is not null)
            {
                return new Result<AttemptOperationStatus, TestAttemptResponse>
                {
                    Status = AttemptOperationStatus.Success,
                    Value = ToResponse(existingAttempt)
                };
            }
        }

        var now = DateTimeOffset.UtcNow;
        if (assignment.AvailableFrom > now || assignment.Deadline <= now)
        {
            return new Result<AttemptOperationStatus, TestAttemptResponse>
            {
                Status = AttemptOperationStatus.NotFound
            };
        }

        var attempt = new TestAttempt
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment.Id,
            GroupId = assignment.GroupId,
            TestId = assignment.Test.Id,
            TestRevision = assignment.Test.VersionNumber,
            TeacherUserId = assignment.TeacherUserId,
            StudentUserId = studentUserId,
            StudentName = AttemptSubmissionOutboxWriter.NormalizeStudentName(studentUserName),
            Status = TestAttemptStatus.InProgress,
            StartedAt = now,
            EndsAt = Min(now.AddMinutes(assignment.Test.TimeLimitMinutes), assignment.Deadline),
            AnswersJson = "{}",
            AllowedTaskIdsJson = SerializeTaskIds(assignment.Test.Tasks.Select(task => task.Id))
        };

        await using var writeContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        writeContext.TestAttempts.Add(attempt);
        try
        {
            await writeContext.SaveChangesAsync(cancellationToken);
            return new Result<AttemptOperationStatus, TestAttemptResponse>
            {
                Status = AttemptOperationStatus.Created,
                Value = ToResponse(attempt)
            };
        }
        catch (DbUpdateException exception) when (IsDuplicateAttempt(exception))
        {
            var concurrentlyCreatedAttempt = await LoadAttemptAsync(
                assignmentId,
                studentUserId,
                cancellationToken);
            return concurrentlyCreatedAttempt is null
                ? new Result<AttemptOperationStatus, TestAttemptResponse>
                {
                    Status = AttemptOperationStatus.Conflict
                }
                : new Result<AttemptOperationStatus, TestAttemptResponse>
                {
                    Status = AttemptOperationStatus.Success,
                    Value = ToResponse(concurrentlyCreatedAttempt)
                };
        }
    }

    public async Task<Result<AttemptOperationStatus, TestAttemptResponse>> SaveAnswersAsync(
        Guid attemptId,
        string studentUserId,
        Dictionary<string, string> answers,
        CancellationToken cancellationToken)
    {
        var assignmentAccessChecked = false;
        for (var mutationAttempt = 1; mutationAttempt <= MaxMutationAttempts; mutationAttempt++)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var attempt = await FindStudentAttemptAsync(
                dbContext,
                attemptId,
                studentUserId,
                cancellationToken);
            if (attempt is null)
            {
                return new Result<AttemptOperationStatus, TestAttemptResponse>
                {
                    Status = AttemptOperationStatus.NotFound
                };
            }

            if (!assignmentAccessChecked)
            {
                var assignment = await _teachingServiceClient.GetStudentAssignmentAsync(
                    attempt.AssignmentId,
                    studentUserId,
                    cancellationToken);
                if (assignment is null)
                {
                    return new Result<AttemptOperationStatus, TestAttemptResponse>
                    {
                        Status = AttemptOperationStatus.NotFound
                    };
                }

                assignmentAccessChecked = true;
            }

            if (attempt.Status != TestAttemptStatus.InProgress)
            {
                return new Result<AttemptOperationStatus, TestAttemptResponse>
                {
                    Status = AttemptOperationStatus.Conflict,
                    Value = ToResponse(attempt)
                };
            }

            if (attempt.EndsAt <= DateTimeOffset.UtcNow)
            {
                ApplyDeadlineSubmission(attempt);
                var submissionEventStaged = await _outboxWriter.StageAsync(
                    dbContext,
                    attempt,
                    cancellationToken);
                if (!submissionEventStaged)
                {
                    return await CreateConcurrencyConflictAsync(
                        attemptId,
                        studentUserId,
                        cancellationToken);
                }

                IncrementStateRevision(attempt);
                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return new Result<AttemptOperationStatus, TestAttemptResponse>
                    {
                        Status = AttemptOperationStatus.Conflict,
                        Value = ToResponse(attempt)
                    };
                }
                catch (DbUpdateConcurrencyException) when (mutationAttempt < MaxMutationAttempts)
                {
                    continue;
                }
                catch (DbUpdateException exception) when (
                    IsDuplicateSubmissionEvent(exception)
                    && mutationAttempt < MaxMutationAttempts)
                {
                    continue;
                }
                catch (DbUpdateConcurrencyException)
                {
                    return await CreateConcurrencyConflictAsync(
                        attemptId,
                        studentUserId,
                        cancellationToken);
                }
                catch (DbUpdateException exception) when (IsDuplicateSubmissionEvent(exception))
                {
                    return await CreateConcurrencyConflictAsync(
                        attemptId,
                        studentUserId,
                        cancellationToken);
                }
            }

            if (!AreAnswersWithinLimits(answers))
            {
                return new Result<AttemptOperationStatus, TestAttemptResponse>
                {
                    Status = AttemptOperationStatus.InvalidInput
                };
            }

            attempt.AnswersJson = SerializeAnswers(FilterAnswers(attempt, answers));
            IncrementStateRevision(attempt);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return new Result<AttemptOperationStatus, TestAttemptResponse>
                {
                    Status = AttemptOperationStatus.Success,
                    Value = ToResponse(attempt)
                };
            }
            catch (DbUpdateConcurrencyException) when (mutationAttempt < MaxMutationAttempts)
            {
                continue;
            }
            catch (DbUpdateConcurrencyException)
            {
                return await CreateConcurrencyConflictAsync(
                    attemptId,
                    studentUserId,
                    cancellationToken);
            }
        }

        return new Result<AttemptOperationStatus, TestAttemptResponse>
        {
            Status = AttemptOperationStatus.Conflict
        };
    }

    public async Task<Result<AttemptOperationStatus, TestAttemptResponse>> SubmitAttemptAsync(
        Guid attemptId,
        string studentUserId,
        string studentUserName,
        CancellationToken cancellationToken)
    {
        var assignmentAccessChecked = false;
        for (var mutationAttempt = 1; mutationAttempt <= MaxMutationAttempts; mutationAttempt++)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var attempt = await FindStudentAttemptAsync(
                dbContext,
                attemptId,
                studentUserId,
                cancellationToken);
            if (attempt is null)
            {
                return new Result<AttemptOperationStatus, TestAttemptResponse>
                {
                    Status = AttemptOperationStatus.NotFound
                };
            }

            if (!assignmentAccessChecked)
            {
                var assignment = await _teachingServiceClient.GetStudentAssignmentAsync(
                    attempt.AssignmentId,
                    studentUserId,
                    cancellationToken);
                if (assignment is null)
                {
                    return new Result<AttemptOperationStatus, TestAttemptResponse>
                    {
                        Status = AttemptOperationStatus.NotFound
                    };
                }

                assignmentAccessChecked = true;
            }

            if (attempt.Status == TestAttemptStatus.Submitted)
            {
                return new Result<AttemptOperationStatus, TestAttemptResponse>
                {
                    Status = AttemptOperationStatus.Success,
                    Value = ToResponse(attempt)
                };
            }

            var now = DateTimeOffset.UtcNow;
            attempt.StudentName = AttemptSubmissionOutboxWriter.NormalizeStudentName(studentUserName);
            if (attempt.EndsAt <= now)
            {
                ApplyDeadlineSubmission(attempt);
            }
            else
            {
                ApplyStudentSubmission(attempt, now);
            }

            var submissionEventStaged = await _outboxWriter.StageAsync(
                dbContext,
                attempt,
                cancellationToken);
            if (!submissionEventStaged)
            {
                var raceResult = await ResolveSubmissionRaceAsync(
                    attemptId,
                    studentUserId,
                    cancellationToken);
                if (raceResult.Status != AttemptOperationStatus.Conflict
                    || raceResult.Value is not null
                    || mutationAttempt == MaxMutationAttempts)
                {
                    return raceResult;
                }

                continue;
            }
            IncrementStateRevision(attempt);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return new Result<AttemptOperationStatus, TestAttemptResponse>
                {
                    Status = AttemptOperationStatus.Success,
                    Value = ToResponse(attempt)
                };
            }
            catch (DbUpdateConcurrencyException) when (mutationAttempt < MaxMutationAttempts)
            {
                continue;
            }
            catch (DbUpdateException exception) when (
                IsDuplicateSubmissionEvent(exception)
                && mutationAttempt < MaxMutationAttempts)
            {
                continue;
            }
            catch (DbUpdateConcurrencyException)
            {
                return await ResolveSubmissionRaceAsync(
                    attemptId,
                    studentUserId,
                    cancellationToken);
            }
            catch (DbUpdateException exception) when (IsDuplicateSubmissionEvent(exception))
            {
                return await ResolveSubmissionRaceAsync(
                    attemptId,
                    studentUserId,
                    cancellationToken);
            }
        }

        return new Result<AttemptOperationStatus, TestAttemptResponse>
        {
            Status = AttemptOperationStatus.Conflict
        };
    }

    private async Task SubmitDueStudentAttemptsAsync(
        string studentUserId,
        CancellationToken cancellationToken)
    {
        for (var mutationAttempt = 1; mutationAttempt <= MaxMutationAttempts; mutationAttempt++)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var attempts = await dbContext.TestAttempts
                .Where(attempt => attempt.StudentUserId == studentUserId
                    && attempt.Status == TestAttemptStatus.InProgress
                    && attempt.EndsAt <= now)
                .ToListAsync(cancellationToken);
            if (attempts.Count == 0)
            {
                return;
            }

            var stagedAttemptCount = 0;
            foreach (var attempt in attempts)
            {
                ApplyDeadlineSubmission(attempt);
                if (!await _outboxWriter.StageAsync(dbContext, attempt, cancellationToken))
                {
                    dbContext.Entry(attempt).State = EntityState.Unchanged;
                    continue;
                }

                IncrementStateRevision(attempt);
                stagedAttemptCount++;
            }

            if (stagedAttemptCount == 0)
            {
                return;
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException) when (mutationAttempt < MaxMutationAttempts)
            {
                continue;
            }
            catch (DbUpdateException exception) when (
                IsDuplicateSubmissionEvent(exception)
                && mutationAttempt < MaxMutationAttempts)
            {
                continue;
            }
            catch (DbUpdateConcurrencyException)
            {
                return;
            }
            catch (DbUpdateException exception) when (IsDuplicateSubmissionEvent(exception))
            {
                return;
            }
        }
    }

    private async Task<Result<AttemptOperationStatus, TestAttemptResponse>> CreateConcurrencyConflictAsync(
        Guid attemptId,
        string studentUserId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var currentAttempt = await dbContext.TestAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                attempt => attempt.Id == attemptId
                    && attempt.StudentUserId == studentUserId,
                cancellationToken);

        if (currentAttempt is null)
        {
            return new Result<AttemptOperationStatus, TestAttemptResponse>
            {
                Status = AttemptOperationStatus.NotFound
            };
        }

        return currentAttempt.Status == TestAttemptStatus.InProgress
            ? new Result<AttemptOperationStatus, TestAttemptResponse>
            {
                Status = AttemptOperationStatus.Conflict
            }
            : new Result<AttemptOperationStatus, TestAttemptResponse>
            {
                Status = AttemptOperationStatus.Conflict,
                Value = ToResponse(currentAttempt)
            };
    }

    private async Task<Result<AttemptOperationStatus, TestAttemptResponse>> ResolveSubmissionRaceAsync(
        Guid attemptId,
        string studentUserId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var currentAttempt = await dbContext.TestAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                attempt => attempt.Id == attemptId
                    && attempt.StudentUserId == studentUserId,
                cancellationToken);

        if (currentAttempt is null)
        {
            return new Result<AttemptOperationStatus, TestAttemptResponse>
            {
                Status = AttemptOperationStatus.NotFound
            };
        }

        return currentAttempt.Status switch
        {
            TestAttemptStatus.Submitted =>
                new Result<AttemptOperationStatus, TestAttemptResponse>
                {
                    Status = AttemptOperationStatus.Success,
                    Value = ToResponse(currentAttempt)
                },
            _ => new Result<AttemptOperationStatus, TestAttemptResponse>
            {
                Status = AttemptOperationStatus.Conflict
            }
        };
    }

    private async Task<TestAttempt?> LoadAttemptAsync(
        Guid assignmentId,
        string studentUserId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.TestAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                attempt => attempt.AssignmentId == assignmentId
                    && attempt.StudentUserId == studentUserId,
                cancellationToken);
    }

    private static Task<TestAttempt?> FindStudentAttemptAsync(
        AttemptDbContext dbContext,
        Guid attemptId,
        string studentUserId,
        CancellationToken cancellationToken)
    {
        return dbContext.TestAttempts.SingleOrDefaultAsync(
            attempt => attempt.Id == attemptId
                && attempt.StudentUserId == studentUserId,
            cancellationToken);
    }

    private static Dictionary<string, string> FilterAnswers(
        TestAttempt attempt,
        Dictionary<string, string> answers)
    {
        var allowedTaskIds = DeserializeTaskIds(attempt.AllowedTaskIdsJson).ToHashSet();
        return answers
            .Where(answer => allowedTaskIds.Contains(answer.Key))
            .ToDictionary(answer => answer.Key, answer => answer.Value);
    }

    private bool AreAnswersWithinLimits(Dictionary<string, string> answers)
    {
        if (answers.Count > _inputLimits.MaxAnswerCount)
            return false;

        var totalAnswerLength = 0L;
        foreach (var answer in answers.Values)
        {
            if (answer is null || answer.Length > _inputLimits.MaxAnswerLength)
                return false;

            totalAnswerLength += answer.Length;
            if (totalAnswerLength > _inputLimits.MaxTotalAnswerLength)
                return false;
        }

        return true;
    }

    private static void ApplyStudentSubmission(TestAttempt attempt, DateTimeOffset submittedAt)
    {
        attempt.Status = TestAttemptStatus.Submitted;
        attempt.SubmittedAt = submittedAt;
        attempt.SubmissionReason = AttemptSubmissionReason.Student;
    }

    private static void ApplyDeadlineSubmission(TestAttempt attempt)
    {
        attempt.Status = TestAttemptStatus.Submitted;
        attempt.SubmittedAt = attempt.EndsAt;
        attempt.SubmissionReason = AttemptSubmissionReason.Deadline;
    }

    private static void IncrementStateRevision(TestAttempt attempt)
    {
        attempt.StateRevision = checked(attempt.StateRevision + 1);
    }

    private static TestAttemptResponse ToResponse(TestAttempt attempt)
    {
        return new TestAttemptResponse
        {
            Id = attempt.Id,
            AssignmentId = attempt.AssignmentId,
            GroupId = attempt.GroupId,
            TestId = attempt.TestId,
            TestRevision = attempt.TestRevision,
            TeacherUserId = attempt.TeacherUserId,
            StudentUserId = attempt.StudentUserId,
            StudentName = attempt.StudentName,
            Status = attempt.Status,
            StartedAt = attempt.StartedAt,
            EndsAt = attempt.EndsAt,
            SubmittedAt = attempt.SubmittedAt,
            SubmissionReason = attempt.SubmissionReason,
            Answers = DeserializeAnswers(attempt.AnswersJson)
        };
    }

    private static bool IsDuplicateAttempt(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_TestAttempts_AssignmentId_StudentUserId"
        };
    }

    private static bool IsDuplicateSubmissionEvent(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_AttemptSubmissionOutboxMessages_AttemptId"
        };
    }

    private static Dictionary<string, string> DeserializeAnswers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>();

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
            ?? new Dictionary<string, string>();
    }

    private static string SerializeAnswers(Dictionary<string, string> answers)
    {
        return JsonSerializer.Serialize(answers, JsonOptions);
    }

    private static List<string> DeserializeTaskIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        return JsonSerializer.Deserialize<List<string>>(json, JsonOptions)
            ?? new List<string>();
    }

    private static string SerializeTaskIds(IEnumerable<string> taskIds)
    {
        return JsonSerializer.Serialize(taskIds.Distinct().ToList(), JsonOptions);
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right)
    {
        return left <= right ? left : right;
    }
}
