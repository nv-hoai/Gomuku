namespace TicTacToeServer;

class Program
{
    private static readonly List<DistributedTicTacToeServer> _servers = new();

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Distributed Tic-Tac-Toe Server System ===\n");

        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nShutting down servers...");
            
            foreach (var server in _servers)
            {
                server.Stop();
            }
            
            Environment.Exit(0);
        };

        if (args.Length > 0 && args[0] == "single")
        {
            await RunSingleServer();
        }
        else
        {
            await RunMultipleServers();
        }
    }

    private static async Task RunSingleServer()
    {
        Console.WriteLine("Starting single server mode...\n");
        
        var server = new DistributedTicTacToeServer(
            serverId: "Server-Single",
            clientPort: 8080,
            serverPort: 9000,
            botCount: 3
        );
        
        _servers.Add(server);
        
        await server.StartAsync();
        
        Console.WriteLine("Server started. Press Ctrl+C to stop...\n");
        await Task.Delay(Timeout.Infinite);
    }

    private static async Task RunMultipleServers()
    {
        Console.WriteLine("Starting distributed server cluster...\n");

        // Create server instances
        var server1 = new DistributedTicTacToeServer("Server-Alpha", 8081, 9001, botCount: 2);
        var server2 = new DistributedTicTacToeServer("Server-Beta", 8082, 9002, botCount: 2);
        var server3 = new DistributedTicTacToeServer("Server-Gamma", 8083, 9003, botCount: 2);

        _servers.AddRange(new[] { server1, server2, server3 });

        // Configure peer servers (each server knows about the others)
        server1.RegisterPeerServer("Server-Beta", "localhost", 8082, 9002);
        server1.RegisterPeerServer("Server-Gamma", "localhost", 8083, 9003);

        server2.RegisterPeerServer("Server-Alpha", "localhost", 8081, 9001);
        server2.RegisterPeerServer("Server-Gamma", "localhost", 8083, 9003);

        server3.RegisterPeerServer("Server-Alpha", "localhost", 8081, 9001);
        server3.RegisterPeerServer("Server-Beta", "localhost", 8082, 9002);

        // Start all servers concurrently
        var startTasks = new List<Task>
        {
            Task.Run(() => server1.StartAsync()),
            Task.Run(() => server2.StartAsync()),
            Task.Run(() => server3.StartAsync())
        };

        Console.WriteLine("Starting servers...");
        await Task.Delay(2000); // Give servers time to start

        Console.WriteLine("\n=== SERVER CLUSTER STARTED ===");
        Console.WriteLine("Servers running:");
        Console.WriteLine($"  • Server-Alpha  : Client port 8081, Server port 9001");
        Console.WriteLine($"  • Server-Beta   : Client port 8082, Server port 9002");
        Console.WriteLine($"  • Server-Gamma  : Client port 8083, Server port 9003");
        Console.WriteLine("\nFeatures enabled:");
        Console.WriteLine("  ✓ Cross-server player matching");
        Console.WriteLine("  ✓ Distributed game state");
        Console.WriteLine("  ✓ Health monitoring & failover");
        Console.WriteLine("  ✓ AI worker bots (2 per server)");
        Console.WriteLine("  ✓ Real-time metrics");
        Console.WriteLine("\nPress Ctrl+C to stop all servers...\n");

        // Wait for user interrupt or server completion
        try
        {
            await Task.WhenAll(startTasks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in server cluster: {ex.Message}");
        }
    }
}
