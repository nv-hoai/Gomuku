using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace TictactoeWorker;

public class Program
{
    private const int DEFAULT_PORT = 6000;
    private const string DEFAULT_SERVER_IP = "localhost";
    private const int DEFAULT_SERVER_PORT = 5000;
    private static string _role = "Logic"; // Default role
    private static int _concurrentTasks = 0;
    private static readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingRequests = new();
    private static readonly ConcurrentDictionary<string, DateTime> _requestTimings = new();
    private static readonly ConcurrentQueue<double> _processingTimes = new();
    private static int _totalRequestsProcessed = 0;
    private static double _averageProcessingTime = 0;
    private static string _localIp = "localhost"; // Will be detected
    private static int _port = DEFAULT_PORT;
    private static string? _mainServerIp = null;
    private static int _mainServerPort = DEFAULT_SERVER_PORT;
    private static bool _autoRegister = false;

    static async Task Main(string[] args)
    {
        int port = DEFAULT_PORT;
        string role = "Logic"; // Default role: Logic

        // Parse command line arguments
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--port" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int parsedPort))
                {
                    port = parsedPort;
                    _port = port;
                }
            }
            else if (args[i] == "--role" && i + 1 < args.Length)
            {
                role = args[i + 1];
                _role = role;
            }
            else if (args[i] == "--server" && i + 1 < args.Length)
            {
                _mainServerIp = args[i + 1];
            }
            else if (args[i] == "--serverport" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int parsedPort))
                {
                    _mainServerPort = parsedPort;
                }
            }
            else if (args[i] == "--autoregister")
            {
                _autoRegister = true;
            }
            else if (args[i] == "--ip" && i + 1 < args.Length)
            {
                _localIp = args[i + 1];
            }
        }

        // Try to detect local IP if not specified
        if (_localIp == "localhost")
        {
            try
            {
                var hostName = Dns.GetHostName();
                var addresses = await Dns.GetHostAddressesAsync(hostName);
                var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                if (ipv4 != null)
                {
                    _localIp = ipv4.ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to detect local IP: {ex.Message}. Using localhost.");
            }
        }

        Console.WriteLine($"Starting Tic-tac-toe Worker Server with role: {role} on port: {port}, IP: {_localIp}");

        // Auto-register with main server if enabled
        if (_autoRegister && _mainServerIp != null)
        {
            Console.WriteLine($"Auto-registering with main server at {_mainServerIp}:{_mainServerPort}...");
            _ = Task.Run(() => RegisterWithMainServerAsync(_mainServerIp, _mainServerPort));
        }

        // Start periodic statistics reporting
        _ = Task.Run(ReportStatisticsPeriodicallyAsync);

        // Start TCP listener
        var tcpListener = new TcpListener(IPAddress.Any, port);
        tcpListener.Start();
        Console.WriteLine($"Worker listening on port {port} with role {role}");

        // Accept connections
        while (true)
        {
            try
            {
                var client = await tcpListener.AcceptTcpClientAsync();
                Console.WriteLine("Received connection from main server");

                // Handle client in background
                _ = Task.Run(() => HandleClientAsync(client));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting connection: {ex.Message}");
            }
        }
    }

    private static async Task RegisterWithMainServerAsync(string serverIp, int serverPort)
    {
        while (true)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(serverIp, serverPort);
                using var stream = client.GetStream();

                var request = new WorkerRequest
                {
                    RequestType = "REGISTER_WORKER",
                    RequestId = Guid.NewGuid().ToString(),
                    WorkerInfo = new WorkerInfo
                    {
                        Ip = _localIp,
                        Port = _port,
                        Role = _role,
                        CurrentLoad = _concurrentTasks
                    }
                };

                string requestJson = JsonSerializer.Serialize(request);
                byte[] requestData = Encoding.UTF8.GetBytes(requestJson + "\n");
                await stream.WriteAsync(requestData);
                
                Console.WriteLine($"Registered with main server at {serverIp}:{serverPort}");
                
                // Wait and re-register periodically to update stats
                await Task.Delay(TimeSpan.FromMinutes(5));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to register with main server: {ex.Message}");
                // Retry after delay
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
        }
    }

    private static async Task ReportStatisticsPeriodicallyAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromMinutes(1));
            
            Console.WriteLine("===== WORKER STATISTICS =====");
            Console.WriteLine($"Role: {_role}");
            Console.WriteLine($"Current load: {_concurrentTasks} concurrent tasks");
            Console.WriteLine($"Total requests processed: {_totalRequestsProcessed}");
            Console.WriteLine($"Average processing time: {_averageProcessingTime:F2}ms");
            Console.WriteLine("=============================");
        }
    }

    private static async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var buffer = new byte[4096];
                var messageBuilder = new StringBuilder();

                // Read data
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    messageBuilder.Append(message);

                    // Process complete messages
                    string fullMessage = messageBuilder.ToString();
                    if (fullMessage.EndsWith("\n"))
                    {
                        string[] messages = fullMessage.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var msg in messages)
                        {
                            if (!string.IsNullOrEmpty(msg))
                            {
                                Console.WriteLine($"Received from main server: {msg}");
                                var response = await ProcessMessageAsync(msg);
                                
                                // Send response back
                                var responseBytes = Encoding.UTF8.GetBytes(response + "\n");
                                await stream.WriteAsync(responseBytes);
                                Console.WriteLine($"Sent to main server: {response}");
                            }
                        }
                        messageBuilder.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
            }
        }
    }

    private static async Task<string> ProcessMessageAsync(string message)
    {
        var startTime = DateTime.UtcNow;
        var requestId = Guid.NewGuid().ToString();
        
        try
        {
            // Track concurrent tasks
            Interlocked.Increment(ref _concurrentTasks);
            Console.WriteLine($"Current load: {_concurrentTasks} concurrent tasks");

            WorkerRequest? request = null;
            try
            {
                request = JsonSerializer.Deserialize<WorkerRequest>(message);
                if (request != null)
                {
                    requestId = request.RequestId;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to deserialize request: {ex.Message}");
                return JsonSerializer.Serialize(new WorkerResponse
                {
                    RequestId = requestId,
                    ResponseType = "ERROR",
                    IsSuccess = false,
                    ErrorMessage = "Failed to parse request"
                });
            }

            if (request == null)
            {
                return JsonSerializer.Serialize(new WorkerResponse
                {
                    RequestId = requestId,
                    ResponseType = "ERROR",
                    IsSuccess = false,
                    ErrorMessage = "Null request"
                });
            }

            Console.WriteLine($"[Worker:{_role}] Processing {request.RequestType} request (ID: {request.RequestId})");

            // Process based on request type
            string response;
            switch (request.RequestType)
            {
                case "AI_MOVE":
                    response = await ProcessAIMoveAsync(request);
                    break;
                case "NORMAL_MOVE":
                    response = await ProcessNormalMoveAsync(request);
                    break;
                case "REGISTER_WORKER":
                    response = ProcessRegisterWorker(request);
                    break;
                default:
                    response = JsonSerializer.Serialize(new WorkerResponse
                    {
                        RequestId = request.RequestId,
                        ResponseType = "ERROR",
                        IsSuccess = false,
                        ErrorMessage = $"Unknown request type: {request.RequestType}"
                    });
                    break;
            }

            // Track timing statistics
            var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _processingTimes.Enqueue(elapsedMs);
            Interlocked.Increment(ref _totalRequestsProcessed);
            
            // Keep only the last 100 processing times for average
            if (_processingTimes.Count > 100)
            {
                _processingTimes.TryDequeue(out _);
            }
            
            // Update average
            _averageProcessingTime = _processingTimes.Any() ? _processingTimes.Average() : 0;
            
            return response;
        }
        finally
        {
            Interlocked.Decrement(ref _concurrentTasks);
        }
    }

    private static async Task<string> ProcessAIMoveAsync(WorkerRequest request)
    {
        if (_role != "AI")
        {
            Console.WriteLine($"Warning: Non-AI worker ({_role}) processing AI_MOVE request");
        }

        // Simulate AI processing time based on board complexity
        int filledCells = 0;
        if (request.Board != null)
        {
            for (int i = 0; i < 15; i++)
            {
                for (int j = 0; j < 15; j++)
                {
                    if (!string.IsNullOrEmpty(request.Board[i, j]))
                    {
                        filledCells++;
                    }
                }
            }
        }

        // Simulate more complex AI thinking as the board gets fuller
        int processingTime = 200 + (filledCells * 5);
        await Task.Delay(processingTime);

        Console.WriteLine($"[Worker:{_role}] Processing AI move for game {request.GameId} " +
                          $"(board has {filledCells} filled cells, took {processingTime}ms)");

        // In a real implementation, we would call the AI logic here
        // For now, just generate a random valid move
        var move = GenerateRandomMove(request.Board ?? new string[15, 15]);

        return JsonSerializer.Serialize(new WorkerResponse
        {
            RequestId = request.RequestId,
            ResponseType = "AI_MOVE_RESULT",
            IsSuccess = true,
            Move = move
        });
    }

    private static async Task<string> ProcessNormalMoveAsync(WorkerRequest request)
    {
        // Simulate processing time
        await Task.Delay(100);

        Console.WriteLine($"[Worker:{_role}] Validating move for game {request.GameId}");

        if (request.LastMove == null)
        {
            return JsonSerializer.Serialize(new WorkerResponse
            {
                RequestId = request.RequestId,
                ResponseType = "MOVE_VALIDATION_RESULT",
                IsSuccess = false,
                ErrorMessage = "No move data provided"
            });
        }

        if (request.Board == null)
        {
            return JsonSerializer.Serialize(new WorkerResponse
            {
                RequestId = request.RequestId,
                ResponseType = "MOVE_VALIDATION_RESULT",
                IsSuccess = false,
                ErrorMessage = "No board data provided"
            });
        }

        bool isValid = GameLogic.IsValidMove(request.Board, request.LastMove.row, request.LastMove.col);
        
        if (!isValid)
        {
            return JsonSerializer.Serialize(new WorkerResponse
            {
                RequestId = request.RequestId,
                ResponseType = "MOVE_VALIDATION_RESULT",
                IsSuccess = false,
                ErrorMessage = "Invalid move"
            });
        }

        // Apply the move to check for win condition
        var boardCopy = (string[,])request.Board.Clone();
        boardCopy[request.LastMove.row, request.LastMove.col] = request.PlayerSymbol ?? "X";

        bool isWinningMove = GameLogic.CheckWin(boardCopy, request.LastMove.row, request.LastMove.col, request.PlayerSymbol ?? "X");

        return JsonSerializer.Serialize(new WorkerResponse
        {
            RequestId = request.RequestId,
            ResponseType = "MOVE_VALIDATION_RESULT",
            IsSuccess = true,
            Move = request.LastMove,
            IsWinningMove = isWinningMove
        });
    }

    private static string ProcessRegisterWorker(WorkerRequest request)
    {
        // This is just for echo confirmation if needed
        return JsonSerializer.Serialize(new WorkerResponse
        {
            RequestId = request.RequestId,
            ResponseType = "WORKER_REGISTERED",
            IsSuccess = true
        });
    }

    private static MoveData GenerateRandomMove(string[,] board)
    {
        var random = new Random();
        int row, col;
        int attempts = 0;
        const int maxAttempts = 100; // Avoid infinite loop if board is full

        // Try to find an empty cell
        do
        {
            row = random.Next(0, 15);
            col = random.Next(0, 15);
            attempts++;

            if (attempts >= maxAttempts)
            {
                // If we can't find an empty cell after many attempts, 
                // board might be full or nearly full, just return any position
                return new MoveData { row = row, col = col };
            }
        } while (row < 15 && col < 15 && !string.IsNullOrEmpty(board[row, col]));

        return new MoveData { row = row, col = col };
    }
}
