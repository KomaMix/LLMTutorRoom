using System.Security.Claims;
using AttemptService.Contracts.Requests;
using AttemptService.Contracts.Responses;
using AttemptService.Controllers;
using AttemptService.Enums;
using AttemptService.Interfaces;
using AttemptService.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AttemptService.Tests;

public sealed class AttemptsControllerTests
{
    [Fact]
    public async Task SaveAnswers_WhenServiceRejectsInput_ReturnsBadRequestProblemDetails()
    {
        var attemptService = new InvalidInputAttemptLifecycleService();
        var controller = new AttemptsController(attemptService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, "student-1") },
                        "TestAuthentication"))
                }
            }
        };
        var attemptId = Guid.NewGuid();
        var answers = new Dictionary<string, string> { ["task-a"] = "oversized-answer" };

        var result = await controller.SaveAnswers(
            attemptId,
            new SaveAttemptAnswersRequest { Answers = answers },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Invalid answers", problem.Title);
        Assert.Equal(
            "The submitted answers exceed the configured input limits.",
            problem.Detail);
        Assert.Equal(attemptId, attemptService.AttemptId);
        Assert.Equal("student-1", attemptService.StudentUserId);
        Assert.Same(answers, attemptService.Answers);
    }

    private sealed class InvalidInputAttemptLifecycleService : IAttemptLifecycleService
    {
        public Guid? AttemptId { get; private set; }
        public string? StudentUserId { get; private set; }
        public Dictionary<string, string>? Answers { get; private set; }

        public Task<Result<AttemptOperationStatus, TestAttemptResponse>> SaveAnswersAsync(
            Guid attemptId,
            string studentUserId,
            Dictionary<string, string> answers,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AttemptId = attemptId;
            StudentUserId = studentUserId;
            Answers = answers;
            return Task.FromResult(
                new Result<AttemptOperationStatus, TestAttemptResponse>
                {
                    Status = AttemptOperationStatus.InvalidInput
                });
        }

        public Task<List<TestAttemptResponse>> GetStudentAttemptsAsync(
            string studentUserId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TestAttemptResponse?> GetAttemptAsync(
            Guid attemptId,
            string studentUserId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<AttemptOperationStatus, TestAttemptResponse>> StartAttemptAsync(
            Guid testId,
            string studentUserId,
            int? expectedVersionNumber,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<AttemptOperationStatus, TestAttemptResponse>> SubmitAttemptAsync(
            Guid attemptId,
            string studentUserId,
            string studentUserName,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
