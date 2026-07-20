using LLMGateway.Data.Models;

namespace LLMGateway.Services
{
    public class RateLimitService
    {
        private readonly object _syncRoot = new();
        private readonly Dictionary<int, int> _activeRequests = new();
        private readonly Dictionary<RateLimitKey, Queue<DateTimeOffset>> _requests = new();

        public LimitCheckResult TryAcquire(
            int deploymentId,
            int? maxConcurrentRequests,
            IReadOnlyCollection<ModelRateLimitRule> rules)
        {
            var now = DateTimeOffset.UtcNow;
            var concurrencyLimit = maxConcurrentRequests.GetValueOrDefault();
            var hasConcurrencyLimit = concurrencyLimit > 0;

            lock (_syncRoot)
            {
                _activeRequests.TryGetValue(deploymentId, out var activeRequests);
                if (hasConcurrencyLimit && activeRequests >= concurrencyLimit)
                    return LimitCheckResult.ConcurrencyLimitExceeded;

                var allowedQueues = new List<Queue<DateTimeOffset>>();
                foreach (var rule in rules)
                {
                    if (rule.WindowSeconds <= 0 || rule.MaxRequests <= 0)
                        throw new InvalidOperationException(
                            $"Rate limit rule '{rule.Id}' has invalid configuration.");

                    var key = new RateLimitKey(deploymentId, rule.Id);
                    var queue = GetRequestQueue(key);
                    var window = TimeSpan.FromSeconds(rule.WindowSeconds);
                    var windowStart = now - window;

                    while (queue.Count > 0 && queue.Peek() <= windowStart)
                        queue.Dequeue();

                    if (queue.Count >= rule.MaxRequests)
                        return LimitCheckResult.RateLimitExceeded;

                    allowedQueues.Add(queue);
                }

                if (hasConcurrencyLimit)
                    _activeRequests[deploymentId] = activeRequests + 1;

                foreach (var queue in allowedQueues)
                    queue.Enqueue(now);

                return LimitCheckResult.Allowed;
            }
        }

        public void Release(
            int deploymentId,
            int? maxConcurrentRequests)
        {
            if (maxConcurrentRequests.GetValueOrDefault() <= 0)
                return;

            lock (_syncRoot)
            {
                if (!_activeRequests.TryGetValue(deploymentId, out var activeRequests))
                    return;

                if (activeRequests <= 1)
                    _activeRequests.Remove(deploymentId);
                else
                    _activeRequests[deploymentId] = activeRequests - 1;
            }
        }

        private Queue<DateTimeOffset> GetRequestQueue(RateLimitKey key)
        {
            if (!_requests.TryGetValue(key, out var queue))
                _requests[key] = queue = new Queue<DateTimeOffset>();

            return queue;
        }

        private readonly record struct RateLimitKey(int DeploymentId, int RuleId);
    }

    public enum LimitCheckResult
    {
        Allowed,
        RateLimitExceeded,
        ConcurrencyLimitExceeded
    }
}
