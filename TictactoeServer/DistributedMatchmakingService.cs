using System.Collections.Concurrent;

namespace TicTacToeServer;

public class WaitingPlayer
{
    public string PlayerId { get; set; } = string.Empty;
    public string ServerId { get; set; } = string.Empty;
    public PlayerInfo PlayerInfo { get; set; } = new();
    public DateTime JoinTime { get; set; }
}

public class DistributedMatchmakingService
{
    private readonly ConcurrentDictionary<string, GameRoom> _gameRooms = new();
    private readonly ConcurrentQueue<WaitingPlayer> _globalWaitingQueue = new();
    private readonly Queue<ClientHandler> _localWaitingPlayers = new();
    private readonly object _waitingLock = new object();
    private readonly ServerRegistry _serverRegistry;
    private readonly ServerCommunicator _serverCommunicator;
    private readonly string _serverId;

    public int ActiveRoomsCount => _gameRooms.Count;

    public DistributedMatchmakingService(string serverId, ServerRegistry serverRegistry, ServerCommunicator serverCommunicator)
    {
        _serverId = serverId;
        _serverRegistry = serverRegistry;
        _serverCommunicator = serverCommunicator;

        // Subscribe to server communication events
        _serverCommunicator.MessageReceived += HandleServerMessage;
    }

    public async Task<GameRoom> FindOrCreateRoom(ClientHandler client)
    {
        GameRoom? room = null;
        ClientHandler? localWaitingPlayer = null;

        // Try local matching first
        lock (_waitingLock)
        {
            if (_localWaitingPlayers.Count > 0)
            {
                localWaitingPlayer = _localWaitingPlayers.Dequeue();
                if (localWaitingPlayer.IsConnected && localWaitingPlayer.CurrentRoom != null)
                {
                    if (localWaitingPlayer.CurrentRoom.AddPlayer(client))
                    {
                        room = localWaitingPlayer.CurrentRoom;
                    }
                }
                else
                {
                    // Player disconnected, try next
                    localWaitingPlayer = null;
                }
            }
        }

        if (room != null)
        {
            Console.WriteLine($"[{_serverId}] Local match found for {client.ClientId}");
            await client.SendMessage($"PLAYER_SYMBOL:{client.PlayerSymbol}");
            return room;
        }

        // Try global matching
        room = await TryGlobalMatching(client);
        if (room != null)
        {
            Console.WriteLine($"[{_serverId}] Global match found for {client.ClientId}");
            await client.SendMessage($"PLAYER_SYMBOL:{client.PlayerSymbol}");
            return room;
        }

        // Create new room and add to waiting queue
        var roomId = Guid.NewGuid().ToString();
        room = new GameRoom(roomId);
        _gameRooms[roomId] = room;

        if (room.AddPlayer(client))
        {
            lock (_waitingLock)
            {
                _localWaitingPlayers.Enqueue(client);
            }

            // Add to global waiting queue
            var globalWaitingPlayer = new WaitingPlayer
            {
                PlayerId = client.ClientId,
                ServerId = _serverId,
                PlayerInfo = client.PlayerInfo ?? new PlayerInfo(),
                JoinTime = DateTime.Now
            };
            _globalWaitingQueue.Enqueue(globalWaitingPlayer);

            // Broadcast to other servers that we have a waiting player
            await BroadcastWaitingPlayer(client);

            Console.WriteLine($"[{_serverId}] Player {client.ClientId} added to waiting queue");
        }

        await client.SendMessage($"PLAYER_SYMBOL:{client.PlayerSymbol}");
        return room;
    }

    private async Task<GameRoom?> TryGlobalMatching(ClientHandler client)
    {
        var availableServers = _serverRegistry.GetAvailableServers();
        
        foreach (var server in availableServers)
        {
            if (server.ServerId != _serverId && server.ActivePlayers > 0)
            {
                // Request match from this server
                await _serverCommunicator.RequestPlayerMatchAsync(server.ServerId, client.PlayerInfo ?? new PlayerInfo());
                
                // Wait briefly for response (in real implementation, this would be event-driven)
                await Task.Delay(100);
                
                // Check if a match was made (simplified logic)
                if (await CheckForRemoteMatch(client, server.ServerId))
                {
                    var roomId = Guid.NewGuid().ToString();
                    var room = new GameRoom(roomId);
                    _gameRooms[roomId] = room;
                    room.AddPlayer(client);
                    
                    await _serverCommunicator.NotifyGameCreated(room, server.ServerId);
                    return room;
                }
            }
        }

        return null;
    }

    private async Task<bool> CheckForRemoteMatch(ClientHandler client, string serverId)
    {
        // Simplified check - in real implementation, this would be based on actual server response
        var random = new Random();
        return random.Next(100) < 20; // 20% chance of finding a match
    }

    private async Task BroadcastWaitingPlayer(ClientHandler client)
    {
        var availableServers = _serverRegistry.GetAvailableServers();
        
        foreach (var server in availableServers)
        {
            if (server.ServerId != _serverId)
            {
                await _serverCommunicator.RequestPlayerMatchAsync(server.ServerId, client.PlayerInfo ?? new PlayerInfo());
            }
        }
    }

    private async void HandleServerMessage(ServerMessage message)
    {
        switch (message.Type)
        {
            case "PLAYER_MATCH_REQUEST":
                await HandlePlayerMatchRequest(message);
                break;
            case "GAME_CREATED":
                await HandleGameCreated(message);
                break;
            case "PLAYER_TRANSFER":
                await HandlePlayerTransfer(message);
                break;
            case "GAME_SYNC":
                await HandleGameSync(message);
                break;
        }
    }

    private async Task HandlePlayerMatchRequest(ServerMessage message)
    {
        // Check if we have waiting players
        ClientHandler? localPlayer = null;
        
        lock (_waitingLock)
        {
            if (_localWaitingPlayers.Count > 0)
            {
                localPlayer = _localWaitingPlayers.Dequeue();
            }
        }

        if (localPlayer != null && localPlayer.IsConnected)
        {
            // We have a match! Create cross-server game
            Console.WriteLine($"[{_serverId}] Cross-server match created with {message.SenderId}");
            
            // Notify the requesting server
            await _serverCommunicator.NotifyGameCreated(localPlayer.CurrentRoom!, message.SenderId);
        }
    }

    private async Task HandleGameCreated(ServerMessage message)
    {
        Console.WriteLine($"[{_serverId}] Game created notification from {message.SenderId}");
        // Handle game creation notification
    }

    private async Task HandlePlayerTransfer(ServerMessage message)
    {
        Console.WriteLine($"[{_serverId}] Player transfer request from {message.SenderId}");
        // Handle player transfer logic
    }

    private async Task HandleGameSync(ServerMessage message)
    {
        Console.WriteLine($"[{_serverId}] Game state sync from {message.SenderId}");
        // Handle game state synchronization
    }

    public void LeaveRoom(ClientHandler client, GameRoom room)
    {
        room.RemovePlayer(client);

        if (room.IsEmpty)
        {
            _gameRooms.TryRemove(room.RoomId, out _);
            Console.WriteLine($"[{_serverId}] Room {room.RoomId} removed");
        }

        // Remove from local waiting queue if present
        lock (_waitingLock)
        {
            var tempQueue = new Queue<ClientHandler>();
            while (_localWaitingPlayers.Count > 0)
            {
                var player = _localWaitingPlayers.Dequeue();
                if (player.ClientId != client.ClientId)
                {
                    tempQueue.Enqueue(player);
                }
            }
            
            while (tempQueue.Count > 0)
            {
                _localWaitingPlayers.Enqueue(tempQueue.Dequeue());
            }
        }

        // Remove from global waiting queue
        var validPlayers = new List<WaitingPlayer>();
        while (_globalWaitingQueue.TryDequeue(out var player))
        {
            if (player.PlayerId != client.ClientId)
            {
                validPlayers.Add(player);
            }
        }

        foreach (var player in validPlayers)
        {
            _globalWaitingQueue.Enqueue(player);
        }
    }

    public async Task CleanupRoomsAsync()
    {
        var cutoffTime = DateTime.Now.AddMinutes(-10);
        var roomsToRemove = new List<string>();

        foreach (var kvp in _gameRooms)
        {
            var room = kvp.Value;
            if (room.LastActivity < cutoffTime || room.IsEmpty)
            {
                roomsToRemove.Add(kvp.Key);
            }
        }

        foreach (var roomId in roomsToRemove)
        {
            if (_gameRooms.TryRemove(roomId, out _))
            {
                Console.WriteLine($"[{_serverId}] Cleaned up inactive room: {roomId}");
            }
        }

        // Cleanup old waiting players
        var validPlayers = new List<WaitingPlayer>();
        while (_globalWaitingQueue.TryDequeue(out var player))
        {
            if ((DateTime.Now - player.JoinTime).TotalMinutes < 5)
            {
                validPlayers.Add(player);
            }
        }

        foreach (var player in validPlayers)
        {
            _globalWaitingQueue.Enqueue(player);
        }

        // Cleanup disconnected local waiting players
        lock (_waitingLock)
        {
            var tempQueue = new Queue<ClientHandler>();
            while (_localWaitingPlayers.Count > 0)
            {
                var player = _localWaitingPlayers.Dequeue();
                if (player.IsConnected)
                {
                    tempQueue.Enqueue(player);
                }
            }
            
            while (tempQueue.Count > 0)
            {
                _localWaitingPlayers.Enqueue(tempQueue.Dequeue());
            }
        }
    }

    public GameRoom? GetRoom(string roomId)
    {
        _gameRooms.TryGetValue(roomId, out var room);
        return room;
    }

    public List<GameRoom> GetActiveRooms()
    {
        return _gameRooms.Values.Where(r => r.IsGameActive).ToList();
    }

    public int GetWaitingPlayersCount()
    {
        lock (_waitingLock)
        {
            return _localWaitingPlayers.Count;
        }
    }

    public List<WaitingPlayer> GetGlobalWaitingPlayers()
    {
        return _globalWaitingQueue.ToList();
    }
}