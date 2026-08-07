using LLMGateway.Data.Models;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LLMGateway.Data
{
    public class AppDbContext : DbContext
    {
        private static readonly JsonSerializerOptions RateLimitJsonOptions = new(JsonSerializerDefaults.Web);
        private static readonly ValueComparer<List<ModelRateLimitRule>> RateLimitRulesComparer = new(
            (left, right) => SerializeRateLimitRules(left) == SerializeRateLimitRules(right),
            rules => SerializeRateLimitRules(rules).GetHashCode(),
            rules => DeserializeRateLimitRules(SerializeRateLimitRules(rules)));

        public DbSet<Model> Models { get; set; } = null!;
        public DbSet<ModelDeployment> ModelDeployments { get; set; } = null!;

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Model>(entity =>
            {
                entity.HasIndex(m => m.Key).IsUnique();
            });

            modelBuilder.Entity<ModelDeployment>(entity =>
            {
                var rateLimitRules = entity.Property(d => d.RateLimitRules)
                    .HasConversion(
                        rules => SerializeRateLimitRules(rules),
                        json => DeserializeRateLimitRules(json))
                    .HasColumnName("RateLimitRules")
                    .HasColumnType("jsonb")
                    .HasDefaultValueSql("'[]'::jsonb");
                rateLimitRules.Metadata.SetValueComparer(RateLimitRulesComparer);

                entity.HasOne(d => d.Model)
                    .WithMany(m => m.Deployments)
                    .HasForeignKey(d => d.ModelId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private static string SerializeRateLimitRules(List<ModelRateLimitRule>? rules)
        {
            return JsonSerializer.Serialize(rules ?? new List<ModelRateLimitRule>(), RateLimitJsonOptions);
        }

        private static List<ModelRateLimitRule> DeserializeRateLimitRules(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<ModelRateLimitRule>();

            return JsonSerializer.Deserialize<List<ModelRateLimitRule>>(json, RateLimitJsonOptions)
                ?? new List<ModelRateLimitRule>();
        }
    }
}
