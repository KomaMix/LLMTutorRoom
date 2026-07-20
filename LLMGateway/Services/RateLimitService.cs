using LLMGateway.Data.Models;

namespace LLMGateway.Services
{
    public class RateLimitService
    {
        private readonly object _syncRoot = new();
        private readonly Dictionary<RateLimitKey, Queue<DateTimeOffset>> _requests = new();

        public bool TryConsume(
            int deploymentId,
            IReadOnlyCollection<ModelRateLimitRule> rules)
        {
            if (rules.Count == 0)
                return true;

            var now = DateTimeOffset.UtcNow;

            lock (_syncRoot)
            {
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
                        return false;

                    allowedQueues.Add(queue);
                }

                foreach (var queue in allowedQueues)
                    queue.Enqueue(now);

                return true;
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
}
