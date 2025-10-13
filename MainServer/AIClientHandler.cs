using System.Net.Sockets;
using SharedLib.Models;

namespace MainServer;

public class AIClientHandler : IGamePlayer
{
    public string ClientId { get; set; }
    public PlayerInfo? PlayerInfo { get; set; }
    public GameRoom? CurrentRoom { get; set; }
    public string? PlayerSymbol { get; set; }
    public bool IsConnected { get; set; } = true;

    public AIClientHandler(string aiSymbol)
    {
        ClientId = "AI-" + Guid.NewGuid().ToString();
        PlayerSymbol = aiSymbol;
        PlayerInfo = new PlayerInfo 
        { 
            PlayerName = "AI Player",
            PlayerId = ClientId 
        };
    }

    public async Task SendMessage(string message)
    {
        // AI doesn't need to receive messages, just log them
        Console.WriteLine($"AI would receive: {message}");
        await Task.CompletedTask;
    }

    public async Task Disconnect()
    {
        // AI doesn't need cleanup
        await Task.CompletedTask;
    }
}