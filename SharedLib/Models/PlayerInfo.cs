namespace SharedLib.Models;

public class PlayerInfo
{
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public int PlayerLevel { get; set; }
    public int PlayerElo { get; set; }
    public string? AvatarUrl { get; set; }
}