using Microsoft.EntityFrameworkCore;
using GroupService.Models;

namespace GroupService.Data;

public class GroupDbContext : DbContext
{
    public DbSet<StudyGroup> StudyGroups { get; set; } = null!;
    public DbSet<StudyGroupStudent> StudyGroupStudents { get; set; } = null!;

    public GroupDbContext(DbContextOptions<GroupDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
    }
}
