﻿using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace TicTacToeServer;

public class TicTacToeServer
{
    private TcpListener tcpListener;
    private readonly int port;
    private bool isRunning = false;
    private readonly ConcurrentDictionary<string, ClientHandler> clients = new();
    private readonly MatchmakingService matchmakingService = new();
    
    // Worker management
    private readonly ConcurrentDictionary<string, WorkerInfo> workers = new();
    private readonly SemaphoreSlim workerLock = new(1, 1);
    private readonly int maxConcurrentRequests = 5; // Consider server overloaded if more than 5 concurrent requests
    private readonly int maxClients = 3; // Phân tán khi có nhiều hơn 3 clients
    private int currentConcurrentRequests = 0;

    public TicTacToeServer(int port = 5000)
    {
        this.port = port;
    }

    // 🔥 THÊM: Helper methods để convert arrays
    private static string[][] ConvertToJaggedArray(string[,] multiArray)
    {
        int rows = multiArray.GetLength(0);
        int cols = multiArray.GetLength(1);
        
        string[][] jaggedArray = new string[rows][];
        for (int r = 0; r < rows; r++)
        {
            jaggedArray[r] = new string[cols];
            for (int c = 0; c < cols; c++)
            {
                jaggedArray[r][c] = multiArray[r, c] ?? string.Empty;
            }
        }
        
        return jaggedArray;
    }

    public async Task StartAsync()
    {
        tcpListener = new TcpListener(IPAddress.Any, port);
        tcpListener.Start();
        isRunning = true;

        Console.WriteLine($"Tic-Tac-Toe Server started on port {port}");
      
        Console.WriteLine($"Server will distribute workload when having more than {maxClients} connected clients");

        // Start cleanup tasks
        _ = Task.Run(CleanupRoomsAsync);
        
        // Start worker status reporting
        // Start worker status reporting - GIÁ TRƯỚC MỖI 30s
        _ = Task.Run(ReportWorkerStatusAsync);

        while (isRunning)
        {
            try
            {
                var tcpClient = await tcpListener.AcceptTcpClientAsync();
                var clientHandler = new ClientHandler(tcpClient, this);
                clients[clientHandler.ClientId] = clientHandler;

                Console.WriteLine($"New client connected: {clientHandler.ClientId}");
                Console.WriteLine($"📊 Total connected clients: {clients.Count}");

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

    // Thêm method để report worker status
    // Thêm method để report worker status - GIẢM XUỐC 30s
    private async Task ReportWorkerStatusAsync()
    {
        while (isRunning)
        {
            await Task.Delay(TimeSpan.FromSeconds(30)); // Giảm từ 2 phút xuống 30s
            
            await workerLock.WaitAsync();
            try
            {
                Console.WriteLine("===== SERVER STATUS =====");
                Console.WriteLine($"Total registered workers: {workers.Count}");
                
                if (workers.Count == 0)
                {
                    Console.WriteLine("⚠️ NO WORKERS REGISTERED! Workers need to auto-register first.");
                }
                else
                {
                    foreach (var kvp in workers)
                    {
                        var worker = kvp.Value;
                        Console.WriteLine($"Worker {kvp.Key}: Role={worker.Role}, Load={worker.CurrentLoad}");
                    }
                }
                
                Console.WriteLine($"Current connected clients: {clients.Count}");
                Console.WriteLine($"Current concurrent requests: {currentConcurrentRequests}");
                
                // THÊM: Kiểm tra logic phân tán
                bool shouldDistribute = clients.Count > maxClients && workers.Values.Any(w => w.Role == "Logic");
                Console.WriteLine($"Should distribute work: {shouldDistribute} (clients: {clients.Count} > {maxClients} && has Logic worker: {workers.Values.Any(w => w.Role == "Logic")})");
                
                Console.WriteLine("========================");
            }
            finally
            {
                workerLock.Release();
            }
        }
    }

    public void RegisterWorker(string ip, int port, string role)
    {
        var workerId = $"{ip}:{port}";
        workers[workerId] = new WorkerInfo { Ip = ip, Port = port, Role = role, CurrentLoad = 0 };
        Console.WriteLine($"✅ Registered worker {workerId} with role {role}");
        Console.WriteLine($"Total workers: {workers.Count}");
        
        // THÊM: Logging để confirm registration
        Console.WriteLine($"🔍 Current worker list:");
        foreach (var kvp in workers)
        {
            Console.WriteLine($"   - {kvp.Key}: {kvp.Value.Role}");
        }
    }

    public Task<GameRoom> FindOrCreateRoom(ClientHandler client)
    {
        return matchmakingService.FindOrCreateRoom(client);
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

        Console.WriteLine($"🎮 Game started in room {room.RoomId}");
    }

    public async Task<bool> ProcessGameMove(GameRoom room, ClientHandler player, MoveData move)
    {
        Interlocked.Increment(ref currentConcurrentRequests);
        try
        {
            int connectedClients = clients.Count;
            Console.WriteLine($"🎯 Processing move - Connected clients: {connectedClients}/{maxClients}, Current requests: {currentConcurrentRequests}");
            Console.WriteLine($"🔍 Available workers: {workers.Count}, Logic workers: {workers.Values.Count(w => w.Role == "Logic")}");
            
            // Check if this is an AI move or if server is overloaded
            if (player == null) // AI move
            {
                var result = await ProcessAIMoveAsync(room);
                if (result != null)
                {
                    // Apply AI move
                    room.Board[result.Move.row, result.Move.col] = "O"; // Assuming AI is always "O"
                    room.LastActivity = DateTime.Now;

                    // Check for win
                    if (GameLogic.CheckWin(room.Board, result.Move.row, result.Move.col, "O"))
                    {
                        // AI won
                        await EndGame(room, room.Player2, "WIN");
                        return true;
                    }

                    // Check for draw
                    if (GameLogic.IsBoardFull(room.Board))
                    {
                        await EndGame(room, null, "DRAW");
                        return true;
                    }

                    // Switch turns
                    room.CurrentPlayer = "X";

                    var turnMessage = $"TURN_CHANGE:{{\"currentPlayer\":\"X\"}}";
                    if (room.Player1 != null)
                        await room.Player1.SendMessage(turnMessage);

                    // Send move notification to client
                    var moveMessage = $"GAME_MOVE:{{\"row\":{result.Move.row},\"col\":{result.Move.col}}}";
                    if (room.Player1 != null)
                        await room.Player1.SendMessage(moveMessage);

                    return true;
                }
                return false;
            }

            // 🔥 SỬA: Phân tán dựa trên số clients thay vì requests
            bool useWorker = connectedClients > maxClients && workers.Values.Any(w => w.Role == "Logic");
            
            Console.WriteLine($"🤔 Distribution decision: useWorker={useWorker} (clients {connectedClients} > {maxClients}: {connectedClients > maxClients}, has Logic worker: {workers.Values.Any(w => w.Role == "Logic")})");

            if (useWorker)
            {
                // Try to find a Logic worker
                var worker = await GetAvailableWorkerAsync("Logic");
                if (worker != null)
                {
                    Console.WriteLine($"🔄 [Client] -> [Main Server] -> [Worker {worker.Ip}:{worker.Port}] (Too many clients: {connectedClients}, delegating to worker)");
                    
                    // 🔥 SỬA: Convert board to jagged array trước khi gửi
                    var jaggedBoard = ConvertToJaggedArray(room.Board);
                    
                    // Send request to worker
                    var response = await AskWorkerAsync(new WorkerRequest
                    {
                        RequestType = "NORMAL_MOVE",
                        RequestId = Guid.NewGuid().ToString(),
                        GameId = room.RoomId,
                        PlayerSymbol = player.PlayerSymbol,
                        Board = jaggedBoard, // 🔥 Sử dụng jagged array
                        LastMove = move
                    }, worker.Ip, worker.Port);

                    if (response != null && response.IsSuccess)
                    {
                        Console.WriteLine($"✅ [Worker {worker.Ip}:{worker.Port}] -> [Main Server] -> [Client] (Move validated by worker)");
                        
                        // Apply move from worker response
                        room.Board[move.row, move.col] = player.PlayerSymbol;
                        room.LastActivity = DateTime.Now;

                        if (response.IsWinningMove)
                        {
                            await EndGame(room, player, "WIN");
                            return true;
                        }

                        // Check for draw
                        if (GameLogic.IsBoardFull(room.Board))
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
							
                        return true;
                    }
                    else
                    {
                        // Worker validation failed, send error to client
                        if (response != null)
                        {
                            await player.SendMessage($"ERROR:{response.ErrorMessage}");
                        }
                        else
                        {
                            await player.SendMessage("ERROR:Worker failed to process move");
                        }
                        return false;
                    }
                }
                // If no worker available, process locally
                Console.WriteLine("⚠️ No Logic worker available, processing move locally");
            }
            else
            {
                Console.WriteLine($"💻 Processing locally (clients: {connectedClients} <= {maxClients} threshold or no Logic worker available)");
            }

            // Process locally
            // Validate move
            if (move.row < 0 || move.row >= 15 || move.col < 0 || move.col >= 15)
            {
                await player.SendMessage("ERROR:Invalid move coordinates");
                return false;
            }

            if (!string.IsNullOrEmpty(room.Board[move.row, move.col]))
            {
                await player.SendMessage("ERROR:Cell already occupied");
                return false;
            }

            // Apply move
            room.Board[move.row, move.col] = player.PlayerSymbol;
            room.LastActivity = DateTime.Now;

            // Check for win condition
            if (GameLogic.CheckWin(room.Board, move.row, move.col, player.PlayerSymbol))
            {
                await EndGame(room, player, "WIN");
                return true;
            }

            // Check for draw
            if (GameLogic.IsBoardFull(room.Board))
            {
                await EndGame(room, null, "DRAW");
                return true;
            }

            // Switch turns
            room.CurrentPlayer = room.CurrentPlayer == "X" ? "O" : "X";

            var message = $"TURN_CHANGE:{{\"currentPlayer\":\"{room.CurrentPlayer}\"}}";
            if (room.Player1 != null)
                await room.Player1.SendMessage(message);
            if (room.Player2 != null)
                await room.Player2.SendMessage(message);

            return true;
        }
        finally
        {
            Interlocked.Decrement(ref currentConcurrentRequests);
        }
    }

    private async Task<WorkerResponse?> ProcessAIMoveAsync(GameRoom room)
    {
        // Find an AI worker
        var worker = await GetAvailableWorkerAsync("AI");
        if (worker != null)
        {
            Console.WriteLine($"🤖 [Client] -> [Main Server] -> [AI Worker {worker.Ip}:{worker.Port}] (Requesting AI move)");
            
            // 🔥 SỬA: Convert board to jagged array trước khi gửi
            var jaggedBoard = ConvertToJaggedArray(room.Board);
            
            var response = await AskWorkerAsync(new WorkerRequest
            {
                RequestType = "AI_MOVE",
                RequestId = Guid.NewGuid().ToString(),
                GameId = room.RoomId,
                Board = jaggedBoard // 🔥 Sử dụng jagged array
            }, worker.Ip, worker.Port);

            if (response != null && response.IsSuccess)
            {
                Console.WriteLine($"✅ [AI Worker {worker.Ip}:{worker.Port}] -> [Main Server] -> [Client] (AI move received)");
                return response;
            }
        }

        // If no AI worker or worker failed, use fallback logic (random move)
        Console.WriteLine("⚠️ No AI worker available, generating random move locally");
        return new WorkerResponse
        {
            RequestId = Guid.NewGuid().ToString(),
            ResponseType = "AI_MOVE_RESULT",
            IsSuccess = true,
            Move = GenerateRandomMove(room.Board)
        };
    }

    private MoveData GenerateRandomMove(string[,] board)
    {
        var random = new Random();
        int row, col;

        // Try to find an empty cell
        do
        {
            row = random.Next(0, 15);
            col = random.Next(0, 15);
        } while (!string.IsNullOrEmpty(board[row, col]));

        return new MoveData { row = row, col = col };
    }

    private async Task<WorkerInfo?> GetAvailableWorkerAsync(string role)
    {
        await workerLock.WaitAsync();
        try
        {
            var availableWorkers = workers.Values
                .Where(w => w.Role.Equals(role, StringComparison.OrdinalIgnoreCase))
                .OrderBy(w => w.CurrentLoad)
                .ToList();

            Console.WriteLine($"🔍 Looking for {role} workers: Found {availableWorkers.Count}");

            if (availableWorkers.Count == 0)
                return null;

            var worker = availableWorkers.First();
            worker.CurrentLoad++; // Increment load
            Console.WriteLine($"📋 Selected worker {worker.Ip}:{worker.Port} (load: {worker.CurrentLoad})");
            return worker;
        }
        finally
        {
            workerLock.Release();
        }
    }

    public async Task<WorkerResponse?> AskWorkerAsync(WorkerRequest request, string workerIp, int workerPort)
    {
        var workerId = $"{workerIp}:{workerPort}";
        try
        {
            Console.WriteLine($"📡 Sending request to worker {workerId}: {request.RequestType}");
            
            using var client = new TcpClient();
            var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(workerIp, workerPort, connectCts.Token);

            client.SendTimeout = 5000;
            client.ReceiveTimeout = 10000;

            using var stream = client.GetStream();

            // Send request (newline framed)
            string requestJson = JsonSerializer.Serialize(request);
            byte[] requestData = Encoding.UTF8.GetBytes(requestJson + "\n");
            await stream.WriteAsync(requestData);
            await stream.FlushAsync();

            // Read one line (ending with '\n')
            var responseBuilder = new StringBuilder();
            var buffer = new byte[4096];
            var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), readCts.Token);
                if (bytesRead <= 0) break;
                var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                responseBuilder.Append(chunk);
                var full = responseBuilder.ToString();
                var newlineIdx = full.IndexOf('\n');
                if (newlineIdx >= 0)
                {
                    var line = full.Substring(0, newlineIdx).Trim();
                    var response = JsonSerializer.Deserialize<WorkerResponse>(line);
                    Console.WriteLine($"📨 Received response from worker {workerId}: Success={response?.IsSuccess}");
                    return response;
                }
            }

            Console.WriteLine($"Worker {workerId} closed connection without newline-terminated response");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error communicating with worker {workerId}: {ex.Message}");
            
            // Remove worker if it's not responding
            await workerLock.WaitAsync();
            try
            {
                workers.TryRemove(workerId, out _);
                Console.WriteLine($"🗑️ Removed unresponsive worker {workerId}");
            }
            finally
            {
                workerLock.Release();
            }
            
            return null;
        }
        finally
        {
            // Update worker load decremented if worker is still registered
            await workerLock.WaitAsync();
            try
            {
                if (workers.TryGetValue(workerId, out var worker))
                {
                    worker.CurrentLoad = Math.Max(0, worker.CurrentLoad - 1);
                }
            }
            finally
            {
                workerLock.Release();
            }
        }
    }

    private async Task EndGame(GameRoom room, ClientHandler winner, string reason)
    {
        room.IsGameActive = false;

        string endMessage = $"GAME_END:{{\"reason\":\"{reason}\",\"winner\":\"{winner?.PlayerSymbol ?? "NONE"}\"}}";

        if (room.Player1 != null)
            await room.Player1.SendMessage(endMessage);
        if (room.Player2 != null)
            await room.Player2.SendMessage(endMessage);

        Console.WriteLine($"Game ended in room {room.RoomId}: {reason}");
    }

    public async Task LeaveRoom(ClientHandler client, GameRoom room)
    {
        matchmakingService.LeaveRoom(client, room);

        var opponent = room.GetOpponent(client);
        if (opponent != null)
        {
            await opponent.SendMessage("OPPONENT_LEFT:Your opponent left the game");
        }
    }

    public void RemoveClient(ClientHandler client)
    {
        clients.TryRemove(client.ClientId, out _);
        Console.WriteLine($"📉 Client disconnected: {client.ClientId}. Remaining clients: {clients.Count}");
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

    public void Stop()
    {
        isRunning = false;
        tcpListener?.Stop();
        Console.WriteLine("Server stopped");
    }
}
