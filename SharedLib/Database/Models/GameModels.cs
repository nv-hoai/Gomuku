using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLib.Database.Models;

public class User
{
    [Key]
    public Guid UserId { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string Salt { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastLoginAt { get; set; }

    // Navigation property
    public PlayerProfile? PlayerProfile { get; set; }
    
    // Game relationships
    public List<GameHistory> GamesAsPlayer1 { get; set; } = new();
    public List<GameHistory> GamesAsPlayer2 { get; set; } = new();
}

public class PlayerProfile
{
    [Key]
    public Guid ProfileId { get; set; } = Guid.NewGuid();
    
    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string DisplayName { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string? Avatar { get; set; }
    
    public int PlayerLevel { get; set; } = 1;
    
    public int EloRating { get; set; } = 1200;
    
    public int TotalGamesPlayed { get; set; } = 0;
    
    public int Wins { get; set; } = 0;
    
    public int Losses { get; set; } = 0;
    
    public int Draws { get; set; } = 0;
    
    public char? PreferredSymbol { get; set; } // X or O
    
    [MaxLength(500)]
    public string? Bio { get; set; }
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public User User { get; set; } = null!;
}

public class GameHistory
{
    [Key]
    public Guid GameId { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(50)]
    public string RoomId { get; set; } = string.Empty;
    
    [ForeignKey(nameof(Player1))]
    public Guid Player1Id { get; set; }
    
    [ForeignKey(nameof(Player2))]
    public Guid? Player2Id { get; set; } // Nullable for AI games
    
    public bool IsAIGame { get; set; } = false;
    
    public GameWinner Winner { get; set; }
    
    public char Player1Symbol { get; set; } = 'X';
    
    public char? Player2Symbol { get; set; } = 'O';
    
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    
    public DateTime? EndTime { get; set; }
    
    public int Duration { get; set; } // seconds
    
    public GameStatus GameStatus { get; set; } = GameStatus.Completed;

    // Navigation properties
    public User Player1 { get; set; } = null!;
    public User? Player2 { get; set; }
}

public enum GameWinner
{
    Player1,
    Player2,
    Draw
}

public enum GameStatus
{
    Completed,
    Abandoned,
    Disconnected
}