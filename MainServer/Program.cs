namespace MainServer;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Distributed Gomoku Server ===");
        Console.WriteLine("Starting server...");
        Console.WriteLine("Note: Ensure WorkerServer is running on port 6000 for full distributed features");
        Console.WriteLine();

        var server = new MainServer(5000);

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
