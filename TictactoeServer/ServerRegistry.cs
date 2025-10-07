using System.Collections.Concurrent;
using System.Text.Json;

namespace TicTacToeServer;

public class ServerInfo
{
    public string ServerId { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int ClientPort { get; set; }
    public int ServerPort { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public int ActivePlayers { get; set; }
    public int ActiveGames { get; set; }
    public ServerStatus Status { get; set; }
    public double CpuUsage { get; set; }
    public long MemoryUsage { get; set; }
}

public enum ServerStatus
{
    Online,
    Busy,
    Offline,
    Starting
}

public class ServerRegistry
{
    private readonly ConcurrentDictionary<string, ServerInfo> _servers = new();
    private readonly string _currentServerId;
    private readonly Timer _cleanupTimer;

    public event Action<ServerInfo>? ServerAdded;
    public event Action<ServerInfo>? ServerRemoved;
    public event Action<ServerInfo>? ServerUpdated;

    public ServerRegistry(string serverId)
    {
        _currentServerId = serverId;
        _cleanupTimer = new Timer(CleanupOfflineServers, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public void RegisterServer(ServerInfo server)
    {
        var isNew = !_servers.ContainsKey(server.ServerId);
        _servers[server.ServerId] = server;
        
        Console.WriteLine($"[{_currentServerId}] Server {(isNew ? "registered" : "updated")}: {server.ServerId} at {server.Host}:{server.ClientPort}");
        
        if (isNew)
            ServerAdded?.Invoke(server);
        else
            ServerUpdated?.Invoke(server);
    }

    public void UnregisterServer(string serverId)
    {
        if (_servers.TryRemove(serverId, out var server))
        {
            Console.WriteLine($"[{_currentServerId}] Server unregistered: {serverId}");
            ServerRemoved?.Invoke(server);
        }
    }

    public List<ServerInfo> GetAvailableServers()
    {
        return _servers.Values
            .Where(s => s.Status == ServerStatus.Online && 
                       (DateTime.Now - s.LastHeartbeat).TotalSeconds < 30)
            .OrderBy(s => s.ActivePlayers)
            .ToList();
    }

    public ServerInfo? GetServer(string serverId)
    {
        _servers.TryGetValue(serverId, out var server);
        return server;
    }

    public ServerInfo? GetBestServer()
    {
        return GetAvailableServers()
            .Where(s => s.ServerId != _currentServerId)
            .FirstOrDefault();
    }

    public void UpdateHeartbeat(string serverId, ServerInfo? metrics = null)
    {
        if (_servers.TryGetValue(serverId, out var server))
        {
            server.LastHeartbeat = DateTime.Now;
            server.Status = ServerStatus.Online;
            
            if (metrics != null)
            {
                server.ActivePlayers = metrics.ActivePlayers;
                server.ActiveGames = metrics.ActiveGames;
                server.CpuUsage = metrics.CpuUsage;
                server.MemoryUsage = metrics.MemoryUsage;
            }
        }
    }

    public List<ServerInfo> GetAllServers()
    {
        return _servers.Values.ToList();
    }

    public int GetTotalServers()
    {
        return _servers.Count;
    }

    private void CleanupOfflineServers(object? state)
    {
        var cutoffTime = DateTime.Now.AddSeconds(-30);
        var offlineServers = _servers.Values
            .Where(s => s.LastHeartbeat < cutoffTime && s.Status != ServerStatus.Offline)
            .ToList();

        foreach (var server in offlineServers)
        {
            server.Status = ServerStatus.Offline;
            Console.WriteLine($"[{_currentServerId}] Server marked offline: {server.ServerId}");
            ServerUpdated?.Invoke(server);
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }
}