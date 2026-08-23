using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using ReviewService.Controllers;
using ReviewService.Contracts.Requests;
using ReviewService.Contracts.Responses;
using ReviewService.Enums;
using ReviewService.Interfaces;
using ReviewService.Models.ModelAccess;
using ReviewService.Models.Reviews;

namespace ReviewService.Tests;

public sealed class ReviewApiControllerTests
{
    [Fact]
    public async Task ManualReview_ForwardsAuthenticatedTeacherId()
    {
        var service = new CapturingReviewQueryService();
        var controller = new ReviewsController(service)
        {
            ControllerContext = CreateControllerContext(
                new Claim(ClaimTypes.NameIdentifier, "teacher-from-token"))
        };
        var request = new UpdateManualTaskReviewRequest
        {
            Score = 2,
            Feedback = "Проверено"
        };

        var response = await controller.UpdateManualTaskReview(
            reviewId: 12,
            taskId: "task-1",
            request,
            CancellationToken.None);

        Assert.Equal("teacher-from-token", service.TeacherUserId);
        Assert.Equal(12, service.ReviewId);
        Assert.Equal("task-1", service.TaskId);
        Assert.Same(request, service.Request);
        var problemResult = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status404NotFound, problemResult.StatusCode);
    }

    [Fact]
    public async Task ManualReview_WithoutTeacherIdClaim_ReturnsUnauthorized()
    {
        var service = new CapturingReviewQueryService();
        var controller = new ReviewsController(service)
        {
            ControllerContext = CreateControllerContext()
        };

        var response = await controller.UpdateManualTaskReview(
            reviewId: 12,
            taskId: "task-1",
            new UpdateManualTaskReviewRequest { Score = 2 },
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response.Result);
        Assert.Null(service.TeacherUserId);
    }

    [Fact]
    public async Task MyModelAccess_ForwardsAuthenticatedTeacherIdAndExcludesDisabledAccess()
    {
        var service = new CapturingModelAccessService();
        var controller = new ModelAccessController(service)
        {
            ControllerContext = CreateControllerContext(
                new Claim(ClaimTypes.NameIdentifier, "teacher-from-token"))
        };

        var response = await controller.GetMyAccess(CancellationToken.None);

        Assert.Equal("teacher-from-token", service.TeacherUserId);
        Assert.False(service.IncludeDisabled);
        Assert.IsType<OkObjectResult>(response.Result);
    }

    [Fact]
    public async Task MyModelAccess_WithoutTeacherIdClaim_ReturnsUnauthorized()
    {
        var service = new CapturingModelAccessService();
        var controller = new ModelAccessController(service)
        {
            ControllerContext = CreateControllerContext()
        };

        var response = await controller.GetMyAccess(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response.Result);
        Assert.Null(service.TeacherUserId);
    }

    [Fact]
    public void PublicRoutes_HaveExpectedRoleRestrictions()
    {
        Assert.Equal("api/reviews", GetControllerRoute<ReviewsController>());
        Assert.Equal("Teacher", GetControllerRole<ReviewsController>());
        AssertMethodRoute<HttpPutAttribute>(
            typeof(ReviewsController),
            nameof(ReviewsController.UpdateManualTaskReview),
            "{reviewId:int}/tasks/{taskId}/manual");

        Assert.Equal("api/model-access", GetControllerRoute<ModelAccessController>());
        Assert.Null(GetControllerRole<ModelAccessController>());
        AssertMethodRoute<HttpGetAttribute>(
            typeof(ModelAccessController),
            nameof(ModelAccessController.GetCatalog),
            "models",
            "Admin");
        AssertMethodRoute<HttpGetAttribute>(
            typeof(ModelAccessController),
            nameof(ModelAccessController.GetMyAccess),
            "me",
            "Teacher");
        AssertMethodRoute<HttpGetAttribute>(
            typeof(ModelAccessController),
            nameof(ModelAccessController.GetTeacherAccess),
            "teachers/{teacherId}",
            "Admin");
        AssertMethodRoute<HttpPutAttribute>(
            typeof(ModelAccessController),
            nameof(ModelAccessController.UpsertTeacherAccess),
            "models/{modelKey}/teachers/{teacherId}",
            "Admin");
        AssertMethodRoute<HttpDeleteAttribute>(
            typeof(ModelAccessController),
            nameof(ModelAccessController.DeleteTeacherAccess),
            "models/{modelKey}/teachers/{teacherId}",
            "Admin");
    }

    [Fact]
    public void InternalControllers_ExposeReadEndpointsOnly()
    {
        Assert.Equal("internal/reviews", GetControllerRoute<InternalReviewsController>());
        Assert.Equal("internal/model-access", GetControllerRoute<InternalModelAccessController>());
        Assert.Null(GetControllerRole<InternalReviewsController>());
        Assert.Null(GetControllerRole<InternalModelAccessController>());

        Assert.All(GetPublicActions(typeof(InternalReviewsController)), AssertGetOnly);
        Assert.All(GetPublicActions(typeof(InternalModelAccessController)), AssertGetOnly);
    }

    private static ControllerContext CreateControllerContext(params Claim[] claims)
    {
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthentication"))
            }
        };
    }

    private static string GetControllerRoute<TController>()
    {
        return typeof(TController).GetCustomAttribute<RouteAttribute>()?.Template
            ?? string.Empty;
    }

    private static string? GetControllerRole<TController>()
    {
        return typeof(TController).GetCustomAttribute<AuthorizeAttribute>()?.Roles;
    }

    private static void AssertMethodRoute<TAttribute>(
        Type controllerType,
        string methodName,
        string route,
        string? role = null)
        where TAttribute : HttpMethodAttribute
    {
        var method = controllerType.GetMethod(methodName);

        Assert.NotNull(method);
        Assert.Equal(route, method.GetCustomAttribute<TAttribute>()?.Template);
        Assert.Equal(role, method.GetCustomAttribute<AuthorizeAttribute>()?.Roles);
    }

    private static List<MethodInfo> GetPublicActions(Type controllerType)
    {
        return controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .ToList();
    }

    private static void AssertGetOnly(MethodInfo method)
    {
        Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>());
        Assert.Null(method.GetCustomAttribute<HttpPutAttribute>());
        Assert.Null(method.GetCustomAttribute<HttpDeleteAttribute>());
    }

    private sealed class CapturingReviewQueryService : IReviewQueryService
    {
        public int? ReviewId { get; private set; }
        public string? TaskId { get; private set; }
        public string? TeacherUserId { get; private set; }
        public UpdateManualTaskReviewRequest? Request { get; private set; }

        public Task<ManualReviewUpdateResult> UpdateManualTaskReviewAsync(
            int reviewId,
            string taskId,
            string teacherUserId,
            UpdateManualTaskReviewRequest request,
            CancellationToken cancellationToken)
        {
            ReviewId = reviewId;
            TaskId = taskId;
            TeacherUserId = teacherUserId;
            Request = request;
            return Task.FromResult(new ManualReviewUpdateResult(
                ManualReviewUpdateStatus.NotFound));
        }

        public Task<ReviewPageQueryResult> GetTeacherReviewsAsync(
            string teacherUserId,
            bool includeNonTerminal,
            bool includeHistoricalVersions,
            int pageSize,
            string? cursor,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ReviewPageQueryResult> GetStudentReviewsAsync(
            string studentUserId,
            bool includeNonTerminal,
            int pageSize,
            string? cursor,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class CapturingModelAccessService : ITeacherModelAccessService
    {
        public string? TeacherUserId { get; private set; }
        public bool? IncludeDisabled { get; private set; }

        public Task<List<LlmModelCatalogItemResponse>> GetModelCatalogAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<List<LlmModelCatalogItemResponse>>([]);
        }

        public Task<List<TeacherModelAccessResponse>> GetTeacherAccessAsync(
            string teacherUserId,
            bool includeDisabled,
            CancellationToken cancellationToken)
        {
            TeacherUserId = teacherUserId;
            IncludeDisabled = includeDisabled;
            return Task.FromResult<List<TeacherModelAccessResponse>>([]);
        }

        public Task<TeacherModelAccessResponse?> UpsertTeacherAccessAsync(
            string teacherUserId,
            string modelKey,
            UpsertTeacherModelAccessRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> DeleteTeacherAccessAsync(
            string teacherUserId,
            string modelKey,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ModelQuotaConsumptionResult> TryConsumeChecksAsync(
            string teacherUserId,
            string modelKey,
            int checkCount,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
