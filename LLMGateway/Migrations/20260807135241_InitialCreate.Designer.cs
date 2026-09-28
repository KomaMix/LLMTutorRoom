using System;
using LLMGateway.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LLMGateway.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260807135241_InitialCreate")]
    partial class InitialCreate
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.17")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("LLMGateway.Data.Models.Model", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Description")
                        .HasColumnType("text");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool>("IsArchived")
                        .HasColumnType("boolean");

                    b.Property<string>("Key")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("Key")
                        .IsUnique();

                    b.ToTable("Models");
                });

            modelBuilder.Entity("LLMGateway.Data.Models.ModelDeployment", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("ApiKey")
                        .HasColumnType("text");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Endpoint")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("boolean");

                    b.Property<int?>("MaxConcurrentRequests")
                        .HasColumnType("integer");

                    b.Property<int>("ModelId")
                        .HasColumnType("integer");

                    b.Property<int>("Priority")
                        .HasColumnType("integer");

                    b.Property<string>("ProviderModelId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("ModelId");

                    b.ToTable("ModelDeployments");
                });

            modelBuilder.Entity("LLMGateway.Data.Models.ModelRateLimitRule", b =>
                {
                    b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("integer");
                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));
                    b.Property<int>("DeploymentId").HasColumnType("integer");
                    b.Property<int>("WindowSeconds").HasColumnType("integer");
                    b.Property<int>("MaxRequests").HasColumnType("integer");
                    b.HasKey("Id");
                    b.HasIndex("DeploymentId");
                    b.ToTable("ModelRateLimitRules");
                });

            modelBuilder.Entity("LLMGateway.Data.Models.ModelRateLimitRule", b =>
                {
                    b.HasOne("LLMGateway.Data.Models.ModelDeployment", "Deployment")
                        .WithMany("RateLimitRules").HasForeignKey("DeploymentId")
                        .OnDelete(DeleteBehavior.Cascade).IsRequired();
                    b.Navigation("Deployment");
                });

            modelBuilder.Entity("LLMGateway.Data.Models.ModelDeployment", b =>
                {
                    b.HasOne("LLMGateway.Data.Models.Model", "Model")
                        .WithMany("Deployments")
                        .HasForeignKey("ModelId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Model");
                    b.Navigation("RateLimitRules");
                });

            modelBuilder.Entity("LLMGateway.Data.Models.Model", b =>
                {
                    b.Navigation("Deployments");
                });
#pragma warning restore 612, 618
        }
    }
}
