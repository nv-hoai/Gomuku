using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SharedLib.Database;

/// <summary>
/// Design-time factory for creating DbContext during migrations
/// </summary>
public class GomokuDbContextFactory : IDesignTimeDbContextFactory<GomokuDbContext>
{
    public GomokuDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GomokuDbContext>();
        
        // Default connection string for migrations
        // This can be overridden by environment variables or args
        var connectionString = "Server=(localdb)\\mssqllocaldb;Database=GomokuGameDB;Trusted_Connection=True;MultipleActiveResultSets=true";
        
        optionsBuilder.UseSqlServer(connectionString);
        
        return new GomokuDbContext(optionsBuilder.Options);
    }
}
