namespace TicTacToeServer;

public class DistributedGameRoom : GameRoom
{
    public string HostServerId { get; set; } = string.Empty;
    public List<string> ObserverServerIds { get; set; } = new();
    public DateTime LastSync { get; set; } = DateTime.Now;
    public bool IsDistributed { get; set; } = false;

    public DistributedGameRoom(string roomId, string hostServerId) : base(roomId)
    {
        HostServerId = hostServerId;
        IsDistributed = true;
    }

    public void AddObserverServer(string serverId)
    {
        if (!ObserverServerIds.Contains(serverId) && serverId != HostServerId)
        {
            ObserverServerIds.Add(serverId);
        }
    }

    public void RemoveObserverServer(string serverId)
    {
        ObserverServerIds.Remove(serverId);
    }

    public List<string> GetAllServerIds()
    {
        var serverIds = new List<string> { HostServerId };
        serverIds.AddRange(ObserverServerIds);
        return serverIds;
    }

    public bool IsHostedBy(string serverId)
    {
        return HostServerId == serverId;
    }

    public void UpdateSyncTime()
    {
        LastSync = DateTime.Now;
    }
}