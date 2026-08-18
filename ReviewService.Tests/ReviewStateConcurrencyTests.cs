using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ReviewService.Contracts.Enums;
using ReviewService.Contracts.Requests;
using ReviewService.Data;
using ReviewService.Enums;
using ReviewService.Models.Reviews;
using ReviewService.Services;

namespace ReviewService.Tests;

public sealed class ReviewStateConcurrencyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ReviewStatus.Queued)]
    [InlineData(ReviewStatus.RetryScheduled)]
    public async Task ManualUpdate_WhenReviewIsNotAwaitingManualReview_DoesNotChangeTask(
        ReviewStatus reviewStatus)
    {
        await using var dbContext = CreateDbContext();
        var review = CreateReview(reviewStatus);
        dbContext.Reviews.Add(review);
        await dbContext.SaveChangesAsync();
        var reviewId = review.Id;
        var service = new ReviewQueryService(
            dbContext,
            new ReviewScoringService());

        var result = await service.UpdateManualTaskReviewAsync(
            reviewId,
            "manual",
            "teacher",
            new UpdateManualTaskReviewRequest
            {
                Score = 4m,
                Feedback = "Changed feedback.",
                Findings = ["Changed finding."]
            },
            CancellationToken.None);

        dbContext.ChangeTracker.Clear();
        var unchangedReview = await dbContext.Reviews
            .Include(item => item.TaskResults)
            .SingleAsync(item => item.Id == reviewId);
        var unchangedTask = Assert.Single(unchangedReview.TaskResults);
        Assert.Equal(ManualReviewUpdateStatus.InvalidState, result.Status);
        Assert.Null(result.Review);
        Assert.Equal(reviewStatus, unchangedReview.Status);
        Assert.Equal(ReviewTaskStatus.ManualReview, unchangedTask.Status);
        Assert.Equal(1.5m, unchangedTask.Score);
        Assert.Equal("Original feedback.", unchangedTask.Feedback);
        Assert.Equal("[\"Original finding.\"]", unchangedTask.FindingsJson);
        Assert.Null(unchangedTask.CompletedAt);
    }

    [Fact]
    public async Task ProcessingGeneration_PreventsStaleContextFromSavingReview()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<ReviewDbContext>()
            .UseInMemoryDatabase($"review-fencing-{Guid.NewGuid():N}", databaseRoot)
            .Options;
        await using (var seedContext = new ReviewDbContext(options))
        {
            seedContext.Reviews.Add(CreateReview(ReviewStatus.Processing, processingGeneration: 1));
            await seedContext.SaveChangesAsync();
        }

        await using var staleContext = new ReviewDbContext(options);
        await using var currentContext = new ReviewDbContext(options);
        var staleReview = await staleContext.Reviews.SingleAsync();
        var currentReview = await currentContext.Reviews.SingleAsync();
        currentReview.ProcessingGeneration++;
        currentReview.LastError = "Current worker owns the review.";
        await currentContext.SaveChangesAsync();

        staleReview.LastError = "Stale worker result.";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => staleContext.SaveChangesAsync());

        await using var verificationContext = new ReviewDbContext(options);
        var persistedReview = await verificationContext.Reviews.SingleAsync();
        Assert.Equal(2, persistedReview.ProcessingGeneration);
        Assert.Equal("Current worker owns the review.", persistedReview.LastError);
    }

    private static ReviewDbContext CreateDbContext()
    {
        return new ReviewDbContext(
            new DbContextOptionsBuilder<ReviewDbContext>()
                .UseInMemoryDatabase($"review-state-tests-{Guid.NewGuid():N}")
                .Options);
    }

    private static Review CreateReview(
        ReviewStatus status,
        int processingGeneration = 0)
    {
        return new Review
        {
            AttemptId = 100,
            TestId = "test-1",
            TestRevision = 1,
            TestTitle = "Test",
            TeacherUserId = "teacher",
            StudentUserId = "student",
            Status = status,
            SubmittedAt = Now,
            ProcessingGeneration = processingGeneration,
            Score = 1.5m,
            MaxScore = 5m,
            TaskResults =
            [
                new ReviewTask
                {
                    TaskId = "manual",
                    TaskType = ReviewTaskType.FreeText,
                    TaskTitle = "Manual task",
                    TaskPrompt = "Explain.",
                    StudentAnswer = "Answer.",
                    CheckMode = ReviewCheckMode.Manual,
                    Status = ReviewTaskStatus.ManualReview,
                    Score = 1.5m,
                    MaxScore = 5m,
                    Feedback = "Original feedback.",
                    FindingsJson = "[\"Original finding.\"]"
                }
            ]
        };
    }
}
