using LLMTutorRoom.Models;
using Microsoft.EntityFrameworkCore;
namespace LLMTutorRoom.Data
{
    public sealed class TutorRoomDbContext : DbContext
    {
        public DbSet<CourseTest> Tests { get; set; } = null!;
        public DbSet<TestTask> TestTasks { get; set; } = null!;
        public DbSet<AnswerOption> AnswerOptions { get; set; } = null!;
        public DbSet<SubmissionReview> SubmissionReviews { get; set; } = null!;
        public DbSet<TaskReviewResult> TaskReviewResults { get; set; } = null!;
        public DbSet<TestAttempt> TestAttempts { get; set; } = null!;

        public TutorRoomDbContext(DbContextOptions<TutorRoomDbContext> options) : base(options) { }

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
                entity.Property(task => task.CreatedAt).HasDefaultValueSql("now()");
                entity.HasMany(task => task.Options)
                    .WithOne()
                    .HasForeignKey(option => option.TestTaskId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AnswerOption>(entity =>
            {
                entity.HasKey(option => option.Id);
            });

            modelBuilder.Entity<SubmissionReview>(entity =>
            {
                entity.Property(review => review.Status).HasConversion<string>();
                entity.HasIndex(review => review.AttemptId)
                    .IsUnique();
                entity.HasOne(review => review.Attempt)
                    .WithMany()
                    .HasForeignKey(review => review.AttemptId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(review => review.TaskResults)
                    .WithOne()
                    .HasForeignKey(result => result.SubmissionReviewId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TaskReviewResult>(entity =>
            {
                entity.Property(result => result.CheckMode).HasConversion<string>();
                entity.Property(result => result.Status).HasConversion<string>();
                entity.Property(result => result.FindingsJson)
                    .HasColumnName("Findings")
                    .HasColumnType("jsonb");
                entity.HasIndex(result => new { result.SubmissionReviewId, result.TaskId })
                    .IsUnique();
            });

            modelBuilder.Entity<TestAttempt>(entity =>
            {
                entity.Property(attempt => attempt.Status).HasConversion<string>();
                entity.Property(attempt => attempt.AnswersJson)
                    .HasColumnName("Answers")
                    .HasColumnType("jsonb");
                entity.HasIndex(attempt => new { attempt.TestId, attempt.StudentUserId })
                    .IsUnique();
                entity.HasOne<CourseTest>()
                    .WithMany()
                    .HasForeignKey(attempt => attempt.TestId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
