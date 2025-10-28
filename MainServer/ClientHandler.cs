using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SharedLib.Models;
using SharedLib.Database.Models;

namespace MainServer;

public class ClientHandler : IGamePlayer
{
    public string ClientId { get; set; }
    public TcpClient TcpClient { get; set; }
    public NetworkStream Stream { get; set; }
    public PlayerInfo? PlayerInfo { get; set; }
    public GameRoom? CurrentRoom { get; set; }
    public string? PlayerSymbol { get; set; }
    public bool IsConnected { get; set; } = true;
    public int? AuthenticatedUserId { get; set; }
    public PlayerProfile? AuthenticatedProfile { get; set; }
    private readonly MainServer server;

    public ClientHandler(TcpClient tcpClient, MainServer server)
    {
        TcpClient = tcpClient;
        Stream = tcpClient.GetStream();
        ClientId = Guid.NewGuid().ToString();
        this.server = server;
    }

    public async Task HandleClientAsync()
    {
        byte[] buffer = new byte[4096];
        StringBuilder messageBuilder = new StringBuilder();

        try
        {
            while (IsConnected && TcpClient.Connected)
            {
                int bytesRead = await Stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                    break;

                string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuilder.Append(data);

                string messages = messageBuilder.ToString();
                string[] lines = messages.Split('\n');

                for (int i = 0; i < lines.Length - 1; i++)
                {
                    string message = lines[i].Trim();
                    if (!string.IsNullOrEmpty(message))
                    {
                        await ProcessMessage(message);
                    }
                }

                messageBuilder.Clear();
                if (lines.Length > 0)
                {
                    messageBuilder.Append(lines[lines.Length - 1]);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client {ClientId} error: {ex.Message}");
        }
        finally
        {
            await Disconnect();
        }
    }

    private async Task ProcessMessage(string message)
    {
        Console.WriteLine($"Client {ClientId}: {message}");

        try
        {
            if (message.StartsWith("LOGIN:"))
            {
                await HandleLogin(message.Substring("LOGIN:".Length));
            }
            else if (message.StartsWith("REGISTER:"))
            {
                await HandleRegister(message.Substring("REGISTER:".Length));
            }
            else if (message.StartsWith("PLAYER_INFO:"))
            {
                await HandlePlayerInfo(message.Substring("PLAYER_INFO:".Length));
            }
            else if (message.StartsWith("GAME_MOVE:"))
            {
                await HandleGameMove(message.Substring("GAME_MOVE:".Length));
            }
            else if (message == "FIND_MATCH")
            {
                await HandleFindMatch();
            }
            else if (message == "PLAY_WITH_AI")
            {
                await HandlePlayWithAI();
            }
            else if (message == "START_GAME")
            {
                await HandleStartGame();
            }
            else if (message == "LEAVE_MATCH")
            {
                await HandleLeaveMatch();
            }
            else if (message == "GET_PROFILE")
            {
                await HandleGetProfile();
            }
            else if (message == "GET_LEADERBOARD")
            {
                await HandleGetLeaderboard();
            }
            else if (message == "GET_GAME_HISTORY")
            {
                await HandleGetGameHistory();
            }
            else if (message == "GET_FRIENDS")
            {
                await HandleGetFriends();
            }
            else if (message.StartsWith("SEND_FRIEND_REQUEST:"))
            {
                await HandleSendFriendRequest(message.Substring("SEND_FRIEND_REQUEST:".Length));
            }
            else if (message.StartsWith("ACCEPT_FRIEND_REQUEST:"))
            {
                await HandleAcceptFriendRequest(message.Substring("ACCEPT_FRIEND_REQUEST:".Length));
            }
            else if (message.StartsWith("GET_PLAYER_STATS:"))
            {
                await HandleGetPlayerStats(message.Substring("GET_PLAYER_STATS:".Length));
            }
            else if (message.StartsWith("UPDATE_PLAYER_NAME:"))
            {
                await HandleUpdatePlayerName(message.Substring("UPDATE_PLAYER_NAME:".Length));
            }
            else if (message.StartsWith("UPDATE_AVATAR_URL:"))
            {
                await HandleUpdateAvatarUrl(message.Substring("UPDATE_AVATAR_URL:".Length));
            }
            else if (message.StartsWith("UPDATE_BIO:"))
            {
                await HandleUpdateBio(message.Substring("UPDATE_BIO:".Length));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing message from {ClientId}: {ex.Message}");
            await SendMessage($"ERROR:Failed to process message - {ex.Message}");
        }
    }

    // ==================== Authentication Handlers ====================

    private async Task HandleLogin(string json)
    {
        try
        {
            var loginData = JsonSerializer.Deserialize<LoginRequest>(json);
            if (loginData == null)
            {
                await SendMessage("LOGIN_FAILED:Invalid login data");
                return;
            }

            var (success, message, user, profile) = await server.LoginUserAsync(loginData.Username, loginData.Password);
            
            if (success && user != null && profile != null)
            {
                AuthenticatedUserId = user.UserId;
                AuthenticatedProfile = profile;
                
                // Set player info from profile
                PlayerInfo = new PlayerInfo
                {
                    PlayerId = profile.ProfileId.ToString(),
                    PlayerName = profile.PlayerName,
                    PlayerLevel = profile.Level,
                    PlayerElo = profile.Elo,
                    AvatarUrl = profile.AvatarUrl
                };

                var responseData = new
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    ProfileId = profile.ProfileId,
                    PlayerName = profile.PlayerName,
                    Elo = profile.Elo,
                    Level = profile.Level,
                    Wins = profile.Wins,
                    Losses = profile.Losses,
                    TotalGames = profile.TotalGames,
                    AvatarUrl = profile.AvatarUrl
                };

                await SendMessage($"LOGIN_SUCCESS:{JsonSerializer.Serialize(responseData)}");
                Console.WriteLine($"User {user.Username} logged in successfully");
            }
            else
            {
                await SendMessage($"LOGIN_FAILED:{message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Login error: {ex.Message}");
            await SendMessage($"LOGIN_FAILED:Invalid login format");
        }
    }

    private async Task HandleRegister(string json)
    {
        try
        {
            var registerData = JsonSerializer.Deserialize<RegisterRequest>(json);
            if (registerData == null)
            {
                await SendMessage("REGISTER_FAILED:Invalid registration data");
                return;
            }

            var (success, message, user) = await server.RegisterUserAsync(
                registerData.Username, 
                registerData.Password, 
                registerData.Email, 
                registerData.PlayerName);

            if (success)
            {
                await SendMessage($"REGISTER_SUCCESS:{message}");
                Console.WriteLine($"New user {registerData.Username} registered");
            }
            else
            {
                await SendMessage($"REGISTER_FAILED:{message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Registration error: {ex.Message}");
            await SendMessage($"REGISTER_FAILED:Invalid registration format");
        }
    }

    // ==================== Profile & Stats Handlers ====================

    private async Task HandleGetProfile()
    {
        if (AuthenticatedProfile == null)
        {
            await SendMessage("ERROR:Not authenticated");
            return;
        }

        try
        {
            var profile = await server.GetPlayerProfileAsync(AuthenticatedProfile.ProfileId);
            if (profile != null)
            {
                var rank = await server.GetPlayerRankAsync(profile.ProfileId);
                var responseData = new
                {
                    ProfileId = profile.ProfileId,
                    PlayerName = profile.PlayerName,
                    Elo = profile.Elo,
                    Level = profile.Level,
                    TotalGames = profile.TotalGames,
                    Wins = profile.Wins,
                    Losses = profile.Losses,
                    Draws = profile.Draws,
                    WinRate = profile.WinRate,
                    Bio = profile.Bio,
                    AvatarUrl = profile.AvatarUrl,
                    Rank = rank
                };

                await SendMessage($"PROFILE_DATA:{JsonSerializer.Serialize(responseData)}");
            }
            else
            {
                await SendMessage("ERROR:Profile not found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Get profile error: {ex.Message}");
            await SendMessage($"ERROR:Failed to get profile");
        }
    }

    private async Task HandleGetLeaderboard()
    {
        try
        {
            var topPlayers = await server.GetLeaderboardAsync(100);
            var leaderboardData = topPlayers.Select((p, index) => new
            {
                Rank = index + 1,
                PlayerName = p.PlayerName,
                Elo = p.Elo,
                Level = p.Level,
                Wins = p.Wins,
                TotalGames = p.TotalGames,
                WinRate = p.WinRate
            }).ToList();

            await SendMessage($"LEADERBOARD_DATA:{JsonSerializer.Serialize(leaderboardData)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Get leaderboard error: {ex.Message}");
            await SendMessage($"ERROR:Failed to get leaderboard");
        }
    }

    private async Task HandleGetGameHistory()
    {
        if (AuthenticatedProfile == null)
        {
            await SendMessage("ERROR:Not authenticated");
            return;
        }

        try
        {
            var games = await server.GetPlayerGameHistoryAsync(AuthenticatedProfile.ProfileId, 20);
            var historyData = games.Select(g => new
            {
                GameId = g.GameId,
                Player1Name = g.Player1.PlayerName,
                Player2Name = g.Player2?.PlayerName ?? "AI",
                IsAIGame = g.IsAIGame,
                GameResult = g.GameResult,
                WinnerName = g.Winner?.PlayerName ?? (g.IsAIGame ? "AI" : "N/A"),
                TotalMoves = g.TotalMoves,
                GameDuration = g.GameDuration.TotalSeconds,
                EloChange = g.Player1Id == AuthenticatedProfile.ProfileId ? g.Player1EloChange : g.Player2EloChange,
                PlayedAt = g.PlayedAt
            }).ToList();

            await SendMessage($"GAME_HISTORY_DATA:{JsonSerializer.Serialize(historyData)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Get game history error: {ex.Message}");
            await SendMessage($"ERROR:Failed to get game history");
        }
    }

    private async Task HandleGetPlayerStats(string playerName)
    {
        try
        {
            var profile = await server.GetPlayerProfileByNameAsync(playerName);
            if (profile != null)
            {
                var rank = await server.GetPlayerRankAsync(profile.ProfileId);
                var stats = new
                {
                    PlayerName = profile.PlayerName,
                    Elo = profile.Elo,
                    Level = profile.Level,
                    TotalGames = profile.TotalGames,
                    Wins = profile.Wins,
                    Losses = profile.Losses,
                    Draws = profile.Draws,
                    WinRate = profile.WinRate,
                    Rank = rank
                };

                await SendMessage($"PLAYER_STATS_DATA:{JsonSerializer.Serialize(stats)}");
            }
            else
            {
                await SendMessage($"ERROR:Player '{playerName}' not found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Get player stats error: {ex.Message}");
            await SendMessage($"ERROR:Failed to get player stats");
        }
    }

    // ==================== Friendship Handlers ====================

    private async Task HandleGetFriends()
    {
        if (AuthenticatedProfile == null)
        {
            await SendMessage("ERROR:Not authenticated");
            return;
        }

        try
        {
            var friends = await server.GetFriendsAsync(AuthenticatedProfile.ProfileId);
            var friendsData = friends.Select(f => new
            {
                ProfileId = f.ProfileId,
                PlayerName = f.PlayerName,
                Elo = f.Elo,
                Level = f.Level,
                Wins = f.Wins,
                TotalGames = f.TotalGames
            }).ToList();

            await SendMessage($"FRIENDS_DATA:{JsonSerializer.Serialize(friendsData)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Get friends error: {ex.Message}");
            await SendMessage($"ERROR:Failed to get friends");
        }
    }

    private async Task HandleSendFriendRequest(string targetPlayerName)
    {
        if (AuthenticatedProfile == null)
        {
            await SendMessage("ERROR:Not authenticated");
            return;
        }

        try
        {
            var targetProfile = await server.GetPlayerProfileByNameAsync(targetPlayerName);
            if (targetProfile == null)
            {
                await SendMessage($"ERROR:Player '{targetPlayerName}' not found");
                return;
            }

            var (success, message, _) = await server.SendFriendRequestAsync(AuthenticatedProfile.ProfileId, targetProfile.ProfileId);
            if (success)
            {
                await SendMessage($"FRIEND_REQUEST_SENT:{message}");
            }
            else
            {
                await SendMessage($"ERROR:{message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Send friend request error: {ex.Message}");
            await SendMessage($"ERROR:Failed to send friend request");
        }
    }

    private async Task HandleAcceptFriendRequest(string friendshipIdStr)
    {
        if (AuthenticatedProfile == null)
        {
            await SendMessage("ERROR:Not authenticated");
            return;
        }

        try
        {
            if (int.TryParse(friendshipIdStr, out int friendshipId))
            {
                var success = await server.AcceptFriendRequestAsync(friendshipId, AuthenticatedProfile.ProfileId);
                if (success)
                {
                    await SendMessage("FRIEND_REQUEST_ACCEPTED:Friend request accepted");
                }
                else
                {
                    await SendMessage("ERROR:Failed to accept friend request");
                }
            }
            else
            {
                await SendMessage("ERROR:Invalid friendship ID");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Accept friend request error: {ex.Message}");
            await SendMessage($"ERROR:Failed to accept friend request");
        }
    }

    // ==================== Game Handlers ====================

    private async Task HandlePlayerInfo(string json)
    {
        try
        {
            PlayerInfo = JsonSerializer.Deserialize<PlayerInfo>(json);
            Console.WriteLine($"Player {PlayerInfo?.PlayerName} connected with ID {ClientId}");
            await SendMessage("PLAYER_INFO_ACK:Player info received");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse player info: {ex.Message}");
            await SendMessage("ERROR:Invalid player info format");
        }
    }

    private async Task HandleGameMove(string json)
    {
        if (CurrentRoom == null || !CurrentRoom.IsGameActive)
        {
            await SendMessage("ERROR:Not in an active game");
            return;
        }

        if (CurrentRoom.CurrentPlayer != PlayerSymbol)
        {
            await SendMessage("ERROR:Not your turn");
            return;
        }

        try
        {
            MoveData? clientMove = JsonSerializer.Deserialize<MoveData>(json);
            if (clientMove == null) 
            {
                await SendMessage("ERROR:Invalid move format");
                return;
            }

            if (await server.ProcessGameMove(CurrentRoom, this, clientMove))
            {
                // Move was valid and processed
                var opponent = CurrentRoom.GetOpponent(this);
                if (opponent != null)
                {
                    await opponent.SendMessage($"GAME_MOVE:{json}");
                }
                await SendMessage($"GAME_MOVE:{json}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse game move: {ex.Message}");
            await SendMessage("ERROR:Invalid move format");
        }
    }

    private async Task HandleFindMatch()
    {
        if (PlayerInfo == null)
        {
            await SendMessage("ERROR:Send player info first");
            return;
        }

        var room = await server.FindOrCreateRoom(this);
        if (room != null)
        {
            CurrentRoom = room;
            await SendMessage($"JOIN_ROOM:{room.RoomId}");

            if (room.IsFull)
            {
                // Send the info so both players know about each other
                var opponent = room.GetOpponent(this);
                if (opponent != null)
                {
                    if (opponent.PlayerInfo != null)
                    {
                        string opponentJson = JsonSerializer.Serialize(opponent.PlayerInfo);
                        await SendMessage($"OPPONENT_INFO:{opponentJson}");
                    }

                    if (PlayerInfo != null)
                    {
                        string playerJson = JsonSerializer.Serialize(PlayerInfo);
                        await opponent.SendMessage($"OPPONENT_INFO:{playerJson}");
                    }
                    await SendMessage("MATCH_FOUND:Match found, ready to start");
                    await opponent.SendMessage("MATCH_FOUND:Match found, ready to start");
                }
            }
            else
            {
                await SendMessage("WAITING_FOR_OPPONENT:Waiting for another player");
            }
        }
        else
        {
            await SendMessage("ERROR:Failed to find or create match");
        }
    }

    private async Task HandleStartGame()
    {
        if (CurrentRoom == null)
        {
            await SendMessage("ERROR:Not in a room");
            return;
        }

        if (!CurrentRoom.IsFull)
        {
            await SendMessage("ERROR:Waiting for opponent");
            return;
        }

        // Mark this player as ready
        if (CurrentRoom.Player1 == this)
        {
            CurrentRoom.Player1Ready = true;
        }
        else if (CurrentRoom.Player2 == this)
        {
            CurrentRoom.Player2Ready = true;
        }

        await SendMessage("READY_ACK:You are ready to start");

        // Check if both players are ready
        if (CurrentRoom.BothPlayersReady)
        {
            await SendMessage("BOTH_READY:Both players are ready, starting game");
            var opponent = CurrentRoom.GetOpponent(this);
            if (opponent != null)
            {
                await opponent.SendMessage("BOTH_READY:Both players are ready, starting game");
            }
            await Task.Delay(1000);

            await server.StartGame(CurrentRoom);
        }
        else
        {
            await SendMessage("WAITING_FOR_OPPONENT_READY:Waiting for opponent to be ready");

            // Notify opponent that this player is ready
            var opponent = CurrentRoom.GetOpponent(this);
            if (opponent != null)
            {
                await opponent.SendMessage("OPPONENT_READY:Your opponent is ready to start");
            }
        }
    }

    private async Task HandlePlayWithAI()
    {
        if (PlayerInfo == null)
        {
            await SendMessage("ERROR:Send player info first");
            return;
        }

        var room = await server.CreateAIRoom(this);
        if (room != null)
        {
            CurrentRoom = room;
            await SendMessage($"AI_MATCH_FOUND:{room.RoomId}");
        }
        else
        {
            await SendMessage("ERROR:Failed to create AI game");
        }
    }

    private async Task HandleLeaveMatch()
    {
        if (CurrentRoom != null)
        {
            await server.LeaveRoom(this, CurrentRoom);
            CurrentRoom = null!;
            await SendMessage("MATCH_LEFT:You left the match");
        }
    }

    // ==================== Profile Update Handlers ====================

    private async Task HandleUpdatePlayerName(string json)
    {
        if (AuthenticatedProfile == null)
        {
            await SendMessage("ERROR:Not authenticated");
            return;
        }

        try
        {
            var updateRequest = JsonSerializer.Deserialize<UpdatePlayerNameRequest>(json);
            if (updateRequest == null)
            {
                await SendMessage("ERROR:Invalid request format");
                return;
            }

            string newPlayerName = updateRequest.PlayerName?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(newPlayerName) || newPlayerName.Length < 3 || newPlayerName.Length > 50)
            {
                await SendMessage("ERROR:Player name must be between 3 and 50 characters");
                return;
            }

            bool success = await server.UpdatePlayerNameAsync(AuthenticatedProfile.ProfileId, newPlayerName);

            if (success)
            {
                // Update local cache
                AuthenticatedProfile.PlayerName = newPlayerName;
                if (PlayerInfo != null)
                {
                    PlayerInfo.PlayerName = newPlayerName;
                }

                var responseData = new
                {
                    ProfileId = AuthenticatedProfile.ProfileId,
                    NewPlayerName = newPlayerName,
                    Message = "Player name updated successfully"
                };

                await SendMessage($"UPDATE_PLAYER_NAME_SUCCESS:{JsonSerializer.Serialize(responseData)}");
                Console.WriteLine($"Player name updated for profile {AuthenticatedProfile.ProfileId}: {newPlayerName}");
            }
            else
            {
                await SendMessage("ERROR:Failed to update player name (possibly duplicate name)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Update player name error: {ex.Message}");
            await SendMessage($"ERROR:Failed to update player name - {ex.Message}");
        }
    }

    private async Task HandleUpdateAvatarUrl(string json)
    {
        if (AuthenticatedProfile == null)
        {
            await SendMessage("ERROR:Not authenticated");
            return;
        }

        try
        {
            var updateRequest = JsonSerializer.Deserialize<UpdateAvatarRequest>(json);
            if (updateRequest == null)
            {
                await SendMessage("ERROR:Invalid request format");
                return;
            }

            string newAvatarUrl = updateRequest.AvatarUrl?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(newAvatarUrl) || newAvatarUrl.Length > 500)
            {
                await SendMessage("ERROR:Avatar URL must not be empty and less than 500 characters");
                return;
            }

            bool success = await server.UpdateAvatarUrlAsync(AuthenticatedProfile.ProfileId, newAvatarUrl);

            if (success)
            {
                // Update local cache
                AuthenticatedProfile.AvatarUrl = newAvatarUrl;
                if (PlayerInfo != null)
                {
                    PlayerInfo.AvatarUrl = newAvatarUrl;
                }

                var responseData = new
                {
                    ProfileId = AuthenticatedProfile.ProfileId,
                    NewAvatarUrl = newAvatarUrl,
                    Message = "Avatar URL updated successfully"
                };

                await SendMessage($"UPDATE_AVATAR_URL_SUCCESS:{JsonSerializer.Serialize(responseData)}");
                Console.WriteLine($"Avatar URL updated for profile {AuthenticatedProfile.ProfileId}");
            }
            else
            {
                await SendMessage("ERROR:Failed to update avatar URL");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Update avatar URL error: {ex.Message}");
            await SendMessage($"ERROR:Failed to update avatar URL - {ex.Message}");
        }
    }

    private async Task HandleUpdateBio(string json)
    {
        if (AuthenticatedProfile == null)
        {
            await SendMessage("ERROR:Not authenticated");
            return;
        }

        try
        {
            var updateRequest = JsonSerializer.Deserialize<UpdateBioRequest>(json);
            if (updateRequest == null)
            {
                await SendMessage("ERROR:Invalid request format");
                return;
            }

            string newBio = updateRequest.NewBio?.Trim() ?? string.Empty;

            if (newBio.Length > 500)
            {
                await SendMessage("ERROR:Bio must be less than 500 characters");
                return;
            }

            bool success = await server.UpdateBioAsync(AuthenticatedProfile.ProfileId, newBio);

            if (success)
            {
                // Update local cache
                AuthenticatedProfile.Bio = newBio;

                var responseData = new
                {
                    ProfileId = AuthenticatedProfile.ProfileId,
                    NewBio = newBio,
                    Message = "Bio updated successfully"
                };

                await SendMessage($"UPDATE_BIO_SUCCESS:{JsonSerializer.Serialize(responseData)}");
                Console.WriteLine($"Bio updated for profile {AuthenticatedProfile.ProfileId}");
            }
            else
            {
                await SendMessage("ERROR:Failed to update bio");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Update bio error: {ex.Message}");
            await SendMessage($"ERROR:Failed to update bio - {ex.Message}");
        }
    }

    public async Task SendMessage(string message)
    {
        if (!IsConnected || !TcpClient.Connected)
            return;

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message + "\n");
            await Stream.WriteAsync(data, 0, data.Length);
            await Stream.FlushAsync();
            Console.WriteLine($"Sent to {ClientId}: {message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send message to {ClientId}: {ex.Message}");
            await Disconnect();
        }
    }

    public async Task Disconnect()
    {
        if (!IsConnected)
            return;

        IsConnected = false;

        if (CurrentRoom != null)
        {
            await server.LeaveRoom(this, CurrentRoom);
        }

        try
        {
            Stream?.Close();
            TcpClient?.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during disconnect for {ClientId}: {ex.Message}");
        }

        server.RemoveClient(this);
        Console.WriteLine($"Client {ClientId} disconnected");
    }
}
