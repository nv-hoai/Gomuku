using Microsoft.EntityFrameworkCore;
using SharedLib.Database.Models;

namespace SharedLib.Database;

public class GomokuDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<PlayerProfile> PlayerProfiles { get; set; }
    public DbSet<GameHistory> GameHistories { get; set; }

    public GomokuDbContext(DbContextOptions<GomokuDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User Configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Salt).IsRequired().HasMaxLength(255);
        });

        // PlayerProfile Configuration
        modelBuilder.Entity<PlayerProfile>(entity =>
        {
            entity.HasKey(e => e.ProfileId);
            entity.HasIndex(e => e.UserId).IsUnique();
            
            entity.HasOne(e => e.User)
                .WithOne(e => e.PlayerProfile)
                .HasForeignKey<PlayerProfile>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Avatar).HasMaxLength(255);
            entity.Property(e => e.PreferredSymbol).HasMaxLength(1);
            entity.Property(e => e.Bio).HasMaxLength(500);
        });

        // GameHistory Configuration
        modelBuilder.Entity<GameHistory>(entity =>
        {
            entity.HasKey(e => e.GameId);
            entity.HasIndex(e => e.RoomId);
            entity.HasIndex(e => e.StartTime);
            
            entity.Property(e => e.RoomId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Winner).HasConversion<string>();
            entity.Property(e => e.GameStatus).HasConversion<string>();
            entity.Property(e => e.Player1Symbol).HasMaxLength(1);
            entity.Property(e => e.Player2Symbol).HasMaxLength(1);

            // Player1 relationship
            entity.HasOne(e => e.Player1)
                .WithMany(e => e.GamesAsPlayer1)
                .HasForeignKey(e => e.Player1Id)
                .OnDelete(DeleteBehavior.Restrict);

            // Player2 relationship (nullable for AI games)
            entity.HasOne(e => e.Player2)
                .WithMany(e => e.GamesAsPlayer2)
                .HasForeignKey(e => e.Player2Id)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Seed Data
        SeedDefaultData(modelBuilder);
    }

    private void SeedDefaultData(ModelBuilder modelBuilder)
    {
        // Create default admin user
        var adminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var salt = Guid.NewGuid().ToString();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("admin123" + salt);

        var adminUser = new User
        {
            UserId = adminUserId,
            Username = "admin",
            PasswordHash = passwordHash,
            Salt = salt,
            CreatedAt = DateTime.UtcNow
        };

        var adminProfile = new PlayerProfile
        {
            ProfileId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            UserId = adminUserId,
            DisplayName = "Administrator",
            PlayerLevel = 99,
            EloRating = 2000,
            Bio = "System Administrator",
            UpdatedAt = DateTime.UtcNow
        };

        modelBuilder.Entity<User>().HasData(adminUser);
        modelBuilder.Entity<PlayerProfile>().HasData(adminProfile);
    }
}