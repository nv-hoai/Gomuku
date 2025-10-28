using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLib.Database.Models
{
    public class PlayerProfile
    {
        [Key]
        public int ProfileId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string PlayerName { get; set; } = string.Empty;

        public int Elo { get; set; } = 1000;

        public int TotalGames { get; set; } = 0;

        public int Wins { get; set; } = 0;

        public int Losses { get; set; } = 0;

        public int Draws { get; set; } = 0;

        public int Level { get; set; } = 1;

        [MaxLength(500)]
        public string? Bio { get; set; }

        [MaxLength(255)]
        public string? AvatarUrl { get; set; }

        [NotMapped]
        public double WinRate => TotalGames > 0 ? (double)Wins / TotalGames * 100 : 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastGameAt { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        public virtual ICollection<GameHistory> GamesAsPlayer1 { get; set; } = new List<GameHistory>();
        public virtual ICollection<GameHistory> GamesAsPlayer2 { get; set; } = new List<GameHistory>();
        public virtual ICollection<Friendship> FriendshipsInitiated { get; set; } = new List<Friendship>();
        public virtual ICollection<Friendship> FriendshipsReceived { get; set; } = new List<Friendship>();
    }
}
