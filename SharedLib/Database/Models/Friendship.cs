using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLib.Database.Models
{
    public class Friendship
    {
        [Key]
        public int FriendshipId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int FriendId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // "Pending", "Accepted", "Blocked"

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        public DateTime? AcceptedAt { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual PlayerProfile User { get; set; } = null!;

        [ForeignKey("FriendId")]
        public virtual PlayerProfile Friend { get; set; } = null!;
    }
}
