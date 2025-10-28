using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLib.Database.Models
{
    public class GameHistory
    {
        [Key]
        public int GameId { get; set; }

        [Required]
        public int Player1Id { get; set; }

        public int? Player2Id { get; set; } // Nullable for AI games

        public bool IsAIGame { get; set; } = false;

        public int? WinnerId { get; set; }

        [Required]
        [MaxLength(20)]
        public string GameResult { get; set; } = string.Empty; // "Player1Win", "Player2Win", "Draw"

        public int TotalMoves { get; set; }

        public int GameDurationSeconds { get; set; }

        [NotMapped]
        public TimeSpan GameDuration
        {
            get => TimeSpan.FromSeconds(GameDurationSeconds);
            set => GameDurationSeconds = (int)value.TotalSeconds;
        }

        public DateTime PlayedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(50)]
        public string? GameMode { get; set; } // "Ranked", "Casual", "AI"

        public int Player1EloChange { get; set; } = 0;

        public int Player2EloChange { get; set; } = 0;

        // Navigation properties
        [ForeignKey("Player1Id")]
        public virtual PlayerProfile Player1 { get; set; } = null!;

        [ForeignKey("Player2Id")]
        public virtual PlayerProfile Player2 { get; set; } = null!;

        [ForeignKey("WinnerId")]
        public virtual PlayerProfile? Winner { get; set; }
    }
}
