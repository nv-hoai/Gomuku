using Microsoft.EntityFrameworkCore;
using SharedLib.Database.Models;
using SharedLib.Database;

namespace SharedLib.Services;

public interface IAuthenticationService
{
    Task<(bool Success, User? User, string Message)> RegisterUserAsync(string username, string password, string? displayName = null);
    Task<(bool Success, User? User, string Message)> LoginUserAsync(string username, string password);
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<User?> GetUserByUsernameAsync(string username);
    Task<bool> UpdateLastLoginAsync(Guid userId);
    Task<bool> IsUsernameAvailableAsync(string username);
}

public class AuthenticationService : IAuthenticationService
{
    private readonly GomokuDbContext _context;

    public AuthenticationService(GomokuDbContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, User? User, string Message)> RegisterUserAsync(string username, string password, string? displayName = null)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(username))
                return (false, null, "Username is required");
            
            if (string.IsNullOrWhiteSpace(password))
                return (false, null, "Password is required");

            if (password.Length < 6)
                return (false, null, "Password must be at least 6 characters");

            username = username.Trim().ToLowerInvariant();

            // Check if username already exists
            if (!await IsUsernameAvailableAsync(username))
                return (false, null, "Username already exists");

            // Create user with BCrypt + Salt
            var salt = Guid.NewGuid().ToString();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password + salt);

            var user = new User
            {
                Username = username,
                PasswordHash = passwordHash,
                Salt = salt,
                CreatedAt = DateTime.UtcNow
            };

            // Create default player profile
            var profile = new PlayerProfile
            {
                UserId = user.UserId,
                DisplayName = displayName ?? username,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            _context.PlayerProfiles.Add(profile);

            await _context.SaveChangesAsync();

            // Return user with profile loaded
            var registeredUser = await GetUserByIdAsync(user.UserId);
            return (true, registeredUser, "Registration successful");
        }
        catch (Exception ex)
        {
            return (false, null, $"Registration failed: {ex.Message}");
        }
    }

    public async Task<(bool Success, User? User, string Message)> LoginUserAsync(string username, string password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return (false, null, "Username and password are required");

            username = username.Trim().ToLowerInvariant();

            var user = await _context.Users
                .Include(u => u.PlayerProfile)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
                return (false, null, "Invalid username or password");

            // Verify password with BCrypt + Salt
            if (!BCrypt.Net.BCrypt.Verify(password + user.Salt, user.PasswordHash))
                return (false, null, "Invalid username or password");

            // Update last login
            await UpdateLastLoginAsync(user.UserId);

            return (true, user, "Login successful");
        }
        catch (Exception ex)
        {
            return (false, null, $"Login failed: {ex.Message}");
        }
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users
            .Include(u => u.PlayerProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId);
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        username = username.Trim().ToLowerInvariant();
        return await _context.Users
            .Include(u => u.PlayerProfile)
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<bool> UpdateLastLoginAsync(Guid userId)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsUsernameAvailableAsync(string username)
    {
        username = username.Trim().ToLowerInvariant();
        return !await _context.Users.AnyAsync(u => u.Username == username);
    }
}