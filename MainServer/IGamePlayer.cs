using SharedLib.Models;

namespace MainServer;

public interface IGamePlayer
{
    string ClientId { get; }
    PlayerInfo? PlayerInfo { get; }
    GameRoom? CurrentRoom { get; set; }
    string? PlayerSymbol { get; set; }
    bool IsConnected { get; }
    Task SendMessage(string message);
}