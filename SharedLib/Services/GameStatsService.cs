using Microsoft.EntityFrameworkCore;
using SharedLib.Database;
using SharedLib.Database.Models;

namespace SharedLib.Services;

public interface IGameStatsService
{
    Task<bool> RecordGameAsync(Guid player1Id, Guid? player2Id, string roomId, GameWinner winner, bool isAIGame, int duration);
    Task<PlayerProfile?> GetPlayerStatsAsync(Guid userId);
    Task<bool> UpdatePlayerStatsAsync(Guid userId, bool won, bool draw, int eloChange = 0);
    Task<List<PlayerProfile>> GetLeaderboardAsync(int limit = 50);
    Task<List<GameHistory>> GetPlayerGameHistoryAsync(Guid userId, int limit = 20);
    Task<(int wins, int losses, int draws, double winRate)> GetPlayerRecordAsync(Guid userId);
}

public class GameStatsService : IGameStatsService
{
    private readonly GomokuDbContext _context;

    public GameStatsService(GomokuDbContext context)
    {
        _context = context;
    }

    public async Task<bool> RecordGameAsync(Guid player1Id, Guid? player2Id, string roomId, GameWinner winner, bool isAIGame, int duration)
    {
        try
        {
            var gameHistory = new GameHistory
            {
                RoomId = roomId,
                Player1Id = player1Id,
                Player2Id = player2Id,
                IsAIGame = isAIGame,
                Winner = winner,
                Duration = duration,
                StartTime = DateTime.UtcNow.AddSeconds(-duration),
                EndTime = DateTime.UtcNow,
                GameStatus = GameStatus.Completed
            };

            _context.GameHistories.Add(gameHistory);

            // Update player stats
            switch (winner)
            {
                case GameWinner.Player1:
                    await UpdatePlayerStatsAsync(player1Id, won: true, draw: false);
                    if (player2Id.HasValue)
                        await UpdatePlayerStatsAsync(player2Id.Value, won: false, draw: false);
                    break;

                case GameWinner.Player2:
                    await UpdatePlayerStatsAsync(player1Id, won: false, draw: false);
                    if (player2Id.HasValue)
                        await UpdatePlayerStatsAsync(player2Id.Value, won: true, draw: false);
                    break;

                case GameWinner.Draw:
                    await UpdatePlayerStatsAsync(player1Id, won: false, draw: true);
                    if (player2Id.HasValue)
                        await UpdatePlayerStatsAsync(player2Id.Value, won: false, draw: true);
                    break;
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error recording game: {ex.Message}");
            return false;
        }
    }

    public async Task<PlayerProfile?> GetPlayerStatsAsync(Guid userId)
    {
        return await _context.PlayerProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);
    }

    public async Task<bool> UpdatePlayerStatsAsync(Guid userId, bool won, bool draw, int eloChange = 0)
    {
        try
        {
            var profile = await _context.PlayerProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null) return false;

            profile.TotalGamesPlayed++;

            if (won)
            {
                profile.Wins++;
                profile.EloRating += Math.Max(eloChange, 25); // Minimum +25 for wins
            }
            else if (draw)
            {
                profile.Draws++;
                profile.EloRating += Math.Max(eloChange, 5); // Small boost for draws
            }
            else
            {
                profile.Losses++;
                profile.EloRating += Math.Min(eloChange, -15); // Maximum -15 for losses
            }

            // Keep ELO within reasonable bounds
            profile.EloRating = Math.Max(800, Math.Min(3000, profile.EloRating));

            // Calculate level based on total games and win rate
            var winRate = profile.TotalGamesPlayed > 0 ? (double)profile.Wins / profile.TotalGamesPlayed : 0;
            profile.PlayerLevel = CalculatePlayerLevel(profile.TotalGamesPlayed, winRate);

            profile.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating player stats: {ex.Message}");
            return false;
        }
    }

    public async Task<List<PlayerProfile>> GetLeaderboardAsync(int limit = 50)
    {
        return await _context.PlayerProfiles
            .Include(p => p.User)
            .Where(p => p.TotalGamesPlayed >= 5) // Minimum games for ranking
            .OrderByDescending(p => p.EloRating)
            .ThenByDescending(p => p.Wins)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<GameHistory>> GetPlayerGameHistoryAsync(Guid userId, int limit = 20)
    {
        return await _context.GameHistories
            .Include(g => g.Player1).ThenInclude(p => p.PlayerProfile)
            .Include(g => g.Player2).ThenInclude(p => p!.PlayerProfile)
            .Where(g => g.Player1Id == userId || g.Player2Id == userId)
            .OrderByDescending(g => g.EndTime)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<(int wins, int losses, int draws, double winRate)> GetPlayerRecordAsync(Guid userId)
    {
        var profile = await GetPlayerStatsAsync(userId);
        if (profile == null)
            return (0, 0, 0, 0.0);

        var winRate = profile.TotalGamesPlayed > 0 
            ? (double)profile.Wins / profile.TotalGamesPlayed 
            : 0.0;

        return (profile.Wins, profile.Losses, profile.Draws, winRate);
    }

    private int CalculatePlayerLevel(int totalGames, double winRate)
    {
        // Simple level calculation based on games played and performance
        var baseLevel = Math.Min(totalGames / 10, 50); // 1 level per 10 games, max 50
        var bonusLevel = (int)(winRate * 20); // Up to 20 bonus levels for high win rate
        
        return Math.Max(1, Math.Min(99, baseLevel + bonusLevel));
    }
}