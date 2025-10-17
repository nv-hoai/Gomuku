namespace MainServer;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Distributed Gomoku Server ===");
        Console.WriteLine("Starting server...");
        Console.WriteLine("New Architecture: Workers connect TO this server");
        Console.WriteLine("- Game clients connect on port 5000");
        Console.WriteLine("- Workers connect on port 5001");
        Console.WriteLine("- Start WorkerServers after this MainServer is running");
        Console.WriteLine();

        var server = new MainServer(5000, 5001);

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            server.Stop();
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
    }
}
