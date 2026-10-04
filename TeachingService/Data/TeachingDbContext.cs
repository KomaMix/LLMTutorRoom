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
        public DbSet<StudyGroup> StudyGroups { get; set; } = null!;
        public DbSet<StudyGroupStudent> StudyGroupStudents { get; set; } = null!;
        public DbSet<TestAssignment> TestAssignments { get; set; } = null!;

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
                entity.OwnsMany(version => version.GradingExamples, examples =>
                {
                    ConfigureGradingExamples(examples);
                    examples.ToJson("GradingExamples");
                });
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
                entity.OwnsMany(task => task.GradingExamples, examples =>
                {
                    ConfigureGradingExamples(examples);
                    examples.ToJson("GradingExamples");
                });
                entity.HasMany(task => task.Options)
                    .WithOne()
                    .HasForeignKey(option => option.TestTaskId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AnswerOption>(entity =>
            {
                entity.HasKey(option => option.Id);
            });

            modelBuilder.Entity<StudyGroup>(entity =>
            {
                entity.HasKey(group => group.Id);
                entity.Property(group => group.Name).HasMaxLength(200);
                entity.Property(group => group.TeacherUserId).HasMaxLength(450);
                entity.Property(group => group.Revision).IsConcurrencyToken();
                entity.HasIndex(group => group.TeacherUserId);
                entity.HasMany(group => group.Students)
                    .WithOne(student => student.StudyGroup)
                    .HasForeignKey(student => student.StudyGroupId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(group => group.Assignments)
                    .WithOne(assignment => assignment.StudyGroup)
                    .HasForeignKey(assignment => assignment.StudyGroupId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<StudyGroupStudent>(entity =>
            {
                entity.HasKey(student => new
                {
                    student.StudyGroupId,
                    student.StudentUserId
                });
                entity.Property(student => student.StudentUserId).HasMaxLength(450);
                entity.HasIndex(student => student.StudentUserId);
            });

            modelBuilder.Entity<TestAssignment>(entity =>
            {
                entity.HasKey(assignment => assignment.Id);
                entity.HasIndex(assignment => new
                {
                    assignment.StudyGroupId,
                    assignment.Deadline
                });
                entity.HasIndex(assignment => assignment.CourseTestVersionId);
                entity.HasOne(assignment => assignment.CourseTestVersion)
                    .WithMany()
                    .HasForeignKey(assignment => assignment.CourseTestVersionId)
                    .OnDelete(DeleteBehavior.Restrict);
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
                entity.Property(message => message.TraceParent).HasMaxLength(55);
                entity.Property(message => message.TraceState).HasMaxLength(512);
            });
        }

        private static void ConfigureGradingExamples<TOwner>(
            Microsoft.EntityFrameworkCore.Metadata.Builders.OwnedNavigationBuilder<TOwner, GradingExample> examples)
            where TOwner : class
        {
            examples.Property(example => example.TaskPrompt).HasJsonPropertyName("taskPrompt");
            examples.Property(example => example.StudentAnswer).HasJsonPropertyName("studentAnswer");
            examples.Property(example => example.Score).HasJsonPropertyName("score");
            examples.Property(example => example.MaxScore).HasJsonPropertyName("maxScore");
            examples.Property(example => example.Feedback).HasJsonPropertyName("feedback");
        }
    }
}
