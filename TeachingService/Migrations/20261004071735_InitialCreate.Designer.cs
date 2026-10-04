using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using TeachingService.Data;

#nullable disable

namespace TeachingService.Migrations
{
    [DbContext(typeof(TeachingDbContext))]
    [Migration("20261004071735_InitialCreate")]
    partial class InitialCreate
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.17")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("TeachingService.Models.AnswerOption", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<bool>("IsCorrect")
                        .HasColumnType("boolean");

                    b.Property<Guid>("TestTaskId")
                        .HasColumnType("uuid");

                    b.Property<string>("Text")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("TestTaskId");

                    b.ToTable("AnswerOptions");
                });

            modelBuilder.Entity("TeachingService.Models.CourseTest", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<int>("NextVersionNumber")
                        .IsConcurrencyToken()
                        .HasColumnType("integer");

                    b.Property<string>("TeacherUserId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("TeacherUserId");

                    b.ToTable("Tests");
                });

            modelBuilder.Entity("TeachingService.Models.CourseTestVersion", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<int>("ContentRevision")
                        .IsConcurrencyToken()
                        .HasColumnType("integer");

                    b.Property<Guid>("CourseTestId")
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int?>("LlmModelId")
                        .HasColumnType("integer");

                    b.Property<DateTimeOffset?>("PublishedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Subject")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Summary")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("TimeLimitMinutes")
                        .HasColumnType("integer");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("VersionNumber")
                        .HasColumnType("integer");

                    b.Property<string>("VersionSlot")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("CourseTestId", "VersionNumber")
                        .IsUnique();

                    b.HasIndex("CourseTestId", "VersionSlot")
                        .IsUnique();

                    b.ToTable("TestVersions");
                });

            modelBuilder.Entity("TeachingService.Models.IntegrationOutboxMessage", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("EventType")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("LastError")
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)");

                    b.Property<DateTimeOffset>("NextPublishAttemptAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("OccurredAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Payload")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<int>("PublishAttempts")
                        .HasColumnType("integer");

                    b.Property<DateTimeOffset?>("PublishedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("RoutingKey")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("TraceParent")
                        .HasMaxLength(55)
                        .HasColumnType("character varying(55)");

                    b.Property<string>("TraceState")
                        .HasMaxLength(512)
                        .HasColumnType("character varying(512)");

                    b.HasKey("Id");

                    b.HasIndex("PublishedAt", "NextPublishAttemptAt");

                    b.ToTable("IntegrationOutboxMessages");
                });

            modelBuilder.Entity("TeachingService.Models.StudyGroup", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<int>("Revision")
                        .IsConcurrencyToken()
                        .HasColumnType("integer");

                    b.Property<string>("TeacherUserId")
                        .IsRequired()
                        .HasMaxLength(450)
                        .HasColumnType("character varying(450)");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("TeacherUserId");

                    b.ToTable("StudyGroups");
                });

            modelBuilder.Entity("TeachingService.Models.StudyGroupStudent", b =>
                {
                    b.Property<Guid>("StudyGroupId")
                        .HasColumnType("uuid");

                    b.Property<string>("StudentUserId")
                        .HasMaxLength(450)
                        .HasColumnType("character varying(450)");

                    b.Property<DateTimeOffset>("AddedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("StudyGroupId", "StudentUserId");

                    b.HasIndex("StudentUserId");

                    b.ToTable("StudyGroupStudents");
                });

            modelBuilder.Entity("TeachingService.Models.TestAssignment", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("AvailableFrom")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("CourseTestVersionId")
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("Deadline")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("StudyGroupId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("CourseTestVersionId");

                    b.HasIndex("StudyGroupId", "Deadline");

                    b.ToTable("TestAssignments");
                });

            modelBuilder.Entity("TeachingService.Models.TestReviewPolicyRevision", b =>
                {
                    b.Property<Guid>("TestId")
                        .HasColumnType("uuid");

                    b.Property<int>("Revision")
                        .HasColumnType("integer");

                    b.Property<Guid>("EventId")
                        .HasColumnType("uuid");

                    b.Property<string>("Payload")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<DateTimeOffset>("PublishedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("TestId", "Revision");

                    b.HasIndex("EventId")
                        .IsUnique();

                    b.ToTable("TestReviewPolicyRevisions");
                });

            modelBuilder.Entity("TeachingService.Models.TestTask", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("CheckMode")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid>("CourseTestVersionId")
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<bool>("IsHidden")
                        .HasColumnType("boolean");

                    b.Property<decimal>("MaxPoints")
                        .HasColumnType("numeric");

                    b.Property<string>("Prompt")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<decimal>("WrongAnswerPenalty")
                        .HasColumnType("numeric");

                    b.HasKey("Id");

                    b.HasIndex("CourseTestVersionId");

                    b.ToTable("TestTasks");
                });

            modelBuilder.Entity("TeachingService.Models.AnswerOption", b =>
                {
                    b.HasOne("TeachingService.Models.TestTask", null)
                        .WithMany("Options")
                        .HasForeignKey("TestTaskId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("TeachingService.Models.CourseTestVersion", b =>
                {
                    b.HasOne("TeachingService.Models.CourseTest", "CourseTest")
                        .WithMany("Versions")
                        .HasForeignKey("CourseTestId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.OwnsMany("TeachingService.Models.GradingExample", "GradingExamples", b1 =>
                        {
                            b1.Property<Guid>("CourseTestVersionId")
                                .HasColumnType("uuid");

                            b1.Property<int>("__synthesizedOrdinal")
                                .ValueGeneratedOnAdd()
                                .HasColumnType("integer");

                            b1.Property<string>("Feedback")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasAnnotation("Relational:JsonPropertyName", "feedback");

                            b1.Property<decimal>("MaxScore")
                                .HasColumnType("numeric")
                                .HasAnnotation("Relational:JsonPropertyName", "maxScore");

                            b1.Property<decimal>("Score")
                                .HasColumnType("numeric")
                                .HasAnnotation("Relational:JsonPropertyName", "score");

                            b1.Property<string>("StudentAnswer")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasAnnotation("Relational:JsonPropertyName", "studentAnswer");

                            b1.Property<string>("TaskPrompt")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasAnnotation("Relational:JsonPropertyName", "taskPrompt");

                            b1.HasKey("CourseTestVersionId", "__synthesizedOrdinal");

                            b1.ToTable("TestVersions");

                            b1
                                .ToJson("GradingExamples")
                                .HasColumnType("jsonb");

                            b1.WithOwner()
                                .HasForeignKey("CourseTestVersionId");
                        });

                    b.Navigation("CourseTest");

                    b.Navigation("GradingExamples");
                });

            modelBuilder.Entity("TeachingService.Models.StudyGroupStudent", b =>
                {
                    b.HasOne("TeachingService.Models.StudyGroup", "StudyGroup")
                        .WithMany("Students")
                        .HasForeignKey("StudyGroupId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("StudyGroup");
                });

            modelBuilder.Entity("TeachingService.Models.TestAssignment", b =>
                {
                    b.HasOne("TeachingService.Models.CourseTestVersion", "CourseTestVersion")
                        .WithMany()
                        .HasForeignKey("CourseTestVersionId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("TeachingService.Models.StudyGroup", "StudyGroup")
                        .WithMany("Assignments")
                        .HasForeignKey("StudyGroupId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("CourseTestVersion");

                    b.Navigation("StudyGroup");
                });

            modelBuilder.Entity("TeachingService.Models.TestReviewPolicyRevision", b =>
                {
                    b.HasOne("TeachingService.Models.CourseTest", null)
                        .WithMany()
                        .HasForeignKey("TestId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("TeachingService.Models.TestTask", b =>
                {
                    b.HasOne("TeachingService.Models.CourseTestVersion", null)
                        .WithMany("Tasks")
                        .HasForeignKey("CourseTestVersionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.OwnsMany("TeachingService.Models.GradingExample", "GradingExamples", b1 =>
                        {
                            b1.Property<Guid>("TestTaskId")
                                .HasColumnType("uuid");

                            b1.Property<int>("__synthesizedOrdinal")
                                .ValueGeneratedOnAdd()
                                .HasColumnType("integer");

                            b1.Property<string>("Feedback")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasAnnotation("Relational:JsonPropertyName", "feedback");

                            b1.Property<decimal>("MaxScore")
                                .HasColumnType("numeric")
                                .HasAnnotation("Relational:JsonPropertyName", "maxScore");

                            b1.Property<decimal>("Score")
                                .HasColumnType("numeric")
                                .HasAnnotation("Relational:JsonPropertyName", "score");

                            b1.Property<string>("StudentAnswer")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasAnnotation("Relational:JsonPropertyName", "studentAnswer");

                            b1.Property<string>("TaskPrompt")
                                .IsRequired()
                                .HasColumnType("text")
                                .HasAnnotation("Relational:JsonPropertyName", "taskPrompt");

                            b1.HasKey("TestTaskId", "__synthesizedOrdinal");

                            b1.ToTable("TestTasks");

                            b1
                                .ToJson("GradingExamples")
                                .HasColumnType("jsonb");

                            b1.WithOwner()
                                .HasForeignKey("TestTaskId");
                        });

                    b.Navigation("GradingExamples");
                });

            modelBuilder.Entity("TeachingService.Models.CourseTest", b =>
                {
                    b.Navigation("Versions");
                });

            modelBuilder.Entity("TeachingService.Models.CourseTestVersion", b =>
                {
                    b.Navigation("Tasks");
                });

            modelBuilder.Entity("TeachingService.Models.StudyGroup", b =>
                {
                    b.Navigation("Assignments");

                    b.Navigation("Students");
                });

            modelBuilder.Entity("TeachingService.Models.TestTask", b =>
                {
                    b.Navigation("Options");
                });
#pragma warning restore 612, 618
        }
    }
}
