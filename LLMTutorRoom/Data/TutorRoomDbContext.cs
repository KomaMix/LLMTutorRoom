using LLMTutorRoom.Models;
using Microsoft.EntityFrameworkCore;
namespace LLMTutorRoom.Data
{
    public sealed class TutorRoomDbContext : DbContext
    {
        public DbSet<SubmissionReview> SubmissionReviews { get; set; } = null!;
        public DbSet<TaskReviewResult> TaskReviewResults { get; set; } = null!;
        public DbSet<TestAttempt> TestAttempts { get; set; } = null!;
        public DbSet<TeacherModelAccess> TeacherModelAccesses { get; set; } = null!;
        public DbSet<TeacherModelUsage> TeacherModelUsages { get; set; } = null!;

        public TutorRoomDbContext(DbContextOptions<TutorRoomDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SubmissionReview>(entity =>
            {
                entity.Property(review => review.Status).HasConversion<string>();
                entity.HasIndex(review => review.TeacherUserId);
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
            });

            modelBuilder.Entity<TeacherModelAccess>(entity =>
            {
                entity.HasIndex(access => new { access.TeacherUserId, access.ModelKey })
                    .IsUnique();
                entity.HasIndex(access => access.TeacherUserId);
            });

            modelBuilder.Entity<TeacherModelUsage>(entity =>
            {
                entity.HasIndex(usage => new
                    {
                        usage.TeacherUserId,
                        usage.ModelKey,
                        usage.PeriodStart,
                        usage.PeriodSeconds
                    })
                    .IsUnique();
                entity.HasIndex(usage => usage.TeacherUserId);
            });
        }
    }
}
