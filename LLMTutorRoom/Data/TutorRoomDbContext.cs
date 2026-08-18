using LLMTutorRoom.Models;
using Microsoft.EntityFrameworkCore;
namespace LLMTutorRoom.Data
{
    public sealed class TutorRoomDbContext : DbContext
    {
        public DbSet<TestAttempt> TestAttempts { get; set; } = null!;
        public DbSet<AttemptSubmissionOutboxMessage> AttemptSubmissionOutboxMessages { get; set; } = null!;

        public TutorRoomDbContext(DbContextOptions<TutorRoomDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestAttempt>(entity =>
            {
                entity.Property(attempt => attempt.Status).HasConversion<string>();
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
            });

            modelBuilder.Entity<AttemptSubmissionOutboxMessage>(entity =>
            {
                entity.Property(message => message.PayloadJson)
                    .HasColumnName("Payload")
                    .HasColumnType("jsonb");
                entity.HasIndex(message => message.AttemptId)
                    .IsUnique();
                entity.HasIndex(message => new { message.PublishedAt, message.NextPublishAt });
            });
        }
    }
}
