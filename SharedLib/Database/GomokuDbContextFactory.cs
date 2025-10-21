using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SharedLib.Database;

public class GomokuDbContextFactory : IDesignTimeDbContextFactory<GomokuDbContext>
{
    public GomokuDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GomokuDbContext>();
        
        // Use a default connection string for migrations
        var connectionString = "Server=(localdb)\\mssqllocaldb;Database=GomokuGameDB;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true";
        
        optionsBuilder.UseSqlServer(connectionString);

        return new GomokuDbContext(optionsBuilder.Options);
    }
}