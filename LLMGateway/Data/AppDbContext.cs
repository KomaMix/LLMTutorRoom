using LLMGateway.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LLMGateway.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Model> Models => Set<Model>();
    public DbSet<ModelDeployment> ModelDeployments => Set<ModelDeployment>();
    public DbSet<ModelRateLimitRule> ModelRateLimitRules => Set<ModelRateLimitRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Model>().HasIndex(model => model.Key).IsUnique();
        modelBuilder.Entity<ModelDeployment>()
            .HasOne(deployment => deployment.Model)
            .WithMany(model => model.Deployments)
            .HasForeignKey(deployment => deployment.ModelId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ModelRateLimitRule>()
            .HasOne(rule => rule.Deployment)
            .WithMany(deployment => deployment.RateLimitRules)
            .HasForeignKey(rule => rule.DeploymentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
