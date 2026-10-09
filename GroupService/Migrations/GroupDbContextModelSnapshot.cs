using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using GroupService.Data;

#nullable disable

namespace GroupService.Migrations;

[DbContext(typeof(GroupDbContext))]
partial class GroupDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "9.0.17")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("GroupService.Models.StudyGroup", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            entity.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone");

            entity.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            entity.Property<int>("Revision")
                .IsConcurrencyToken()
                .HasColumnType("integer");

            entity.Property<string>("TeacherUserId")
                .IsRequired()
                .HasMaxLength(450)
                .HasColumnType("character varying(450)");

            entity.Property<DateTimeOffset>("UpdatedAt")
                .HasColumnType("timestamp with time zone");

            entity.HasKey("Id");
            entity.HasIndex("TeacherUserId");
            entity.ToTable("StudyGroups");
        });

        modelBuilder.Entity("GroupService.Models.StudyGroupStudent", entity =>
        {
            entity.Property<Guid>("StudyGroupId")
                .HasColumnType("uuid");

            entity.Property<string>("StudentUserId")
                .HasMaxLength(450)
                .HasColumnType("character varying(450)");

            entity.Property<DateTimeOffset>("AddedAt")
                .HasColumnType("timestamp with time zone");

            entity.HasKey("StudyGroupId", "StudentUserId");
            entity.HasIndex("StudentUserId");
            entity.ToTable("StudyGroupStudents");
        });

        modelBuilder.Entity("GroupService.Models.StudyGroupStudent", entity =>
        {
            entity.HasOne("GroupService.Models.StudyGroup", "StudyGroup")
                .WithMany("Students")
                .HasForeignKey("StudyGroupId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.Navigation("StudyGroup");
        });

        modelBuilder.Entity("GroupService.Models.StudyGroup", entity =>
        {
            entity.Navigation("Students");
        });
    }
}
