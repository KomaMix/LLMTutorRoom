using AuthService.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Data
{
    public sealed class AuthDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(user => user.UserName)
                    .IsRequired()
                    .HasMaxLength(64);
                entity.Property(user => user.Email).IsRequired();
                entity.Property(user => user.NormalizedEmail).IsRequired();
                entity.HasIndex(user => user.NormalizedEmail)
                    .IsUnique()
                    .HasDatabaseName("EmailIndex");
            });
        }
    }
}
