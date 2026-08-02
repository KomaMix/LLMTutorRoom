using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LLMTutorRoom.Data
{
    public sealed class TutorRoomDbContextFactory : IDesignTimeDbContextFactory<TutorRoomDbContext>
    {
        public TutorRoomDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
            var options = new DbContextOptionsBuilder<TutorRoomDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            return new TutorRoomDbContext(options);
        }
    }
}
