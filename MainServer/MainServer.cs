﻿using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using SharedLib.GameEngine;
using SharedLib.Communication;
using SharedLib.Models;
using SharedLib.Database;
using SharedLib.Database.Models;
using SharedLib.Services;
using MainServer.Services;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace MainServer;

public class MainServer
{
    private TcpListener? tcpListener;
    private TcpListener? workerListener;
    private bool isRunning = false;
    private readonly ConcurrentDictionary<string, ClientHandler> clients = new();
    private readonly ConcurrentDictionary<string, WorkerConnection> workers = new();
    private readonly MatchmakingService matchmakingService = new();
    private readonly LoadBalancer loadBalancer = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<WorkerResponse>> pendingRequests = new();

    // Database services
    private readonly GomokuDbContext dbContext;
    private readonly UserService userService;
    private readonly PlayerProfileService profileService;
    private readonly GameHistoryService gameHistoryService;
    private readonly FriendshipService friendshipService;

    private readonly int port;
    private readonly int workerPort;

    public class WorkerConnection
    {
        public string WorkerId { get; set; } = string.Empty;
        public TcpClient Client { get; set; } = null!;
        public NetworkStream Stream { get; set; } = null!;
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    }

    public MainServer(int port = 5000, int workerPort = 5002)
    {
        this.port = port;
        this.workerPort = workerPort;

        // Initialize database context
        var optionsBuilder = new DbContextOptionsBuilder<GomokuDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=GomokuGameDB;Trusted_Connection=True;MultipleActiveResultSets=true");
        
        dbContext = new GomokuDbContext(optionsBuilder.Options);
        
        // Initialize services
        profileService = new PlayerProfileService(dbContext);
        userService = new UserService(dbContext);
        gameHistoryService = new GameHistoryService(dbContext);
        friendshipService = new FriendshipService(dbContext);
    }

    public async Task StartAsync()
    {
        // Start client listener
        tcpListener = new TcpListener(IPAddress.Any, port);
        tcpListener.Start();
        
        // Start worker listener  
        workerListener = new TcpListener(IPAddress.Any, workerPort);
        workerListener.Start();
        
        isRunning = true;

        Console.WriteLine($"Game Server started on port {port}");
        Console.WriteLine($"Worker port: {workerPort} - waiting for workers...");

        // Listen for workers
        _ = Task.Run(ListenForWorkersAsync);

        // Start cleanup task
        _ = Task.Run(CleanupRoomsAsync);

        // Start load monitoring
        _ = Task.Run(MonitorLoadAsync);

        // Start server discovery
        _ = Task.Run(ServerDiscovery);

        // Listen for game clients
        while (isRunning)
        {
            try
            {
                var tcpClient = await tcpListener.AcceptTcpClientAsync();
                var clientHandler = new ClientHandler(tcpClient, this);
                clients[clientHandler.ClientId] = clientHandler;

                Console.WriteLine($"New client connected: {clientHandler.ClientId}");

                // Handle client in background
                _ = Task.Run(() => clientHandler.HandleClientAsync());
            }
            catch (Exception ex)
            {
                if (isRunning)
                {
                    Console.WriteLine($"Error accepting client: {ex.Message}");
                }
            }
        }
    }

    private async Task ListenForWorkersAsync()
    {
        while (isRunning)
        {
            try
            {
                var workerClient = await workerListener!.AcceptTcpClientAsync();
                Console.WriteLine($"Worker connected: {workerClient.Client.RemoteEndPoint}");

                // Handle worker in background
                _ = Task.Run(() => HandleWorkerAsync(workerClient));
            }
            catch (Exception ex)
            {
                if (isRunning)
                {
                    Console.WriteLine($"Error accepting worker: {ex.Message}");
                }
            }
        }
    }

    private async Task HandleWorkerAsync(TcpClient workerClient)
    {
        var stream = workerClient.GetStream();
        var buffer = new byte[4096];
        var messageBuilder = new StringBuilder();
        string? workerId = null;

        try
        {
            while (isRunning && workerClient.Connected)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuilder.Append(data);

                string messages = messageBuilder.ToString();
                string[] lines = messages.Split('\n');

                for (int i = 0; i < lines.Length - 1; i++)
                {
                    string message = lines[i].Trim();
                    if (!string.IsNullOrEmpty(message))
                    {
                        workerId = await ProcessWorkerMessage(message, workerClient, stream, workerId);
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
            Console.WriteLine($"Worker error: {ex.Message}");
        }
        finally
        {
            if (workerId != null)
            {
                workers.TryRemove(workerId, out _);
                Console.WriteLine($"Worker {workerId} disconnected");
            }
            stream?.Close();
            workerClient?.Close();
        }
    }

    private async Task<string?> ProcessWorkerMessage(string message, TcpClient client, NetworkStream stream, string? currentWorkerId)
    {
        try
        {
            var request = JsonSerializer.Deserialize<WorkerRequest>(message);
            if (request == null) return currentWorkerId;

            switch (request.Type)
            {
                case WorkerProtocol.WORKER_REGISTRATION:
                    var regData = JsonSerializer.Deserialize<JsonElement>(request.Data);
                    var workerId = regData.GetProperty("WorkerId").GetString() ?? Guid.NewGuid().ToString();
                    
                    workers[workerId] = new WorkerConnection
                    {
                        WorkerId = workerId,
                        Client = client,
                        Stream = stream,
                        LastSeen = DateTime.UtcNow
                    };

                    // Send ack
                    var ack = new WorkerResponse
                    {
                        RequestId = request.RequestId,
                        Type = WorkerProtocol.WORKER_REGISTRATION_ACK,
                        Status = WorkerProtocol.SUCCESS,
                        Data = JsonSerializer.Serialize(new { WorkerId = workerId })
                    };
                    await SendToWorker(stream, ack);

                    Console.WriteLine($"Worker {workerId} registered");
                    return workerId;

                default:
                    // Handle responses from worker
                    if (pendingRequests.TryRemove(request.RequestId, out var tcs))
                    {
                        var response = new WorkerResponse
                        {
                            RequestId = request.RequestId,
                            Type = request.Type,
                            Status = WorkerProtocol.SUCCESS,
                            Data = request.Data,
                            Timestamp = request.Timestamp
                        };
                        tcs.SetResult(response);
                    }
                    break;
            }

            return currentWorkerId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing worker message: {ex.Message}");
            return currentWorkerId;
        }
    }

    private async Task SendToWorker(NetworkStream stream, object message)
    {
        try
        {
            string json = JsonSerializer.Serialize(message);
            byte[] data = Encoding.UTF8.GetBytes(json + "\n");
            await stream.WriteAsync(data, 0, data.Length);
            await stream.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send to worker: {ex.Message}");
        }
    }

    private async Task<WorkerResponse?> SendRequestToWorker(WorkerRequest request, int timeoutMs = 5000)
    {
        var worker = workers.Values.FirstOrDefault();
        if (worker == null) return null;

        try
        {
            var tcs = new TaskCompletionSource<WorkerResponse>();
            pendingRequests[request.RequestId] = tcs;

            await SendToWorker(worker.Stream, request);

            using var cts = new CancellationTokenSource(timeoutMs);
            var timeoutTask = Task.Delay(timeoutMs, cts.Token);
            var responseTask = tcs.Task;

            var completedTask = await Task.WhenAny(responseTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                pendingRequests.TryRemove(request.RequestId, out _);
                return null;
            }

            cts.Cancel();
            return await responseTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending to worker: {ex.Message}");
            return null;
        }
    }

    public Task<GameRoom> FindOrCreateRoom(ClientHandler client)
    {
        loadBalancer.IncrementLoad();
        return matchmakingService.FindOrCreateRoom(client);
    }

    public Task<GameRoom> CreateAIRoom(ClientHandler client)
    {
        loadBalancer.IncrementLoad();
        return matchmakingService.CreateAIRoom(client);
    }

    public async Task StartGame(GameRoom room)
    {
        if (room.IsGameActive)
            return;

        room.IsGameActive = true;
        room.LastActivity = DateTime.Now;

        var startMessage = $"GAME_START:{{\"roomId\":\"{room.RoomId}\",\"currentPlayer\":\"{room.CurrentPlayer}\"}}";

        if (room.Player1 != null)
            await room.Player1.SendMessage(startMessage);

        if (room.Player2 != null)
            await room.Player2.SendMessage(startMessage);

        Console.WriteLine($"Game started in room {room.RoomId}");

        // If it's an AI game and human player goes first (X), let them start
        // If AI goes first, make the first AI move
        if (room.IsAIGame && room.CurrentPlayer == room.AISymbol)
        {
            _ = Task.Run(async () => await HandleAITurn(room));
        }
    }

    public async Task<bool> ProcessGameMove(GameRoom room, IGamePlayer player, MoveData move)
    {
        bool isValid;
        bool isWinning = false;
        bool isDraw = false;

        if (workers.Any())
        {
            var validationRequest = new MoveValidationRequest
            {
                Board = ConvertTo2DArray(room.Board),
                Row = move.row,
                Col = move.col,
                PlayerSymbol = player.PlayerSymbol ?? ""
            };

            var request = new WorkerRequest
            {
                Type = WorkerProtocol.VALIDATE_MOVE_REQUEST,
                Data = JsonSerializer.Serialize(validationRequest)
            };

            var response = await SendRequestToWorker(request);
            
            if (response?.Status == WorkerProtocol.SUCCESS && response.Data != null)
            {
                var validationResponse = JsonSerializer.Deserialize<MoveValidationResponse>(response.Data);
                isValid = validationResponse?.IsValid ?? false;
                isWinning = validationResponse?.IsWinning ?? false;
                isDraw = validationResponse?.IsDraw ?? false;
            }
            else
            {
                // Fallback to local processing
                isValid = ProcessMoveLocally(room, player, move, out isWinning, out isDraw);
            }
        }
        else
        {
            // Process locally
            isValid = ProcessMoveLocally(room, player, move, out isWinning, out isDraw);
        }

        if (!isValid)
        {
            await player.SendMessage("ERROR:Invalid move");
            return false;
        }

        // Apply move
        room.Board[move.row, move.col] = player.PlayerSymbol ?? "";
        room.LastActivity = DateTime.Now;

        // Handle game end
        if (isWinning)
        {
            await EndGame(room, player, "WIN");
            return true;
        }

        if (isDraw)
        {
            await EndGame(room, null, "DRAW");
            return true;
        }

        // Switch turns
        room.CurrentPlayer = room.CurrentPlayer == "X" ? "O" : "X";

        var turnMessage = $"TURN_CHANGE:{{\"currentPlayer\":\"{room.CurrentPlayer}\"}}";
        if (room.Player1 != null)
            await room.Player1.SendMessage(turnMessage);
        if (room.Player2 != null)
            await room.Player2.SendMessage(turnMessage);

        // Handle AI turn if it's an AI game and now it's AI's turn
        if (room.IsCurrentPlayerAI())
        {
            _ = Task.Run(async () => await HandleAITurn(room));
        }

        return true;
    }

    private bool ProcessMoveLocally(GameRoom room, IGamePlayer player, MoveData move, out bool isWinning, out bool isDraw)
    {
        isWinning = false;
        isDraw = false;

        if (!GameLogic.IsValidMove(room.Board, move.row, move.col))
            return false;

        // Create temp board to check win condition
        var tempBoard = GameLogic.CopyBoard(room.Board);
        tempBoard[move.row, move.col] = player.PlayerSymbol ?? "";

        // Check for win condition using SharedLib
        isWinning = GameLogic.CheckWin(tempBoard, move.row, move.col, player.PlayerSymbol ?? "");

        // Check for draw using SharedLib
        if (!isWinning)
            isDraw = GameLogic.IsBoardFull(tempBoard);

        return true;
    }

    public async Task<(int row, int col)> GetAIMove(GameRoom room, string aiSymbol)
    {
        if (workers.Any())
        {
            var aiRequest = new AIRequest
            {
                Board = ConvertTo2DArray(room.Board),
                AISymbol = aiSymbol,
                RoomId = room.RoomId
            };

            var request = new WorkerRequest
            {
                Type = WorkerProtocol.AI_MOVE_REQUEST,
                Data = JsonSerializer.Serialize(aiRequest)
            };

            var response = await SendRequestToWorker(request);
            
            if (response?.Status == WorkerProtocol.SUCCESS && response.Data != null)
            {
                var aiResponse = JsonSerializer.Deserialize<AIResponse>(response.Data);
                if (aiResponse?.IsValid == true)
                {
                    return (aiResponse.Row, aiResponse.Col);
                }
            }
            
            Console.WriteLine("Worker AI failed, using local AI");
        }

        // Fallback to local AI using SharedLib GomokuAI
        return GetSmartAIMove(room.Board, aiSymbol);
    }

    private (int row, int col) GetSmartAIMove(string[,] board, string aiSymbol)
    {
        try
        {
            // Use SharedLib GomokuAI for smart moves
            var ai = new SharedLib.AI.GomokuAI(aiSymbol);
            var move = ai.GetBestMove(board);
            return move;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error using GomokuAI: {ex.Message}, falling back to random move");
            return GetRandomValidMove(board);
        }
    }

    private (int row, int col) GetRandomValidMove(string[,] board)
    {
        var random = new Random();
        var validMoves = new List<(int, int)>();

        for (int i = 0; i < GameLogic.BOARD_SIZE; i++)
        {
            for (int j = 0; j < GameLogic.BOARD_SIZE; j++)
            {
                if (GameLogic.IsValidMove(board, i, j))
                {
                    validMoves.Add((i, j));
                }
            }
        }

        return validMoves.Count > 0 ? validMoves[random.Next(validMoves.Count)] : (-1, -1);
    }

    private string[][] ConvertTo2DArray(string[,] board)
    {
        var result = new string[15][];
        for (int i = 0; i < 15; i++)
        {
            result[i] = new string[15];
            for (int j = 0; j < 15; j++)
            {
                result[i][j] = board[i, j] ?? string.Empty;
            }
        }
        return result;
    }

    private async Task EndGame(GameRoom room, IGamePlayer? winner, string reason)
    {
        room.IsGameActive = false;
        loadBalancer.DecrementLoad();

        string endMessage = $"GAME_END:{{\"reason\":\"{reason}\",\"winner\":\"{winner?.PlayerSymbol ?? "NONE"}\"}}";

        if (room.Player1 != null)
            await room.Player1.SendMessage(endMessage);
        if (room.Player2 != null)
            await room.Player2.SendMessage(endMessage);

        Console.WriteLine($"Game ended in room {room.RoomId}: {reason}");

        // Record game result to database
        try
        {
            // Get profile IDs from players
            var player1Client = room.Player1 as ClientHandler;
            var player2Client = room.Player2 as ClientHandler;
            var aiClient = room.Player2 as AIClientHandler;

            if (player1Client?.AuthenticatedProfile != null)
            {
                int player1ProfileId = player1Client.AuthenticatedProfile.ProfileId;
                int? player2ProfileId = null;
                int? winnerProfileId = null;
                int totalMoves = CountTotalMoves(room.Board);
                int gameDurationSeconds = (int)(DateTime.Now - (room.LastActivity - TimeSpan.FromHours(1))).TotalSeconds; // Rough estimate
                string gameMode = room.IsAIGame ? "AI" : "Ranked";

                if (room.IsAIGame)
                {
                    // AI game: only player1 is recorded
                    player2ProfileId = null;
                    
                    // Determine winner based on who made the winning move
                    if (reason == "DRAW")
                    {
                        winnerProfileId = null; // Draw
                    }
                    else if (reason == "WIN")
                    {
                        winnerProfileId = (winner == room.Player1) ? player1ProfileId : (int?)null;
                    }
                    else
                    {
                        // Loss or other reason
                        winnerProfileId = null;
                    }

                    // For AI games, record with null Player2Id and mark IsAIGame=true
                    var aiGameResult = await gameHistoryService.RecordAIGameAsync(
                        player1ProfileId,
                        winnerProfileId,
                        reason,
                        totalMoves,
                        gameDurationSeconds,
                        gameMode);
                    
                    Console.WriteLine($"AI game recorded: Player1={player1ProfileId}, Winner={winnerProfileId}, Reason={reason}");
                }
                else if (player2Client?.AuthenticatedProfile != null)
                {
                    // Player vs Player: both players are recorded
                    player2ProfileId = player2Client.AuthenticatedProfile.ProfileId;
                    winnerProfileId = winner == room.Player1 ? player1ProfileId : (winner == room.Player2 ? player2ProfileId : (int?)null);

                    await RecordGameResultAsync(
                        player1ProfileId,
                        player2ProfileId,
                        false,
                        reason,
                        winnerProfileId,
                        totalMoves,
                        TimeSpan.FromSeconds(gameDurationSeconds),
                        gameMode);
                    
                    Console.WriteLine($"PvP game recorded: Player1={player1ProfileId}, Player2={player2ProfileId}, Winner={winnerProfileId}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error recording game result: {ex.Message}");
        }
    }

    private int CountTotalMoves(string[,] board)
    {
        int count = 0;
        for (int i = 0; i < board.GetLength(0); i++)
        {
            for (int j = 0; j < board.GetLength(1); j++)
            {
                if (!string.IsNullOrEmpty(board[i, j]))
                    count++;
            }
        }
        return count;
    }

    public async Task LeaveRoom(IGamePlayer client, GameRoom room)
    {
        matchmakingService.LeaveRoom(client, room);
        loadBalancer.DecrementLoad();

        var opponent = room.GetOpponent(client);
        if (opponent != null)
        {
            await opponent.SendMessage("OPPONENT_LEFT:Your opponent left the game");
        }
    }

    public void RemoveClient(ClientHandler client)
    {
        clients.TryRemove(client.ClientId, out _);
    }

    private async Task CleanupRoomsAsync()
    {
        while (isRunning)
        {
            try
            {
                await matchmakingService.CleanupRoomsAsync();
                await Task.Delay(TimeSpan.FromMinutes(1));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in cleanup task: {ex.Message}");
            }
        }
    }

    private async Task MonitorLoadAsync()
    {
        while (isRunning)
        {
            try
            {
                var stats = loadBalancer.GetLoadStats();
                Console.WriteLine($"Load Stats - System: {stats.SystemLoad}%, Games: {stats.GameLoad}%, Level: {stats.LoadLevel}");
                Console.WriteLine($"Workers connected: {workers.Count}");

                await Task.Delay(TimeSpan.FromSeconds(30));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in load monitoring: {ex.Message}");
            }
        }
    }

    private async Task ServerDiscovery()
    {
        int udpPort = 5001;
        var udp = new UdpClient(udpPort);

        while (true)
        {
            try
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                var data = udp.Receive(ref remote);
                if (Encoding.UTF8.GetString(data) == "DISCOVER")
                {
                    var ip = GetLocalIP();
                    var reply = $"{ip}:{port}";
                    udp.Send(Encoding.UTF8.GetBytes(reply), reply.Length, remote);
                    Console.WriteLine($"Reply {reply} to {remote}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UDP Discovery error: {ex.Message}");
            }
        }
    }
    
    private string GetLocalIP()
    {
        foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();
        return "127.0.0.1";
    }

    private async Task HandleAITurn(GameRoom room)
    {
        try
        {
            if (!room.IsGameActive || !room.IsCurrentPlayerAI())
                return;

            var aiMove = await GetAIMove(room, room.AISymbol);
            
            if (aiMove.row >= 0 && aiMove.col >= 0)
            {
                var moveData = new MoveData { row = aiMove.row, col = aiMove.col };
                
                // Create a dummy AI client for processing
                var aiClient = new AIClientHandler(room.AISymbol);
                
                // Process AI move
                if (await ProcessGameMove(room, aiClient, moveData))
                {
                    // Notify human player about AI move
                    var humanPlayer = room.GetHumanPlayer();
                    if (humanPlayer != null)
                    {
                        var aiMoveJson = JsonSerializer.Serialize(moveData);
                        await humanPlayer.SendMessage($"AI_MOVE:{aiMoveJson}");
                    }
                }
            }
            else
            {
                Console.WriteLine("AI couldn't find a valid move - this shouldn't happen!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in AI turn: {ex.Message}");
        }
    }

    // ==================== Database Service Wrapper Methods ====================

    public async Task<(bool Success, string Message, User? User, PlayerProfile? Profile)> LoginUserAsync(string username, string password)
    {
        return await userService.LoginAsync(username, password);
    }

    public async Task<(bool Success, string Message, User? User)> RegisterUserAsync(string username, string password, string email, string playerName)
    {
        return await userService.RegisterAsync(username, password, email, playerName);
    }

    public async Task<PlayerProfile?> GetPlayerProfileAsync(int profileId)
    {
        return await profileService.GetProfileByIdAsync(profileId);
    }

    public async Task<PlayerProfile?> GetPlayerProfileByNameAsync(string playerName)
    {
        return await profileService.GetProfileByNameAsync(playerName);
    }

    public async Task<int> GetPlayerRankAsync(int profileId)
    {
        return await profileService.GetPlayerRankAsync(profileId);
    }

    public async Task<List<PlayerProfile>> GetLeaderboardAsync(int count)
    {
        return await profileService.GetTopPlayersByEloAsync(count);
    }

    public async Task<List<GameHistory>> GetPlayerGameHistoryAsync(int profileId, int pageSize)
    {
        return await gameHistoryService.GetPlayerGamesAsync(profileId, pageSize);
    }

    public async Task<List<PlayerProfile>> GetFriendsAsync(int profileId)
    {
        return await friendshipService.GetFriendsAsync(profileId);
    }

    public async Task<(bool Success, string Message, Friendship? Friendship)> SendFriendRequestAsync(int requesterId, int receiverId)
    {
        if (requesterId == receiverId)
        {
            return (false, "Cannot send friend request to yourself", null);
        }

        var friendship = await friendshipService.SendFriendRequestAsync(requesterId, receiverId);
        if (friendship != null)
        {
            return (true, "Friend request sent", friendship);
        }

        return (false, "Friend request already exists", null);
    }

    public async Task<bool> AcceptFriendRequestAsync(int friendshipId, int receiverId)
    {
        var friendship = await friendshipService.GetFriendshipByIdAsync(friendshipId);
        if (friendship == null || friendship.FriendId != receiverId || friendship.Status != "Pending")
        {
            return false;
        }

        return await friendshipService.AcceptFriendRequestAsync(friendshipId);
    }

    public async Task<GameHistory?> RecordGameResultAsync(
        int player1ProfileId,
        int? player2ProfileId,
        bool isAIGame,
        string gameResult,
        int? winnerProfileId,
        int totalMoves,
        TimeSpan gameDuration,
        string gameMode)
    {
        // For AI games
        if (isAIGame || !player2ProfileId.HasValue)
        {
            return await gameHistoryService.RecordGameAsync(
                player1ProfileId,
                player2ProfileId ?? 0,
                winnerProfileId,
                gameResult,
                totalMoves,
                (int)gameDuration.TotalSeconds,
                gameMode,
                0,
                0);
        }

        // For player vs player games - calculate ELO changes
        var player1 = await profileService.GetProfileByIdAsync(player1ProfileId);
        var player2 = await profileService.GetProfileByIdAsync(player2ProfileId.Value);

        if (player1 == null || player2 == null)
            return null;

        // Simple ELO calculation
        const int K = 32;
        double expectedPlayer1 = 1.0 / (1.0 + Math.Pow(10, (player2.Elo - player1.Elo) / 400.0));
        double expectedPlayer2 = 1.0 - expectedPlayer1;

        double actualPlayer1 = winnerProfileId == null ? 0.5 : (winnerProfileId == player1ProfileId ? 1.0 : 0.0);
        double actualPlayer2 = winnerProfileId == null ? 0.5 : (winnerProfileId == player2ProfileId ? 1.0 : 0.0);

        int player1EloChange = (int)Math.Round(K * (actualPlayer1 - expectedPlayer1));
        int player2EloChange = (int)Math.Round(K * (actualPlayer2 - expectedPlayer2));

        // Update player ELOs
        await profileService.UpdateEloAsync(player1ProfileId, player1.Elo + player1EloChange);
        await profileService.UpdateEloAsync(player2ProfileId.Value, player2.Elo + player2EloChange);

        // Update game stats
        await profileService.UpdateGameStatsAsync(player1ProfileId, winnerProfileId == player1ProfileId, winnerProfileId == null);
        await profileService.UpdateGameStatsAsync(player2ProfileId.Value, winnerProfileId == player2ProfileId, winnerProfileId == null);

        return await gameHistoryService.RecordGameAsync(
            player1ProfileId,
            player2ProfileId.Value,
            winnerProfileId,
            gameResult,
            totalMoves,
            (int)gameDuration.TotalSeconds,
            gameMode,
            player1EloChange,
            player2EloChange);
    }

    // ==================== Profile Update Wrapper Methods ====================

    public async Task<bool> UpdatePlayerNameAsync(int profileId, string newPlayerName)
    {
        return await profileService.UpdatePlayerNameAsync(profileId, newPlayerName);
    }

    public async Task<bool> UpdateAvatarUrlAsync(int profileId, string newAvatarUrl)
    {
        return await profileService.UpdateAvatarUrlAsync(profileId, newAvatarUrl);
    }

    public async Task<bool> UpdateBioAsync(int profileId, string newBio)
    {
        return await profileService.UpdateBioAsync(profileId, newBio);
    }

    public void Stop()
    {
        isRunning = false;
        tcpListener?.Stop();
        workerListener?.Stop();
        Console.WriteLine("Server stopped");
    }
}