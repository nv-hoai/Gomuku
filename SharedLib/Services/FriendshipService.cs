using Microsoft.EntityFrameworkCore;
using SharedLib.Database;
using SharedLib.Database.Models;

namespace SharedLib.Services
{
    public class FriendshipService
    {
        private readonly GomokuDbContext _context;

        public FriendshipService(GomokuDbContext context)
        {
            _context = context;
        }

        public async Task<Friendship?> GetFriendshipByIdAsync(int friendshipId)
        {
            return await _context.Friendships
                .Include(f => f.User)
                .Include(f => f.Friend)
                .FirstOrDefaultAsync(f => f.FriendshipId == friendshipId);
        }

        public async Task<Friendship?> SendFriendRequestAsync(int userId, int friendId)
        {
            // Check if friendship already exists
            var existing = await _context.Friendships
                .FirstOrDefaultAsync(f =>
                    (f.UserId == userId && f.FriendId == friendId) ||
                    (f.UserId == friendId && f.FriendId == userId));

            if (existing != null) return null;

            var friendship = new Friendship
            {
                UserId = userId,
                FriendId = friendId,
                Status = "Pending",
                RequestedAt = DateTime.UtcNow
            };

            _context.Friendships.Add(friendship);
            await _context.SaveChangesAsync();
            return friendship;
        }

        public async Task<bool> AcceptFriendRequestAsync(int friendshipId)
        {
            var friendship = await _context.Friendships.FindAsync(friendshipId);
            if (friendship == null || friendship.Status != "Pending") return false;

            friendship.Status = "Accepted";
            friendship.AcceptedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectFriendRequestAsync(int friendshipId)
        {
            var friendship = await _context.Friendships.FindAsync(friendshipId);
            if (friendship == null) return false;

            _context.Friendships.Remove(friendship);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> BlockUserAsync(int userId, int friendId)
        {
            var friendship = await _context.Friendships
                .FirstOrDefaultAsync(f =>
                    (f.UserId == userId && f.FriendId == friendId) ||
                    (f.UserId == friendId && f.FriendId == userId));

            if (friendship == null)
            {
                friendship = new Friendship
                {
                    UserId = userId,
                    FriendId = friendId,
                    Status = "Blocked",
                    RequestedAt = DateTime.UtcNow
                };
                _context.Friendships.Add(friendship);
            }
            else
            {
                friendship.Status = "Blocked";
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UnfriendAsync(int userId, int friendId)
        {
            var friendship = await _context.Friendships
                .FirstOrDefaultAsync(f =>
                    (f.UserId == userId && f.FriendId == friendId) ||
                    (f.UserId == friendId && f.FriendId == userId));

            if (friendship == null) return false;

            _context.Friendships.Remove(friendship);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<PlayerProfile>> GetFriendsAsync(int profileId)
        {
            var friendships = await _context.Friendships
                .Where(f =>
                    (f.User.UserId == profileId || f.Friend.UserId == profileId) &&
                    f.Status == "Accepted")
                .Include(f => f.User)
                    .ThenInclude(u => u.User)
                .Include(f => f.Friend)
                    .ThenInclude(u => u.User)
                .ToListAsync();

            var friends = new List<PlayerProfile>();
            foreach (var friendship in friendships)
            {
                if (friendship.User.UserId == profileId)
                    friends.Add(friendship.Friend);
                else
                    friends.Add(friendship.User);
            }

            return friends;
        }

        public async Task<List<Friendship>> GetPendingRequestsAsync(int profileId)
        {
            return await _context.Friendships
                .Include(f => f.User)
                    .ThenInclude(u => u.User)
                .Include(f => f.Friend)
                    .ThenInclude(u => u.User)
                .Where(f => f.Friend.UserId == profileId && f.Status == "Pending")
                .ToListAsync();
        }

        public async Task<List<Friendship>> GetSentRequestsAsync(int profileId)
        {
            return await _context.Friendships
                .Include(f => f.User)
                    .ThenInclude(u => u.User)
                .Include(f => f.Friend)
                    .ThenInclude(u => u.User)
                .Where(f => f.User.UserId == profileId && f.Status == "Pending")
                .ToListAsync();
        }

        public async Task<bool> AreFriendsAsync(int profileId1, int profileId2)
        {
            return await _context.Friendships
                .AnyAsync(f =>
                    ((f.User.UserId == profileId1 && f.Friend.UserId == profileId2) ||
                     (f.User.UserId == profileId2 && f.Friend.UserId == profileId1)) &&
                    f.Status == "Accepted");
        }

        public async Task<int> GetFriendCountAsync(int profileId)
        {
            return await _context.Friendships
                .Where(f =>
                    (f.User.UserId == profileId || f.Friend.UserId == profileId) &&
                    f.Status == "Accepted")
                .CountAsync();
        }
    }
}

