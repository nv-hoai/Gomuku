using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedLib.Database;
using SharedLib.Services;

namespace MainServer;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Distributed Gomoku Server with Database ===");
        Console.WriteLine("Setting up services and database...");

        // Setup configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Ensure database is created and migrated
        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<GomokuDbContext>();
            try
            {
                await dbContext.Database.EnsureCreatedAsync();
                Console.WriteLine("Database connection established successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database connection failed: {ex.Message}");
                Console.WriteLine("Please check your connection string in appsettings.json");
                return;
            }
        }

        Console.WriteLine("Starting server...");
        Console.WriteLine("New Architecture: Workers connect TO this server");
        Console.WriteLine("- Game clients connect on port 5000");
        Console.WriteLine("- Workers connect on port 5001");
        Console.WriteLine("- Database integration enabled");
        Console.WriteLine("- Start WorkerServers after this MainServer is running");
        Console.WriteLine();

        var server = new MainServer(5000, 5001, serviceProvider);

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            server.Stop();
            serviceProvider.Dispose();
        };

        try
        {
            await server.StartAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server error: {ex.Message}");
        }

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        serviceProvider.Dispose();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Add DbContext
        services.AddDbContext<GomokuDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Add services
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IGameStatsService, GameStatsService>();

        Console.WriteLine("Services configured successfully!");
    }
}
