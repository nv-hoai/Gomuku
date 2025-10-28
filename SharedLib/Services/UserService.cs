using Microsoft.EntityFrameworkCore;
using SharedLib.Database;
using SharedLib.Database.Models;

namespace SharedLib.Services
{
    public class UserService
    {
        private readonly GomokuDbContext _context;

        public UserService(GomokuDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _context.Users
                .Include(u => u.PlayerProfile)
                .FirstOrDefaultAsync(u => u.UserId == userId);
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            return await _context.Users
                .Include(u => u.PlayerProfile)
                .FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.Users
                .Include(u => u.PlayerProfile)
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User> CreateUserAsync(string username, string passwordHash, string email)
        {
            var user = new User
            {
                Username = username,
                PasswordHash = passwordHash,
                Email = email,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<bool> UpdateLastLoginAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            user.LastLogin = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdatePasswordAsync(int userId, string newPasswordHash)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            user.PasswordHash = newPasswordHash;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateUserAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            user.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsUsernameAvailableAsync(string username)
        {
            return !await _context.Users.AnyAsync(u => u.Username == username);
        }

        public async Task<bool> IsEmailAvailableAsync(string email)
        {
            return !await _context.Users.AnyAsync(u => u.Email == email);
        }

        // Authentication methods
        public async Task<(bool Success, string Message, User? User, PlayerProfile? Profile)> LoginAsync(string username, string password)
        {
            var user = await GetUserByUsernameAsync(username);
            if (user == null)
            {
                return (false, "Invalid username or password", null, null);
            }

            if (!user.IsActive)
            {
                return (false, "Account is inactive", null, null);
            }

            // Simple password verification (you should use proper password hashing)
            if (user.PasswordHash != HashPassword(password))
            {
                return (false, "Invalid username or password", null, null);
            }

            // Update last login
            await UpdateLastLoginAsync(user.UserId);

            return (true, "Login successful", user, user.PlayerProfile);
        }

        public async Task<(bool Success, string Message, User? User)> RegisterAsync(string username, string password, string email, string playerName)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return (false, "Username and password are required", null);
            }

            // Check if username exists
            if (!await IsUsernameAvailableAsync(username))
            {
                return (false, "Username already exists", null);
            }

            // Check if email exists
            if (!await IsEmailAvailableAsync(email))
            {
                return (false, "Email already exists", null);
            }

            // Create user
            var user = await CreateUserAsync(username, HashPassword(password), email);

            // Create player profile (using ProfileService would be better, but for now we'll do it here)
            var profile = new PlayerProfile
            {
                UserId = user.UserId,
                PlayerName = playerName,
                Elo = 1000,
                CreatedAt = DateTime.UtcNow,
                AvatarUrl = "cat"
            };

            _context.PlayerProfiles.Add(profile);
            await _context.SaveChangesAsync();

            return (true, "Registration successful", user);
        }

        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}

