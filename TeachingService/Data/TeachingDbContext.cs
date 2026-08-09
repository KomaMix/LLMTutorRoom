using Microsoft.EntityFrameworkCore;
using TeachingService.Models;

namespace TeachingService.Data
{
    public sealed class TeachingDbContext : DbContext
    {
        public DbSet<CourseTest> Tests { get; set; } = null!;
        public DbSet<TestTask> TestTasks { get; set; } = null!;
        public DbSet<AnswerOption> AnswerOptions { get; set; } = null!;

        public TeachingDbContext(DbContextOptions<TeachingDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CourseTest>(entity =>
            {
                entity.HasKey(test => test.Id);
                entity.Property(test => test.Status).HasConversion<string>();
                entity.HasMany(test => test.Tasks)
                    .WithOne()
                    .HasForeignKey(task => task.CourseTestId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TestTask>(entity =>
            {
                entity.HasKey(task => task.Id);
                entity.Property(task => task.Type).HasConversion<string>();
                entity.Property(task => task.CheckMode).HasConversion<string>();
                entity.HasMany(task => task.Options)
                    .WithOne()
                    .HasForeignKey(option => option.TestTaskId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AnswerOption>(entity =>
            {
                entity.HasKey(option => option.Id);
            });
        }
    }
}
