using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using ReviewService.Controllers;
using ReviewService.Contracts.Enums;
using ReviewService.Data;
using ReviewService.Enums;
using ReviewService.Models.Reviews;
using ReviewService.Services;

namespace ReviewService.Tests;

public sealed class ReviewPaginationTests
{
    private static readonly DateTimeOffset SubmittedAt =
        new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TeacherPages_KeepAllNonTerminalAndSeparateCurrentFromHistoricalTerminal()
    {
        await using var dbContext = CreateDbContext();
        dbContext.TestReviewPolicies.AddRange(
            CreatePolicy(revision: 1),
            CreatePolicy(revision: 2));
        dbContext.Reviews.AddRange(
            CreateReview(1, ReviewStatus.ManualReview, score: 0, maxScore: 2, attemptId: 1),
            CreateReview(1, ReviewStatus.Queued, score: 0, maxScore: 2, attemptId: 2),
            CreateReview(1, ReviewStatus.Failed, score: 0, maxScore: 2, attemptId: 3),
            CreateReview(2, ReviewStatus.Checked, score: 1, maxScore: 2, attemptId: 4),
            CreateReview(2, ReviewStatus.Checked, score: 4, maxScore: 4, attemptId: 5));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var firstPage = await service.GetTeacherReviewsAsync(
            "teacher",
            includeNonTerminal: true,
            includeHistoricalVersions: false,
            pageSize: 1,
            cursor: null,
            CancellationToken.None);

        Assert.Equal(ReviewPageQueryStatus.Success, firstPage.Status);
        Assert.NotNull(firstPage.Page);
        Assert.Equal(2, firstPage.Page.NonTerminalReviews.Count);
        var firstTerminal = Assert.Single(firstPage.Page.TerminalReviews);
        Assert.Equal(2, firstTerminal.TestRevision);
        Assert.NotNull(firstPage.Page.NextCursor);
        Assert.Equal(2, firstPage.Page.Aggregate.PendingReviews);
        Assert.Equal(75m, firstPage.Page.Aggregate.AverageScorePercentage);

        var secondPage = await service.GetTeacherReviewsAsync(
            "teacher",
            includeNonTerminal: false,
            includeHistoricalVersions: false,
            pageSize: 1,
            cursor: firstPage.Page.NextCursor,
            CancellationToken.None);

        Assert.Equal(ReviewPageQueryStatus.Success, secondPage.Status);
        Assert.NotNull(secondPage.Page);
        var secondTerminal = Assert.Single(secondPage.Page.TerminalReviews);
        Assert.Equal(2, secondTerminal.TestRevision);
        Assert.True(firstTerminal.Id > secondTerminal.Id);
        Assert.Null(secondPage.Page.NextCursor);

        var history = await service.GetTeacherReviewsAsync(
            "teacher",
            includeNonTerminal: false,
            includeHistoricalVersions: true,
            pageSize: 10,
            cursor: null,
            CancellationToken.None);

        Assert.Equal(ReviewPageQueryStatus.Success, history.Status);
        Assert.NotNull(history.Page);
        Assert.Equal(3, history.Page.TerminalReviews.Count);
        Assert.Contains(history.Page.TerminalReviews, review => review.TestRevision == 1);
    }

    [Fact]
    public async Task StudentPages_UseSubmittedAtAndIdCursorAndCapOversizedPage()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Reviews.AddRange(Enumerable.Range(1, 101)
            .Select(index => CreateReview(
                revision: 1,
                ReviewStatus.Checked,
                score: index,
                maxScore: 101,
                attemptId: index)));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var firstPage = await service.GetStudentReviewsAsync(
            "student",
            includeNonTerminal: true,
            pageSize: int.MaxValue,
            cursor: null,
            CancellationToken.None);

        Assert.Equal(ReviewPageQueryStatus.Success, firstPage.Status);
        Assert.NotNull(firstPage.Page);
        Assert.Equal(100, firstPage.Page.TerminalReviews.Count);
        Assert.NotNull(firstPage.Page.NextCursor);
        Assert.Equal(
            firstPage.Page.TerminalReviews.OrderByDescending(review => review.Id).Select(review => review.Id),
            firstPage.Page.TerminalReviews.Select(review => review.Id));

        var secondPage = await service.GetStudentReviewsAsync(
            "student",
            includeNonTerminal: false,
            pageSize: int.MaxValue,
            cursor: firstPage.Page.NextCursor,
            CancellationToken.None);

        Assert.Equal(ReviewPageQueryStatus.Success, secondPage.Status);
        Assert.NotNull(secondPage.Page);
        var finalReview = Assert.Single(secondPage.Page.TerminalReviews);
        Assert.DoesNotContain(
            firstPage.Page.TerminalReviews,
            review => review.Id == finalReview.Id);
        Assert.Null(secondPage.Page.NextCursor);
    }

    [Fact]
    public async Task InvalidCursorAndNonPositivePageSize_AreRejected()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var invalidCursor = await service.GetStudentReviewsAsync(
            "student",
            includeNonTerminal: true,
            pageSize: 25,
            cursor: "not-a-review-cursor",
            CancellationToken.None);
        var invalidPageSize = await service.GetStudentReviewsAsync(
            "student",
            includeNonTerminal: true,
            pageSize: 0,
            cursor: null,
            CancellationToken.None);
        var blankCursor = await service.GetStudentReviewsAsync(
            "student",
            includeNonTerminal: true,
            pageSize: 25,
            cursor: " ",
            CancellationToken.None);

        Assert.Equal(ReviewPageQueryStatus.InvalidCursor, invalidCursor.Status);
        Assert.Null(invalidCursor.Page);
        Assert.Equal(ReviewPageQueryStatus.InvalidPageSize, invalidPageSize.Status);
        Assert.Null(invalidPageSize.Page);
        Assert.Equal(ReviewPageQueryStatus.InvalidCursor, blankCursor.Status);
        Assert.Null(blankCursor.Page);
    }

    [Fact]
    public async Task Controller_ReturnsProblemDetailsForInvalidPagingInputs()
    {
        await using var dbContext = CreateDbContext();
        var controller = new ReviewsController(CreateService(dbContext));

        var response = await controller.GetStudentReviews(
            "student",
            CancellationToken.None,
            pageSize: 0,
            cursor: null);
        var invalidCursorResponse = await controller.GetStudentReviews(
            "student",
            CancellationToken.None,
            pageSize: 25,
            cursor: "invalid");

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(response.Result);
        Assert.Equal(400, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Invalid review page size.", problem.Title);
        var invalidCursorResult = Assert.IsAssignableFrom<ObjectResult>(
            invalidCursorResponse.Result);
        Assert.Equal(400, invalidCursorResult.StatusCode);
        var cursorProblem = Assert.IsType<ProblemDetails>(invalidCursorResult.Value);
        Assert.Equal("Invalid review cursor.", cursorProblem.Title);
    }

    private static ReviewQueryService CreateService(ReviewDbContext dbContext)
    {
        return new ReviewQueryService(
            dbContext,
            new ReviewScoringService());
    }

    private static ReviewDbContext CreateDbContext()
    {
        return new ReviewDbContext(
            new DbContextOptionsBuilder<ReviewDbContext>()
                .UseInMemoryDatabase($"review-pagination-{Guid.NewGuid():N}")
                .Options);
    }

    private static TestReviewPolicy CreatePolicy(int revision)
    {
        return new TestReviewPolicy
        {
            TestId = "test",
            Revision = revision,
            TeacherUserId = "teacher",
            TestTitle = $"Test v{revision}",
            PublishedAt = SubmittedAt,
            ReceivedAt = SubmittedAt
        };
    }

    private static Review CreateReview(
        int revision,
        ReviewStatus status,
        decimal score,
        decimal maxScore,
        int attemptId)
    {
        return new Review
        {
            AttemptId = attemptId,
            TestId = "test",
            TestRevision = revision,
            TestTitle = $"Test v{revision}",
            TeacherUserId = "teacher",
            StudentUserId = "student",
            Status = status,
            SubmittedAt = SubmittedAt,
            Score = score,
            MaxScore = maxScore
        };
    }
}
