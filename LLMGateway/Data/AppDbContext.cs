using LLMGateway.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LLMGateway.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Model> Models => Set<Model>();
        public DbSet<ModelDeployment> ModelDeployments => Set<ModelDeployment>();
        public DbSet<ModelRateLimitRule> ModelRateLimitRules => Set<ModelRateLimitRule>();
        public DbSet<ModelRateLimitBucket> ModelRateLimitBuckets => Set<ModelRateLimitBucket>();

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Model>(entity =>
            {
                entity.HasIndex(m => m.Key).IsUnique();
                entity.Property(m => m.Key).HasMaxLength(200);
                entity.Property(m => m.DisplayName).HasMaxLength(200);
            });

            modelBuilder.Entity<ModelDeployment>(entity =>
            {
                entity.Property(d => d.ProviderType).HasMaxLength(100);
                entity.Property(d => d.ProviderModelId).HasMaxLength(200);
                entity.HasOne(d => d.Model)
                    .WithMany(m => m.Deployments)
                    .HasForeignKey(d => d.ModelId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ModelRateLimitRule>(entity =>
            {
                entity.HasOne(r => r.ModelDeployment)
                    .WithMany(d => d.RateLimitRules)
                    .HasForeignKey(r => r.ModelDeploymentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ModelRateLimitBucket>(entity =>
            {
                entity.HasKey(b => new { b.ModelRateLimitRuleId, b.WindowStartedAt });
                entity.HasOne(b => b.ModelRateLimitRule)
                    .WithMany()
                    .HasForeignKey(b => b.ModelRateLimitRuleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
