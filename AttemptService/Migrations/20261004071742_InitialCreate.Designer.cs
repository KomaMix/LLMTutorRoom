using System;
using AttemptService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AttemptService.Migrations
{
    [DbContext(typeof(AttemptDbContext))]
    [Migration("20261004071742_InitialCreate")]
    partial class InitialCreate
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.17")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("AttemptService.Models.AttemptSubmissionOutboxMessage", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid");

                    b.Property<Guid>("AttemptId")
                        .HasColumnType("uuid");

                    b.Property<string>("LastError")
                        .IsRequired()
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)");

                    b.Property<DateTimeOffset?>("NextPublishAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("OccurredAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("PayloadJson")
                        .IsRequired()
                        .HasColumnType("jsonb")
                        .HasColumnName("Payload");

                    b.Property<int>("PublishAttempts")
                        .HasColumnType("integer");

                    b.Property<DateTimeOffset?>("PublishedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("TraceParent")
                        .HasMaxLength(55)
                        .HasColumnType("character varying(55)");

                    b.Property<string>("TraceState")
                        .HasMaxLength(512)
                        .HasColumnType("character varying(512)");

                    b.HasKey("Id");

                    b.HasIndex("AttemptId")
                        .IsUnique();

                    b.HasIndex("PublishedAt", "NextPublishAt");

                    b.ToTable("AttemptSubmissionOutboxMessages");
                });

            modelBuilder.Entity("AttemptService.Models.TestAttempt", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid");

                    b.Property<string>("AllowedTaskIdsJson")
                        .IsRequired()
                        .HasColumnType("jsonb")
                        .HasColumnName("AllowedTaskIds");

                    b.Property<string>("AnswersJson")
                        .IsRequired()
                        .HasColumnType("jsonb")
                        .HasColumnName("Answers");

                    b.Property<Guid>("AssignmentId")
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("EndsAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("GroupId")
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("StartedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("StateRevision")
                        .IsConcurrencyToken()
                        .HasColumnType("integer");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.Property<string>("StudentName")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<string>("StudentUserId")
                        .IsRequired()
                        .HasMaxLength(450)
                        .HasColumnType("character varying(450)");

                    b.Property<string>("SubmissionReason")
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.Property<DateTimeOffset?>("SubmittedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("TeacherUserId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("TestId")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<int>("TestRevision")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("GroupId");

                    b.HasIndex("TeacherUserId");

                    b.HasIndex("AssignmentId", "StudentUserId")
                        .IsUnique();

                    b.HasIndex("Status", "EndsAt");

                    b.HasIndex("StudentUserId", "StartedAt")
                        .IsDescending(false, true);

                    b.ToTable("TestAttempts");
                });
#pragma warning restore 612, 618
        }
    }
}
