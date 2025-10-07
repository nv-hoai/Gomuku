using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace TicTacToeServer;

public class ServerMessage
{
    public string Type { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public object? Data { get; set; }
}

public class ServerCommunicator
{
    private readonly string _serverId;
    private readonly int _serverPort;
    private TcpListener? _serverListener;
    private readonly ConcurrentDictionary<string, TcpClient> _connections = new();
    private readonly ServerRegistry _registry;
    private bool _isRunning;

    public event Action<ServerMessage>? MessageReceived;

    public ServerCommunicator(string serverId, int serverPort, ServerRegistry registry)
    {
        _serverId = serverId;
        _serverPort = serverPort;
        _registry = registry;
    }

    public async Task StartAsync()
    {
        _serverListener = new TcpListener(IPAddress.Any, _serverPort);
        _serverListener.Start();
        _isRunning = true;

        Console.WriteLine($"[{_serverId}] Server communication started on port {_serverPort}");

        // Accept incoming server connections
        _ = Task.Run(AcceptServerConnectionsAsync);
    }

    private async Task AcceptServerConnectionsAsync()
    {
        while (_isRunning && _serverListener != null)
        {
            try
            {
                var tcpClient = await _serverListener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleServerConnectionAsync(tcpClient));
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    Console.WriteLine($"[{_serverId}] Error accepting server connection: {ex.Message}");
                }
            }
        }
    }

    private async Task HandleServerConnectionAsync(TcpClient client)
    {
        var stream = client.GetStream();
        var buffer = new byte[4096];
        string? remoteServerId = null;

        try
        {
            while (client.Connected && _isRunning)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                string messageJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                
                // Handle multiple messages in one packet
                var messages = messageJson.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var msg in messages)
                {
                    try
                    {
                        var message = JsonSerializer.Deserialize<ServerMessage>(msg);
                        if (message != null)
                        {
                            if (remoteServerId == null)
                            {
                                remoteServerId = message.SenderId;
                                _connections[remoteServerId] = client;
                                Console.WriteLine($"[{_serverId}] Connected to server: {remoteServerId}");
                            }

                            await ProcessServerMessageAsync(message);
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"[{_serverId}] Failed to parse message: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_serverId}] Error handling server connection: {ex.Message}");
        }
        finally
        {
            if (remoteServerId != null)
            {
                _connections.TryRemove(remoteServerId, out _);
                Console.WriteLine($"[{_serverId}] Disconnected from server: {remoteServerId}");
            }
            client.Close();
        }
    }

    private async Task ProcessServerMessageAsync(ServerMessage message)
    {
        switch (message.Type)
        {
            case "HEARTBEAT":
                await HandleHeartbeatAsync(message);
                break;
            case "PING":
                await SendPongAsync(message.SenderId);
                break;
            case "PONG":
                _registry.UpdateHeartbeat(message.SenderId);
                break;
            case "SERVER_JOIN":
                await HandleServerJoinAsync(message);
                break;
            case "METRICS_REQUEST":
                await HandleMetricsRequestAsync(message);
                break;
            case "GAME_CREATED":
            case "PLAYER_TRANSFER":
            case "GAME_SYNC":
            case "PLAYER_MATCH_REQUEST":
                MessageReceived?.Invoke(message);
                break;
        }
    }

    private async Task HandleHeartbeatAsync(ServerMessage message)
    {
        if (message.Data is JsonElement jsonElement)
        {
            try
            {
                var serverInfo = JsonSerializer.Deserialize<ServerInfo>(jsonElement.GetRawText());
                if (serverInfo != null)
                {
                    _registry.RegisterServer(serverInfo);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_serverId}] Failed to process heartbeat: {ex.Message}");
            }
        }
    }

    private async Task HandleServerJoinAsync(ServerMessage message)
    {
        if (message.Data is JsonElement jsonElement)
        {
            try
            {
                var serverInfo = JsonSerializer.Deserialize<ServerInfo>(jsonElement.GetRawText());
                if (serverInfo != null)
                {
                    _registry.RegisterServer(serverInfo);
                    await ConnectToServerAsync(serverInfo);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_serverId}] Failed to process server join: {ex.Message}");
            }
        }
    }

    private async Task HandleMetricsRequestAsync(ServerMessage message)
    {
        // This would be handled by the main server to provide current metrics
        MessageReceived?.Invoke(message);
    }

    public async Task ConnectToServerAsync(ServerInfo serverInfo)
    {
        if (serverInfo.ServerId == _serverId || _connections.ContainsKey(serverInfo.ServerId))
            return;

        try
        {
            var client = new TcpClient();
            await client.ConnectAsync(serverInfo.Host, serverInfo.ServerPort);
            _connections[serverInfo.ServerId] = client;

            Console.WriteLine($"[{_serverId}] Connected to server: {serverInfo.ServerId}");

            // Send initial message to identify ourselves
            var identityMessage = new ServerMessage
            {
                Type = "IDENTITY",
                SenderId = _serverId,
                Timestamp = DateTime.Now
            };

            await SendMessageToServerAsync(serverInfo.ServerId, identityMessage);

            // Handle incoming messages from this server
            _ = Task.Run(() => HandleServerConnectionAsync(client));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_serverId}] Failed to connect to server {serverInfo.ServerId}: {ex.Message}");
        }
    }

    public async Task BroadcastServerJoin(ServerInfo serverInfo)
    {
        var message = new ServerMessage
        {
            Type = "SERVER_JOIN",
            SenderId = _serverId,
            Timestamp = DateTime.Now,
            Data = serverInfo
        };

        await BroadcastMessageAsync(message);
    }

    public async Task NotifyGameCreated(GameRoom room, string targetServerId)
    {
        var message = new ServerMessage
        {
            Type = "GAME_CREATED",
            SenderId = _serverId,
            TargetId = targetServerId,
            Timestamp = DateTime.Now,
            Data = new { RoomId = room.RoomId, HostServer = _serverId }
        };

        await SendMessageToServerAsync(targetServerId, message);
    }

    public async Task RequestPlayerTransfer(string playerId, string targetServerId)
    {
        var message = new ServerMessage
        {
            Type = "PLAYER_TRANSFER",
            SenderId = _serverId,
            TargetId = targetServerId,
            Timestamp = DateTime.Now,
            Data = new { PlayerId = playerId }
        };

        await SendMessageToServerAsync(targetServerId, message);
    }

    public async Task SyncGameState(GameRoom room, List<string> serverIds)
    {
        var gameState = new
        {
            RoomId = room.RoomId,
            Board = room.Board,
            CurrentPlayer = room.CurrentPlayer,
            IsGameActive = room.IsGameActive,
            Player1Id = room.Player1?.ClientId,
            Player2Id = room.Player2?.ClientId
        };

        var message = new ServerMessage
        {
            Type = "GAME_SYNC",
            SenderId = _serverId,
            Timestamp = DateTime.Now,
            Data = gameState
        };

        foreach (var serverId in serverIds)
        {
            if (serverId != _serverId)
            {
                await SendMessageToServerAsync(serverId, message);
            }
        }
    }

    public async Task SendHeartbeatAsync(ServerInfo currentServerInfo)
    {
        var message = new ServerMessage
        {
            Type = "HEARTBEAT",
            SenderId = _serverId,
            Timestamp = DateTime.Now,
            Data = currentServerInfo
        };

        await BroadcastMessageAsync(message);
    }

    public async Task SendPingAsync(string targetServerId)
    {
        var message = new ServerMessage
        {
            Type = "PING",
            SenderId = _serverId,
            TargetId = targetServerId,
            Timestamp = DateTime.Now
        };

        await SendMessageToServerAsync(targetServerId, message);
    }

    public async Task RequestPlayerMatchAsync(string targetServerId, PlayerInfo playerInfo)
    {
        var message = new ServerMessage
        {
            Type = "PLAYER_MATCH_REQUEST",
            SenderId = _serverId,
            TargetId = targetServerId,
            Timestamp = DateTime.Now,
            Data = playerInfo
        };

        await SendMessageToServerAsync(targetServerId, message);
    }

    private async Task SendPongAsync(string targetServerId)
    {
        var message = new ServerMessage
        {
            Type = "PONG",
            SenderId = _serverId,
            TargetId = targetServerId,
            Timestamp = DateTime.Now
        };

        await SendMessageToServerAsync(targetServerId, message);
    }

    private async Task BroadcastMessageAsync(ServerMessage message)
    {
        var messageJson = JsonSerializer.Serialize(message) + "\n";
        var data = Encoding.UTF8.GetBytes(messageJson);

        var tasks = new List<Task>();
        foreach (var connection in _connections.Values)
        {
            tasks.Add(SendDataAsync(connection, data));
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    private async Task SendMessageToServerAsync(string serverId, ServerMessage message)
    {
        if (_connections.TryGetValue(serverId, out var client))
        {
            var messageJson = JsonSerializer.Serialize(message) + "\n";
            var data = Encoding.UTF8.GetBytes(messageJson);
            await SendDataAsync(client, data);
        }
    }

    private async Task SendDataAsync(TcpClient client, byte[] data)
    {
        try
        {
            if (client.Connected)
            {
                var stream = client.GetStream();
                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_serverId}] Error sending data: {ex.Message}");
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _serverListener?.Stop();
        
        foreach (var connection in _connections.Values)
        {
            connection.Close();
        }
        _connections.Clear();
    }
}