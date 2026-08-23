using AttemptService.Models;
using Microsoft.EntityFrameworkCore;

namespace AttemptService.Data;

public sealed class AttemptDbContext(DbContextOptions<AttemptDbContext> options)
    : DbContext(options)
{
    public DbSet<TestAttempt> TestAttempts { get; set; } = null!;
    public DbSet<AttemptSubmissionOutboxMessage> AttemptSubmissionOutboxMessages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestAttempt>(entity =>
        {
            entity.Property(attempt => attempt.Id)
                .ValueGeneratedNever();
            entity.Property(attempt => attempt.TestId)
                .HasMaxLength(100);
            entity.Property(attempt => attempt.StudentUserId)
                .HasMaxLength(450);
            entity.Property(attempt => attempt.Status)
                .HasConversion<string>()
                .HasMaxLength(32);
            entity.Property(attempt => attempt.AnswersJson)
                .HasColumnName("Answers")
                .HasColumnType("jsonb");
            entity.Property(attempt => attempt.AllowedTaskIdsJson)
                .HasColumnName("AllowedTaskIds")
                .HasColumnType("jsonb");
            entity.Property(attempt => attempt.StateRevision)
                .IsConcurrencyToken();
            entity.HasIndex(attempt => new { attempt.TestId, attempt.StudentUserId })
                .IsUnique();
            entity.HasIndex(attempt => new { attempt.StudentUserId, attempt.StartedAt })
                .IsDescending(false, true);
            entity.HasIndex(attempt => new { attempt.Status, attempt.EndsAt });
        });

        modelBuilder.Entity<AttemptSubmissionOutboxMessage>(entity =>
        {
            entity.Property(message => message.Id)
                .ValueGeneratedNever();
            entity.Property(message => message.PayloadJson)
                .HasColumnName("Payload")
                .HasColumnType("jsonb");
            entity.Property(message => message.LastError)
                .HasMaxLength(2000);
            entity.HasIndex(message => message.AttemptId)
                .IsUnique();
            entity.HasIndex(message => new { message.PublishedAt, message.NextPublishAt });
        });
    }
}
