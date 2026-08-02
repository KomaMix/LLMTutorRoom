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
        public DbSet<UserAccount> Users { get; set; } = null!;

        public TutorRoomDbContext(DbContextOptions<TutorRoomDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserAccount>(entity =>
            {
                entity.HasKey(user => user.UserName);
                entity.Property(user => user.Role).HasConversion<string>();
            });

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
                entity.HasMany(review => review.TaskResults)
                    .WithOne()
                    .HasForeignKey(result => result.SubmissionReviewId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TaskReviewResult>(entity =>
            {
                entity.Property(result => result.FindingsJson)
                    .HasColumnName("Findings")
                    .HasColumnType("jsonb");
            });

            modelBuilder.Entity<TestAttempt>(entity =>
            {
                entity.Property(attempt => attempt.Status).HasConversion<string>();
                entity.Property(attempt => attempt.AnswersJson)
                    .HasColumnName("Answers")
                    .HasColumnType("jsonb");
                entity.HasIndex(attempt => new { attempt.TestId, attempt.StudentUserName })
                    .IsUnique();
                entity.HasOne<CourseTest>()
                    .WithMany()
                    .HasForeignKey(attempt => attempt.TestId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
