using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReviewService.Contracts.Enums;
using ReviewService.Contracts.Requests;
using ReviewService.Contracts.Responses;
using ReviewService.Data;
using ReviewService.Enums;
using ReviewService.Helpers;
using ReviewService.Interfaces;
using ReviewService.Models.Reviews;

namespace ReviewService.Services;

public sealed class ReviewQueryService(
    ReviewDbContext dbContext,
    IReviewScoringService scoringService) : IReviewQueryService
{
    private const string LegacyManualFeedbackPlaceholder = "Ожидает ручной проверки.";
    private const string LegacyManualFindingPlaceholder =
        "Задание ожидает ручной проверки преподавателем.";

    public Task<ReviewPageQueryResult> GetTeacherReviewsAsync(
        string teacherUserId,
        bool includeNonTerminal,
        bool includeHistoricalVersions,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var reviews = ReviewQuery()
            .Where(review => review.TeacherUserId == teacherUserId);

        return GetReviewsAsync(
            reviews,
            includeNonTerminal,
            currentTeacherVersionsOnly: !includeHistoricalVersions,
            pageSize,
            cursor,
            cancellationToken);
    }

    public Task<ReviewPageQueryResult> GetStudentReviewsAsync(
        string studentUserId,
        bool includeNonTerminal,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var reviews = ReviewQuery()
            .Where(review => review.StudentUserId == studentUserId);

        return GetReviewsAsync(
            reviews,
            includeNonTerminal,
            currentTeacherVersionsOnly: false,
            pageSize,
            cursor,
            cancellationToken);
    }

    public async Task<ManualReviewUpdateResult> UpdateManualTaskReviewAsync(
        int reviewId,
        string taskId,
        string teacherUserId,
        UpdateManualTaskReviewRequest request,
        CancellationToken cancellationToken)
    {
        var review = await dbContext.Reviews
            .Include(item => item.TaskResults)
            .SingleOrDefaultAsync(item => item.Id == reviewId, cancellationToken);
        if (review is null)
            return new ManualReviewUpdateResult(ManualReviewUpdateStatus.NotFound);

        if (!string.Equals(review.TeacherUserId, teacherUserId, StringComparison.OrdinalIgnoreCase))
            return new ManualReviewUpdateResult(ManualReviewUpdateStatus.Forbidden);
        if (review.Status != ReviewStatus.ManualReview)
            return new ManualReviewUpdateResult(ManualReviewUpdateStatus.InvalidState);

        var task = review.TaskResults.SingleOrDefault(item => item.TaskId == taskId);
        if (task is null)
            return new ManualReviewUpdateResult(ManualReviewUpdateStatus.NotFound);
        if (task.Status != ReviewTaskStatus.ManualReview)
            return new ManualReviewUpdateResult(ManualReviewUpdateStatus.InvalidState);
        if (request.Score < 0 || request.Score > task.MaxScore)
            return new ManualReviewUpdateResult(ManualReviewUpdateStatus.InvalidScore);

        var now = DateTimeOffset.UtcNow;
        task.Score = Math.Clamp(Math.Round(request.Score, 1), 0, task.MaxScore);
        task.Feedback = string.IsNullOrWhiteSpace(request.Feedback)
            ? "Проверено преподавателем."
            : request.Feedback.Trim();
        task.FindingsJson = JsonSerializer.Serialize(
            (request.Findings ?? [])
                .Where(finding => !string.IsNullOrWhiteSpace(finding))
                .Select(finding => finding.Trim())
                .Take(5)
                .DefaultIfEmpty("Проверено преподавателем."),
            JsonHelper.Options);
        task.Status = ReviewTaskStatus.Succeeded;
        task.CompletedAt = now;
        task.NextRetryAt = null;
        task.LastError = string.Empty;

        scoringService.RecalculateReview(review);
        review.Status = scoringService.GetReviewStatusAfterTaskProcessing(review);
        review.CompletedAt = review.Status == ReviewStatus.Checked ? now : null;
        review.LastError = string.Empty;
        review.ProcessingGeneration++;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return new ManualReviewUpdateResult(ManualReviewUpdateStatus.InvalidState);
        }

        return new ManualReviewUpdateResult(
            ManualReviewUpdateStatus.Updated,
            ToResponse(review));
    }

    private IQueryable<Review> ReviewQuery()
    {
        return dbContext.Reviews
            .AsNoTracking()
            .AsSplitQuery()
            .Include(review => review.TaskResults);
    }

    private async Task<ReviewPageQueryResult> GetReviewsAsync(
        IQueryable<Review> ownedReviews,
        bool includeNonTerminal,
        bool currentTeacherVersionsOnly,
        int requestedPageSize,
        string? cursorValue,
        CancellationToken cancellationToken)
    {
        if (requestedPageSize <= 0)
            return new ReviewPageQueryResult(ReviewPageQueryStatus.InvalidPageSize);

        ReviewPageCursor? cursor = null;
        if (cursorValue is not null)
        {
            if (!ReviewPageCursor.TryDecode(cursorValue, out var decodedCursor))
                return new ReviewPageQueryResult(ReviewPageQueryStatus.InvalidCursor);

            cursor = decodedCursor;
        }

        var pageSize = Math.Min(requestedPageSize, ReviewPagination.MaximumPageSize);
        var nonTerminalReviews = includeNonTerminal
            ? await ownedReviews
                .Where(review => review.Status != ReviewStatus.Checked
                    && review.Status != ReviewStatus.Failed)
                .OrderByDescending(review => review.SubmittedAt)
                .ThenByDescending(review => review.Id)
                .ToListAsync(cancellationToken)
            : [];

        var terminalQuery = ownedReviews
            .Where(review => review.Status == ReviewStatus.Checked
                || review.Status == ReviewStatus.Failed);

        if (currentTeacherVersionsOnly)
        {
            terminalQuery = terminalQuery.Where(review =>
                dbContext.TestReviewPolicies
                    .Where(policy => policy.TestId == review.TestId
                        && policy.TeacherUserId == review.TeacherUserId)
                    .Max(policy => (int?)policy.Revision) == review.TestRevision);
        }

        var averageScorePercentage = includeNonTerminal
            ? await terminalQuery
                .Where(review => review.Status == ReviewStatus.Checked && review.MaxScore > 0)
                .Select(review => (decimal?)(review.Score / review.MaxScore * 100))
                .AverageAsync(cancellationToken) ?? 0
            : 0;

        var terminalPageQuery = terminalQuery;
        if (cursor.HasValue)
        {
            var cursorSubmittedAt = cursor.Value.SubmittedAt;
            var cursorId = cursor.Value.Id;
            terminalPageQuery = terminalPageQuery.Where(review =>
                review.SubmittedAt < cursorSubmittedAt
                || review.SubmittedAt == cursorSubmittedAt && review.Id < cursorId);
        }

        var terminalReviews = await terminalPageQuery
            .OrderByDescending(review => review.SubmittedAt)
            .ThenByDescending(review => review.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);
        var hasMore = terminalReviews.Count > pageSize;
        if (hasMore)
            terminalReviews.RemoveAt(terminalReviews.Count - 1);

        var nextCursor = hasMore && terminalReviews.Count > 0
            ? ReviewPageCursor.Encode(
                terminalReviews[^1].SubmittedAt,
                terminalReviews[^1].Id)
            : null;

        return new ReviewPageQueryResult(
            ReviewPageQueryStatus.Success,
            new ReviewPageResponse(
                nonTerminalReviews.Select(ToResponse).ToList(),
                terminalReviews.Select(ToResponse).ToList(),
                nextCursor,
                new ReviewAggregateResponse(
                    nonTerminalReviews.Count,
                    Math.Round(averageScorePercentage, 1))));
    }

    public static ReviewResponse ToResponse(Review review)
    {
        return new ReviewResponse(
            review.Id,
            review.AttemptId,
            review.TestId,
            review.TestRevision,
            review.TestTitle,
            review.TeacherUserId,
            review.StudentUserId,
            review.StudentName,
            review.Status,
            review.ModelKeySnapshot,
            review.SubmittedAt,
            review.QueuedAt,
            review.StartedAt,
            review.CompletedAt,
            review.NextRetryAt,
            review.ProcessingAttempts,
            review.LastError,
            review.Score,
            review.MaxScore,
            review.Summary,
            review.TaskResults
                .OrderBy(task => task.Id)
                .Select(ToResponse)
                .ToList());
    }

    private static ReviewTaskResponse ToResponse(ReviewTask task)
    {
        var answerOptions = DeserializeAnswerOptions(task.AnswerOptionsJson);
        var feedback = task.Feedback;
        var findings = DeserializeFindings(task.FindingsJson);
        if (task.CheckMode == ReviewCheckMode.Manual
            && task.Status == ReviewTaskStatus.Succeeded)
        {
            if (string.Equals(
                    feedback.Trim(),
                    LegacyManualFeedbackPlaceholder,
                    StringComparison.Ordinal))
            {
                feedback = string.Empty;
            }

            findings.RemoveAll(finding => string.Equals(
                finding.Trim(),
                LegacyManualFindingPlaceholder,
                StringComparison.Ordinal));
        }

        return new ReviewTaskResponse(
            task.Id,
            task.TaskId,
            task.TaskTitle,
            task.TaskPrompt,
            task.StudentAnswer,
            answerOptions,
            task.CheckMode,
            task.Status,
            task.Attempts,
            task.NextRetryAt,
            task.CompletedAt,
            task.LastError,
            task.Score,
            task.MaxScore,
            feedback,
            findings);
    }

    private static List<ReviewAnswerOptionResponse> DeserializeAnswerOptions(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<ReviewAnswerOptionResponse>>(
                json,
                JsonHelper.Options)
            ?? [];
    }

    private static List<string> DeserializeFindings(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<string>>(json, JsonHelper.Options) ?? [];
    }
}
