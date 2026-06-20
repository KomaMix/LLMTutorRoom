using LLMGateway.Data;
using LLMGateway.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace LLMGateway.Services
{
    public class RateLimitService
    {
        private readonly AppDbContext _dbContext;

        public RateLimitService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<bool> TryConsumeAsync(
            ICollection<ModelRateLimitRule> rules,
            CancellationToken cancellationToken)
        {
            if (rules.Count == 0)
                return true;

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            foreach (var rule in rules)
            {
                var now = DateTime.UtcNow;
                var windowStartedAt = new DateTime(
                    now.Ticks - now.Ticks % TimeSpan.FromSeconds(rule.WindowSeconds).Ticks,
                    DateTimeKind.Utc);

                var bucket = await _dbContext.ModelRateLimitBuckets
                    .SingleOrDefaultAsync(
                        b => b.ModelRateLimitRuleId == rule.Id && b.WindowStartedAt == windowStartedAt,
                        cancellationToken);

                if (bucket is null)
                {
                    _dbContext.ModelRateLimitBuckets.Add(new ModelRateLimitBucket
                    {
                        ModelRateLimitRuleId = rule.Id,
                        WindowStartedAt = windowStartedAt,
                        RequestCount = 1
                    });
                    continue;
                }

                if (bucket.RequestCount >= rule.MaxRequests)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return false;
                }

                bucket.RequestCount++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
    }
}
