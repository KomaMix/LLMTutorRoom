using Microsoft.EntityFrameworkCore;
using ReviewService.Models.ModelAccess;
using ReviewService.Models.Reviews;

namespace ReviewService.Data;

public sealed class ReviewDbContext(DbContextOptions<ReviewDbContext> options) : DbContext(options)
{
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewTask> ReviewTasks => Set<ReviewTask>();
    public DbSet<TestReviewPolicy> TestReviewPolicies => Set<TestReviewPolicy>();
    public DbSet<PendingSubmission> PendingSubmissions => Set<PendingSubmission>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<TeacherModelAccess> TeacherModelAccesses => Set<TeacherModelAccess>();
    public DbSet<TeacherModelUsage> TeacherModelUsages => Set<TeacherModelUsage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Review>(entity =>
        {
            entity.Property(item => item.TestId).HasMaxLength(200);
            entity.Property(item => item.TestTitle).HasMaxLength(500);
            entity.Property(item => item.TeacherUserId).HasMaxLength(450);
            entity.Property(item => item.StudentUserId).HasMaxLength(450);
            entity.Property(item => item.StudentName).HasMaxLength(500);
            entity.Property(item => item.ModelKeySnapshot).HasMaxLength(300);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.ProcessingGeneration).IsConcurrencyToken();
            entity.HasIndex(item => item.AttemptId).IsUnique();
            entity.HasIndex(item => item.TeacherUserId);
            entity.HasIndex(item => item.StudentUserId);
            entity.HasIndex(item => new
                {
                    item.TeacherUserId,
                    item.SubmittedAt,
                    item.Id
                })
                .IsDescending(false, true, true);
            entity.HasIndex(item => new
                {
                    item.StudentUserId,
                    item.SubmittedAt,
                    item.Id
                })
                .IsDescending(false, true, true);
            entity.HasIndex(item => new { item.TestId, item.TestRevision });
            entity.HasIndex(item => new
            {
                item.Status,
                item.NextRetryAt,
                item.LastEnqueuedAt
            });
            entity.HasIndex(item => new
            {
                item.Status,
                item.ProcessingLeaseExpiresAt
            });
            entity.HasMany(item => item.TaskResults)
                .WithOne()
                .HasForeignKey(item => item.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReviewTask>(entity =>
        {
            entity.Property(item => item.TaskId).HasMaxLength(200);
            entity.Property(item => item.TaskTitle).HasMaxLength(500);
            entity.Property(item => item.TaskType).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.CheckMode).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.AnswerOptionsJson).HasColumnName("AnswerOptions").HasColumnType("jsonb");
            entity.Property(item => item.FindingsJson).HasColumnName("Findings").HasColumnType("jsonb");
            entity.HasIndex(item => new { item.ReviewId, item.TaskId }).IsUnique();
        });

        modelBuilder.Entity<TestReviewPolicy>(entity =>
        {
            entity.Property(item => item.TestId).HasMaxLength(200);
            entity.Property(item => item.TeacherUserId).HasMaxLength(450);
            entity.Property(item => item.TestTitle).HasMaxLength(500);
            entity.Property(item => item.ModelKeySnapshot).HasMaxLength(300);
            entity.Property(item => item.TasksJson).HasColumnName("Tasks").HasColumnType("jsonb");
            entity.HasIndex(item => new { item.TestId, item.Revision }).IsUnique();
        });

        modelBuilder.Entity<PendingSubmission>(entity =>
        {
            entity.Property(item => item.TestId).HasMaxLength(200);
            entity.Property(item => item.StudentUserId).HasMaxLength(450);
            entity.Property(item => item.StudentName).HasMaxLength(500);
            entity.Property(item => item.AnswersJson).HasColumnName("Answers").HasColumnType("jsonb");
            entity.HasIndex(item => item.AttemptId).IsUnique();
            entity.HasIndex(item => item.ReceivedAt);
            entity.HasIndex(item => new { item.TestId, item.TestRevision });
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.HasKey(item => item.EventId);
            entity.Property(item => item.EventType).HasMaxLength(200);
            entity.HasIndex(item => item.ProcessedAt);
        });

        modelBuilder.Entity<TeacherModelAccess>(entity =>
        {
            entity.Property(item => item.TeacherUserId).HasMaxLength(450);
            entity.Property(item => item.ModelKey).HasMaxLength(300);
            entity.HasIndex(item => new { item.TeacherUserId, item.ModelKey }).IsUnique();
            entity.HasIndex(item => item.TeacherUserId);
        });

        modelBuilder.Entity<TeacherModelUsage>(entity =>
        {
            entity.Property(item => item.TeacherUserId).HasMaxLength(450);
            entity.Property(item => item.ModelKey).HasMaxLength(300);
            entity.HasIndex(item => new
            {
                item.TeacherUserId,
                item.ModelKey,
                item.PeriodStart,
                item.PeriodSeconds
            }).IsUnique();
            entity.HasIndex(item => item.TeacherUserId);
        });

    }
}
