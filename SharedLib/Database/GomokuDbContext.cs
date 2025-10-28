using Microsoft.EntityFrameworkCore;
using SharedLib.Database.Models;

namespace SharedLib.Database
{
    public class GomokuDbContext : DbContext
    {
        public GomokuDbContext(DbContextOptions<GomokuDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<PlayerProfile> PlayerProfiles { get; set; }
        public DbSet<GameHistory> GameHistories { get; set; }
        public DbSet<Friendship> Friendships { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
            });

            // PlayerProfile configuration
            modelBuilder.Entity<PlayerProfile>(entity =>
            {
                entity.HasIndex(e => e.UserId).IsUnique();
                entity.HasIndex(e => e.PlayerName);
                entity.HasIndex(e => e.Elo);

                entity.HasOne(p => p.User)
                    .WithOne(u => u.PlayerProfile)
                    .HasForeignKey<PlayerProfile>(p => p.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // GameHistory configuration
            modelBuilder.Entity<GameHistory>(entity =>
            {
                entity.HasIndex(e => e.PlayedAt);
                entity.HasIndex(e => e.Player1Id);
                entity.HasIndex(e => e.Player2Id);

                entity.HasOne(g => g.Player1)
                    .WithMany(p => p.GamesAsPlayer1)
                    .HasForeignKey(g => g.Player1Id)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(g => g.Player2)
                    .WithMany(p => p.GamesAsPlayer2)
                    .HasForeignKey(g => g.Player2Id)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false); // Player2 is optional for AI games

                entity.HasOne(g => g.Winner)
                    .WithMany()
                    .HasForeignKey(g => g.WinnerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Friendship configuration
            modelBuilder.Entity<Friendship>(entity =>
            {
                entity.HasIndex(e => new { e.UserId, e.FriendId }).IsUnique();
                entity.HasIndex(e => e.Status);

                entity.HasOne(f => f.User)
                    .WithMany(p => p.FriendshipsInitiated)
                    .HasForeignKey(f => f.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(f => f.Friend)
                    .WithMany(p => p.FriendshipsReceived)
                    .HasForeignKey(f => f.FriendId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Prevent self-friendship
                entity.HasCheckConstraint("CK_Friendship_NoSelfFriend", "[UserId] <> [FriendId]");
            });
        }
    }
}
