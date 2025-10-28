using Microsoft.EntityFrameworkCore;
using SharedLib.Database;
using SharedLib.Database.Models;

namespace SharedLib.Services
{
    public class GameHistoryService
    {
        private readonly GomokuDbContext _context;

        public GameHistoryService(GomokuDbContext context)
        {
            _context = context;
        }

        public async Task<GameHistory?> GetGameByIdAsync(int gameId)
        {
            return await _context.GameHistories
                .Include(g => g.Player1)
                .Include(g => g.Player2)
                .Include(g => g.Winner)
                .FirstOrDefaultAsync(g => g.GameId == gameId);
        }

        public async Task<GameHistory> RecordGameAsync(
            int player1Id,
            int player2Id,
            int? winnerId,
            string gameResult,
            int totalMoves,
            int gameDurationSeconds,
            string? gameMode = "Ranked",
            int player1EloChange = 0,
            int player2EloChange = 0)
        {
            var game = new GameHistory
            {
                Player1Id = player1Id,
                Player2Id = player2Id,
                WinnerId = winnerId,
                GameResult = gameResult,
                TotalMoves = totalMoves,
                GameDurationSeconds = gameDurationSeconds,
                PlayedAt = DateTime.UtcNow,
                GameMode = gameMode,
                Player1EloChange = player1EloChange,
                Player2EloChange = player2EloChange,
                IsAIGame = false
            };

            _context.GameHistories.Add(game);
            await _context.SaveChangesAsync();
            return game;
        }

        public async Task<GameHistory> RecordAIGameAsync(
            int player1Id,
            int? winnerId,
            string gameResult,
            int totalMoves,
            int gameDurationSeconds,
            string? gameMode = "AI")
        {
            var game = new GameHistory
            {
                Player1Id = player1Id,
                Player2Id = null, // No opponent for AI games
                WinnerId = winnerId,
                GameResult = gameResult,
                TotalMoves = totalMoves,
                GameDurationSeconds = gameDurationSeconds,
                PlayedAt = DateTime.UtcNow,
                GameMode = gameMode,
                Player1EloChange = 0, // AI games don't change ELO (or small change if configured)
                Player2EloChange = 0,
                IsAIGame = true
            };

            _context.GameHistories.Add(game);
            await _context.SaveChangesAsync();
            return game;
        }

        public async Task<List<GameHistory>> GetPlayerGameHistoryAsync(int profileId, int count = 20)
        {
            return await _context.GameHistories
                .Include(g => g.Player1)
                .Include(g => g.Player2)
                .Include(g => g.Winner)
                .Where(g => g.Player1Id == profileId || g.Player2Id == profileId)
                .OrderByDescending(g => g.PlayedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<GameHistory>> GetRecentGamesAsync(int count = 50)
        {
            return await _context.GameHistories
                .Include(g => g.Player1)
                .Include(g => g.Player2)
                .Include(g => g.Winner)
                .OrderByDescending(g => g.PlayedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<Dictionary<string, int>> GetPlayerStatsAsync(int profileId)
        {
            var games = await _context.GameHistories
                .Where(g => g.Player1Id == profileId || g.Player2Id == profileId)
                .ToListAsync();

            var stats = new Dictionary<string, int>
            {
                ["TotalGames"] = games.Count,
                ["Wins"] = games.Count(g => g.WinnerId == profileId),
                ["Losses"] = games.Count(g => g.WinnerId != null && g.WinnerId != profileId),
                ["Draws"] = games.Count(g => g.WinnerId == null),
                ["TotalMoves"] = games.Sum(g => g.TotalMoves),
                ["TotalPlayTime"] = games.Sum(g => g.GameDurationSeconds)
            };

            return stats;
        }

        public async Task<List<GameHistory>> GetHeadToHeadHistoryAsync(int profileId1, int profileId2)
        {
            return await _context.GameHistories
                .Include(g => g.Player1)
                .Include(g => g.Player2)
                .Include(g => g.Winner)
                .Where(g =>
                    (g.Player1Id == profileId1 && g.Player2Id == profileId2) ||
                    (g.Player1Id == profileId2 && g.Player2Id == profileId1))
                .OrderByDescending(g => g.PlayedAt)
                .ToListAsync();
        }

        public async Task<List<GameHistory>> GetGamesByModeAsync(string gameMode, int count = 50)
        {
            return await _context.GameHistories
                .Include(g => g.Player1)
                .Include(g => g.Player2)
                .Include(g => g.Winner)
                .Where(g => g.GameMode == gameMode)
                .OrderByDescending(g => g.PlayedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<GameHistory>> GetPlayerGamesAsync(int profileId, int pageSize = 20, int page = 1)
        {
            return await GetPlayerGameHistoryAsync(profileId, pageSize);
        }
    }
}

