using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

namespace TicTacToeServer;

public class DistributedTicTacToeServer : IGameServer
{
    private TcpListener? _tcpListener;
    private readonly int _clientPort;
    private readonly int _serverPort;
    private readonly string _serverId;
    private bool _isRunning = false;
    private readonly ConcurrentDictionary<string, ClientHandler> _clients = new();
    private readonly DistributedMatchmakingService _matchmakingService;
    private readonly ServerRegistry _serverRegistry;
    private readonly ServerCommunicator _serverCommunicator;
    private readonly List<WorkerBot> _workerBots = new();
    private readonly int _botCount;
    private readonly Timer _heartbeatTimer;
    private readonly Timer _metricsTimer;

    public string ServerId => _serverId;
    public int ActivePlayersCount => _clients.Count;
    public int ActiveGamesCount => _matchmakingService.ActiveRoomsCount;

    public DistributedTicTacToeServer(string serverId, int clientPort = 8080, int serverPort = 9000, int botCount = 3)
    {
        _serverId = serverId;
        _clientPort = clientPort;
        _serverPort = serverPort;
        _botCount = botCount;
        
        _serverRegistry = new ServerRegistry(serverId);
        _serverCommunicator = new ServerCommunicator(serverId, serverPort, _serverRegistry);
        _matchmakingService = new DistributedMatchmakingService(serverId, _serverRegistry, _serverCommunicator);

        // Setup heartbeat timer
        _heartbeatTimer = new Timer(SendHeartbeatAsync, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
        
        // Setup metrics timer
        _metricsTimer = new Timer(LogMetricsAsync, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        // Subscribe to server registry events
        _serverRegistry.ServerAdded += OnServerAdded;
        _serverRegistry.ServerRemoved += OnServerRemoved;
        _serverRegistry.ServerUpdated += OnServerUpdated;

        // Subscribe to server communication events
        _serverCommunicator.MessageReceived += OnServerMessageReceived;
    }

    public void RegisterPeerServer(string serverId, string host, int clientPort, int serverPort)
    {
        var serverInfo = new ServerInfo
        {
            ServerId = serverId,
            Host = host,
            ClientPort = clientPort,
            ServerPort = serverPort,
            LastHeartbeat = DateTime.Now,
            Status = ServerStatus.Online
        };
        
        _serverRegistry.RegisterServer(serverInfo);
    }

    public async Task StartAsync()
    {
        try
        {
            // Start server communication
            await _serverCommunicator.StartAsync();

            // Start TCP listener for clients
            _tcpListener = new TcpListener(IPAddress.Any, _clientPort);
            _tcpListener.Start();
            _isRunning = true;

            Console.WriteLine($"[{_serverId}] Distributed Tic-Tac-Toe Server started");
            Console.WriteLine($"[{_serverId}] Client port: {_clientPort}");
            Console.WriteLine($"[{_serverId}] Server port: {_serverPort}");

            // Register this server
            var currentServerInfo = GetCurrentServerInfo();
            await _serverCommunicator.BroadcastServerJoin(currentServerInfo);

            // Start worker bots
            StartWorkerBots();

            // Start background tasks
            _ = Task.Run(CleanupRoomsAsync);
            _ = Task.Run(AcceptClientsAsync);
            _ = Task.Run(HealthCheckAsync);

            Console.WriteLine($"[{_serverId}] All services started successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_serverId}] Error starting server: {ex.Message}");
            throw;
        }
    }

    private void StartWorkerBots()
    {
        var difficulties = new[] { "easy", "medium", "hard" };
        
        for (int i = 0; i < _botCount; i++)
        {
            string difficulty = difficulties[i % difficulties.Length];
            var bot = new WorkerBot($"Bot-{_serverId}-{i:00}", difficulty);
            _workerBots.Add(bot);

            // Connect bot to this server with staggered timing
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000 * (i + 1)); // Stagger bot connections
                await bot.ConnectAndPlayAsync("localhost", _clientPort);
            });

            Console.WriteLine($"[{_serverId}] Worker bot started: {bot.BotId} ({difficulty})");
        }
    }

    private async Task AcceptClientsAsync()
    {
        while (_isRunning && _tcpListener != null)
        {
            try
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                var clientHandler = new ClientHandler(tcpClient, (IGameServer)this);
                _clients[clientHandler.ClientId] = clientHandler;

                Console.WriteLine($"[{_serverId}] New client connected: {clientHandler.ClientId}");

                _ = Task.Run(() => clientHandler.HandleClientAsync());
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    Console.WriteLine($"[{_serverId}] Error accepting client: {ex.Message}");
                }
            }
        }
    }

    private async Task HealthCheckAsync()
    {
        while (_isRunning)
        {
            try
            {
                var servers = _serverRegistry.GetAllServers();
                
                foreach (var server in servers)
                {
                    if (server.ServerId != _serverId && server.Status == ServerStatus.Online)
                    {
                        await _serverCommunicator.SendPingAsync(server.ServerId);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(15));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_serverId}] Error in health check: {ex.Message}");
            }
        }
    }

    private async Task CleanupRoomsAsync()
    {
        while (_isRunning)
        {
            try
            {
                await _matchmakingService.CleanupRoomsAsync();
                await Task.Delay(TimeSpan.FromMinutes(2));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_serverId}] Error in cleanup: {ex.Message}");
            }
        }
    }

    private async void SendHeartbeatAsync(object? state)
    {
        if (!_isRunning) return;

        try
        {
            var serverInfo = GetCurrentServerInfo();
            await _serverCommunicator.SendHeartbeatAsync(serverInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_serverId}] Error sending heartbeat: {ex.Message}");
        }
    }

    private async void LogMetricsAsync(object? state)
    {
        if (!_isRunning) return;

        try
        {
            var serverInfo = GetCurrentServerInfo();
            var connectedServers = _serverRegistry.GetAvailableServers().Count;
            
            Console.WriteLine($"[{_serverId}] === METRICS ===");
            Console.WriteLine($"[{_serverId}] Active Players: {serverInfo.ActivePlayers}");
            Console.WriteLine($"[{_serverId}] Active Games: {serverInfo.ActiveGames}");
            Console.WriteLine($"[{_serverId}] Connected Servers: {connectedServers}");
            Console.WriteLine($"[{_serverId}] Worker Bots: {_workerBots.Count(b => b.IsInGame)} in-game / {_workerBots.Count} total");
            Console.WriteLine($"[{_serverId}] CPU Usage: {serverInfo.CpuUsage:F1}%");
            Console.WriteLine($"[{_serverId}] Memory Usage: {serverInfo.MemoryUsage / 1024 / 1024} MB");
            Console.WriteLine($"[{_serverId}] ===============");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_serverId}] Error logging metrics: {ex.Message}");
        }
    }

    private ServerInfo GetCurrentServerInfo()
    {
        // Get current process metrics
        var process = Process.GetCurrentProcess();
        var cpuUsage = GetCpuUsage();
        var memoryUsage = process.WorkingSet64;

        return new ServerInfo
        {
            ServerId = _serverId,
            Host = "localhost",
            ClientPort = _clientPort,
            ServerPort = _serverPort,
            LastHeartbeat = DateTime.Now,
            ActivePlayers = _clients.Count,
            ActiveGames = _matchmakingService.ActiveRoomsCount,
            Status = ServerStatus.Online,
            CpuUsage = cpuUsage,
            MemoryUsage = memoryUsage
        };
    }

    private double GetCpuUsage()
    {
        try
        {
            // Simplified CPU usage calculation
            var process = Process.GetCurrentProcess();
            return (Environment.ProcessorCount * 100.0) / (process.TotalProcessorTime.TotalMilliseconds + 1);
        }
        catch
        {
            return 0.0;
        }
    }

    // Event handlers
    private async void OnServerAdded(ServerInfo server)
    {
        Console.WriteLine($"[{_serverId}] New server discovered: {server.ServerId}");
        await _serverCommunicator.ConnectToServerAsync(server);
    }

    private void OnServerRemoved(ServerInfo server)
    {
        Console.WriteLine($"[{_serverId}] Server removed: {server.ServerId}");
    }

    private void OnServerUpdated(ServerInfo server)
    {
        if (server.Status == ServerStatus.Offline)
        {
            Console.WriteLine($"[{_serverId}] Server went offline: {server.ServerId}");
            // Handle failover logic here
        }
    }

    private async void OnServerMessageReceived(ServerMessage message)
    {
        Console.WriteLine($"[{_serverId}] Received message from {message.SenderId}: {message.Type}");
        
        switch (message.Type)
        {
            case "METRICS_REQUEST":
                await HandleMetricsRequest(message);
                break;
        }
    }

    private async Task HandleMetricsRequest(ServerMessage message)
    {
        var serverInfo = GetCurrentServerInfo();
        // Send metrics response back to requesting server
        // Implementation depends on specific requirements
    }

    // TicTacToeServer compatibility methods
    public Task<GameRoom> FindOrCreateRoom(ClientHandler client)
    {
        return _matchmakingService.FindOrCreateRoom(client);
    }

    public async Task StartGame(GameRoom room)
    {
        if (room.IsGameActive)
            return;

        room.IsGameActive = true;
        room.LastActivity = DateTime.Now;

        var startMessage = $"GAME_START:{{\"roomId\":\"{room.RoomId}\",\"currentPlayer\":\"{room.CurrentPlayer}\"}}";

        if (room.Player1 != null)
            await room.Player1.SendMessage(startMessage);

        if (room.Player2 != null)
            await room.Player2.SendMessage(startMessage);

        Console.WriteLine($"[{_serverId}] Game started in room {room.RoomId}");
    }

    public async Task<bool> ProcessGameMove(GameRoom room, ClientHandler player, MoveData move)
    {
        // Validate move
        if (move.row < 0 || move.row >= 15 || move.col < 0 || move.col >= 15)
        {
            await player.SendMessage("ERROR:Invalid move coordinates");
            return false;
        }

        if (!string.IsNullOrEmpty(room.Board[move.row, move.col]))
        {
            await player.SendMessage("ERROR:Cell already occupied");
            return false;
        }

        // Apply move
        room.Board[move.row, move.col] = player.PlayerSymbol;
        room.LastActivity = DateTime.Now;

        // Sync game state with other servers if it's a distributed game
        if (room is DistributedGameRoom distributedRoom)
        {
            await _serverCommunicator.SyncGameState(room, distributedRoom.GetAllServerIds());
        }

        // Check for win condition
        if (GameLogic.CheckWin(room.Board, move.row, move.col, player.PlayerSymbol))
        {
            await EndGame(room, player, "WIN");
            return true;
        }

        // Check for draw
        if (GameLogic.IsBoardFull(room.Board))
        {
            await EndGame(room, null, "DRAW");
            return true;
        }

        // Switch turns
        room.CurrentPlayer = room.CurrentPlayer == "X" ? "O" : "X";

        var turnMessage = $"TURN_CHANGE:{{\"currentPlayer\":\"{room.CurrentPlayer}\"}}";
        if (room.Player1 != null)
            await room.Player1.SendMessage(turnMessage);
        if (room.Player2 != null)
            await room.Player2.SendMessage(turnMessage);

        return true;
    }

    private async Task EndGame(GameRoom room, ClientHandler? winner, string reason)
    {
        room.IsGameActive = false;

        string endMessage = $"GAME_END:{{\"reason\":\"{reason}\",\"winner\":\"{winner?.PlayerSymbol ?? "NONE"}\"}}";

        if (room.Player1 != null)
            await room.Player1.SendMessage(endMessage);

        if (room.Player2 != null)
            await room.Player2.SendMessage(endMessage);

        Console.WriteLine($"[{_serverId}] Game ended in room {room.RoomId}: {reason}");
    }

    public async Task LeaveRoom(ClientHandler client, GameRoom room)
    {
        _matchmakingService.LeaveRoom(client, room);

        var opponent = room.GetOpponent(client);
        if (opponent != null)
        {
            await opponent.SendMessage("OPPONENT_LEFT:Your opponent left the game");
        }
    }

    public void RemoveClient(ClientHandler client)
    {
        _clients.TryRemove(client.ClientId, out _);
    }

    public void Stop()
    {
        Console.WriteLine($"[{_serverId}] Stopping server...");
        
        _isRunning = false;
        
        // Stop all bots
        foreach (var bot in _workerBots)
        {
            bot.Stop();
        }
        
        // Stop network components
        _tcpListener?.Stop();
        _serverCommunicator.Stop();
        
        // Stop timers
        _heartbeatTimer?.Dispose();
        _metricsTimer?.Dispose();
        
        // Dispose registry
        _serverRegistry.Dispose();
        
        Console.WriteLine($"[{_serverId}] Server stopped");
    }
}