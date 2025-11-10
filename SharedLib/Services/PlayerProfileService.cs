using Microsoft.EntityFrameworkCore;
using SharedLib.Database;
using SharedLib.Database.Models;

namespace SharedLib.Services
{
    public class PlayerProfileService
    {
        private readonly GomokuDbContext _context;

        public PlayerProfileService(GomokuDbContext context)
        {
            _context = context;
        }

        public async Task<PlayerProfile?> GetProfileByIdAsync(int profileId)
        {
            return await _context.PlayerProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.ProfileId == profileId);
        }

        public async Task<PlayerProfile?> GetProfileByUserIdAsync(int userId)
        {
            return await _context.PlayerProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId);
        }

        public async Task<PlayerProfile?> GetProfileByPlayerNameAsync(string playerName)
        {
            return await _context.PlayerProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.PlayerName == playerName);
        }

        public async Task<PlayerProfile> CreateProfileAsync(int userId, string playerName, int initialElo = 1000)
        {
            var profile = new PlayerProfile
            {
                UserId = userId,
                PlayerName = playerName,
                Elo = initialElo,
                CreatedAt = DateTime.UtcNow
            };

            _context.PlayerProfiles.Add(profile);
            await _context.SaveChangesAsync();
            return profile;
        }

        public async Task<bool> UpdatePlayerNameAsync(int profileId, string newPlayerName)
        {
            var profile = await _context.PlayerProfiles.FindAsync(profileId);
            if (profile == null) return false;

            profile.PlayerName = newPlayerName;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateEloAsync(int profileId, int newElo)
        {
            var profile = await _context.PlayerProfiles.FindAsync(profileId);
            if (profile == null) return false;

            profile.Elo = newElo;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateAvatarUrlAsync(int profileId, string newAvatarUrl)
        {
            var profile = await _context.PlayerProfiles.FindAsync(profileId);
            if (profile == null) return false;

            profile.AvatarUrl = newAvatarUrl;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateBioAsync(int profileId, string newBio)
        {
            var profile = await _context.PlayerProfiles.FindAsync(profileId);
            if (profile == null) return false;

            profile.Bio = newBio;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateStatusAsync(int profileId, bool isOnline)
        {
            var profile = await _context.PlayerProfiles.FindAsync(profileId);
            if (profile == null) return false;

            profile.IsOnline = isOnline;
            await _context.SaveChangesAsync();
            return true;
        }
        
        public async Task<bool> UpdateGameStatsAsync(int profileId, bool won, bool draw)
        {
            var profile = await _context.PlayerProfiles.FindAsync(profileId);
            if (profile == null) return false;

            profile.TotalGames++;
            if (won) profile.Wins++;
            else if (draw) profile.Draws++;
            else profile.Losses++;
            profile.LastGameAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<PlayerProfile>> GetTopPlayersByEloAsync(int count = 10)
        {
            return await _context.PlayerProfiles
                .Include(p => p.User)
                .OrderByDescending(p => p.Elo)
                .Take(count)
                .ToListAsync();
        }


        public async Task<List<PlayerProfile>> SearchPlayersByNameAsync(string searchTerm, int maxResults = 20)
        {
            return await _context.PlayerProfiles
                .Where(p => p.PlayerName.Contains(searchTerm))
                .Take(maxResults)
                .ToListAsync();
        }

        public async Task<bool> IsPlayerNameAvailableAsync(string playerName)
        {
            return !await _context.PlayerProfiles.AnyAsync(p => p.PlayerName == playerName);
        }

        public async Task<int> GetPlayerRankAsync(int profileId)
        {
            var profile = await _context.PlayerProfiles.FindAsync(profileId);
            if (profile == null) return -1;

            return await _context.PlayerProfiles
                .Where(p => p.Elo > profile.Elo)
                .CountAsync() + 1;
        }

        public async Task<PlayerProfile?> GetProfileByNameAsync(string playerName)
        {
            return await GetProfileByPlayerNameAsync(playerName);
        }
    }
}

