using LLMGateway.Data.Models;
using LLMGateway.Enums;
using LLMGateway.Services;

namespace LLMGateway.Tests;

public sealed class RateLimitServiceTests
{
    [Fact]
    public void EnablingConcurrencyLimit_CountsCallsAlreadyInFlight()
    {
        var service = new RateLimitService();
        Assert.Equal(LimitCheckResult.Allowed, service.TryAcquire(1, null, []));
        Assert.Equal(LimitCheckResult.ConcurrencyLimitExceeded, service.TryAcquire(1, 1, []));
        service.Release(1);
        Assert.Equal(LimitCheckResult.Allowed, service.TryAcquire(1, 1, []));
        service.Release(1);
    }

    [Fact]
    public void EditingMaximum_PreservesUsage_AndNewWindowResetsIt()
    {
        var service = new RateLimitService();
        var rule = new ModelRateLimitRule { Id = 7, DeploymentId = 1, WindowSeconds = 3600, MaxRequests = 2 };
        Assert.Equal(LimitCheckResult.Allowed, service.TryAcquire(1, null, [rule]));
        service.Release(1);
        rule.MaxRequests = 1;
        Assert.Equal(LimitCheckResult.RateLimitExceeded, service.TryAcquire(1, null, [rule]));
        rule.WindowSeconds = 7200;
        Assert.Equal(LimitCheckResult.Allowed, service.TryAcquire(1, null, [rule]));
        service.Release(1);
        Assert.Equal(LimitCheckResult.RateLimitExceeded, service.TryAcquire(1, null, [rule]));
        Assert.Equal(LimitCheckResult.Allowed, service.TryAcquire(2, null, [new ModelRateLimitRule { Id = 8, WindowSeconds = 7200, MaxRequests = 1 }]));
    }
}
