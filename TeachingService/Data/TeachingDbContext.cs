using Microsoft.EntityFrameworkCore;
using TeachingService.Models;

namespace TeachingService.Data
{
    public sealed class TeachingDbContext : DbContext
    {
        public DbSet<CourseTest> Tests { get; set; } = null!;
        public DbSet<CourseTestVersion> TestVersions { get; set; } = null!;
        public DbSet<TestTask> TestTasks { get; set; } = null!;
        public DbSet<AnswerOption> AnswerOptions { get; set; } = null!;
        public DbSet<TestReviewPolicyRevision> TestReviewPolicyRevisions { get; set; } = null!;
        public DbSet<IntegrationOutboxMessage> IntegrationOutboxMessages { get; set; } = null!;

        public TeachingDbContext(DbContextOptions<TeachingDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CourseTest>(entity =>
            {
                entity.HasKey(test => test.Id);
                entity.HasIndex(test => test.TeacherUserId);
                entity.Property(test => test.NextVersionNumber).IsConcurrencyToken();
                entity.HasMany(test => test.Versions)
                    .WithOne(version => version.CourseTest)
                    .HasForeignKey(version => version.CourseTestId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CourseTestVersion>(entity =>
            {
                entity.HasKey(version => version.Id);
                entity.HasIndex(version => new
                {
                    version.CourseTestId,
                    version.VersionNumber
                }).IsUnique();
                entity.HasIndex(version => new
                {
                    version.CourseTestId,
                    version.VersionSlot
                }).IsUnique();
                entity.Property(version => version.VersionSlot)
                    .HasConversion<string>();
                entity.Ignore(version => version.Status);
                entity.Property(version => version.ContentRevision).IsConcurrencyToken();
                entity.HasOne(version => version.CourseTest)
                    .WithMany(test => test.Versions)
                    .HasForeignKey(version => version.CourseTestId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(version => version.Tasks)
                    .WithOne()
                    .HasForeignKey(task => task.CourseTestVersionId)
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

            modelBuilder.Entity<TestReviewPolicyRevision>(entity =>
            {
                entity.HasKey(revision => new { revision.TestId, revision.Revision });
                entity.HasIndex(revision => revision.EventId).IsUnique();
                entity.Property(revision => revision.Payload).HasColumnType("jsonb");
                entity.HasOne<CourseTest>()
                    .WithMany()
                    .HasForeignKey(revision => revision.TestId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<IntegrationOutboxMessage>(entity =>
            {
                entity.HasKey(message => message.Id);
                entity.HasIndex(message => new
                {
                    message.PublishedAt,
                    message.NextPublishAttemptAt
                });
                entity.Property(message => message.EventType).HasMaxLength(200);
                entity.Property(message => message.RoutingKey).HasMaxLength(200);
                entity.Property(message => message.Payload).HasColumnType("jsonb");
                entity.Property(message => message.LastError).HasMaxLength(2000);
            });
        }
    }
}
