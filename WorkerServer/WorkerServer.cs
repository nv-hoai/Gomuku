using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SharedLib.Communication;
using SharedLib.AI;
using SharedLib.GameEngine;

namespace WorkerServer;

public class WorkerServer
{
    private TcpListener? tcpListener;
    private readonly int port;
    private bool isRunning = false;
    private readonly Dictionary<string, TcpClient> connectedClients = new();

    public WorkerServer(int port = 6000)
    {
        this.port = port;
    }

    public async Task StartAsync()
    {
        tcpListener = new TcpListener(IPAddress.Any, port);
        tcpListener.Start();
        isRunning = true;

        Console.WriteLine($"Worker Server started on port {port}");

        while (isRunning)
        {
            try
            {
                var tcpClient = await tcpListener.AcceptTcpClientAsync();
                Console.WriteLine($"MainServer connected: {tcpClient.Client.RemoteEndPoint}");

                // Handle MainServer connection in background
                _ = Task.Run(() => HandleMainServerAsync(tcpClient));
            }
            catch (Exception ex)
            {
                if (isRunning)
                {
                    Console.WriteLine($"Error accepting connection: {ex.Message}");
                }
            }
        }
    }

    private async Task HandleMainServerAsync(TcpClient tcpClient)
    {
        var stream = tcpClient.GetStream();
        var buffer = new byte[4096];
        var messageBuilder = new StringBuilder();

        try
        {
            while (isRunning && tcpClient.Connected)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
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
                        await ProcessRequest(message, stream);
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
            Console.WriteLine($"MainServer connection error: {ex.Message}");
        }
        finally
        {
            stream?.Close();
            tcpClient?.Close();
            Console.WriteLine("MainServer disconnected");
        }
    }

    private async Task ProcessRequest(string message, NetworkStream stream)
    {
        Console.WriteLine($"Received: {message}");

        try
        {
            var request = JsonSerializer.Deserialize<WorkerRequest>(message);
            if (request == null)
            {
                await SendErrorResponse(stream, "", "Invalid request format");
                return;
            }

            WorkerResponse response;

            switch (request.Type)
            {
                case WorkerProtocol.AI_MOVE_REQUEST:
                    response = await ProcessAIMoveRequest(request);
                    break;

                case WorkerProtocol.VALIDATE_MOVE_REQUEST:
                    response = await ProcessMoveValidationRequest(request);
                    break;

                case WorkerProtocol.HEALTH_CHECK:
                    response = new WorkerResponse
                    {
                        RequestId = request.RequestId,
                        Type = WorkerProtocol.HEALTH_CHECK_RESPONSE,
                        Status = WorkerProtocol.SUCCESS,
                        Data = "Worker is healthy"
                    };
                    break;

                default:
                    response = new WorkerResponse
                    {
                        RequestId = request.RequestId,
                        Type = WorkerProtocol.ERROR_RESPONSE,
                        Status = WorkerProtocol.ERROR,
                        ErrorMessage = $"Unknown request type: {request.Type}"
                    };
                    break;
            }

            await SendResponse(stream, response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing request: {ex.Message}");
            await SendErrorResponse(stream, "", $"Processing error: {ex.Message}");
        }
    }

    private async Task<WorkerResponse> ProcessAIMoveRequest(WorkerRequest request)
    {
        var startTime = DateTime.Now;
        try
        {
            Console.WriteLine($"[Worker] Processing AI request {request.RequestId}");
            
            var aiRequest = JsonSerializer.Deserialize<AIRequest>(request.Data);
            if (aiRequest == null)
            {
                return CreateErrorResponse(request.RequestId, "Invalid AI request data");
            }

            // Convert jagged array to 2D array
            var board2D = ConvertToRectangularArray(aiRequest.Board);
            
            Console.WriteLine($"[Worker] Starting AI calculation for {aiRequest.AISymbol}");
            var gomokuAI = new GomokuAI(aiRequest.AISymbol);
            var (row, col) = gomokuAI.GetBestMove(board2D);
            
            var elapsed = DateTime.Now - startTime;
            Console.WriteLine($"[Worker] AI calculation completed in {elapsed.TotalMilliseconds}ms");

            var aiResponse = new AIResponse
            {
                Row = row,
                Col = col,
                IsValid = row != -1 && col != -1
            };

            if (!aiResponse.IsValid)
            {
                aiResponse.ErrorMessage = "No valid moves available";
            }

            return new WorkerResponse
            {
                RequestId = request.RequestId,
                Type = WorkerProtocol.AI_MOVE_RESPONSE,
                Status = WorkerProtocol.SUCCESS,
                Data = JsonSerializer.Serialize(aiResponse)
            };
        }
        catch (Exception ex)
        {
            var elapsed = DateTime.Now - startTime;
            Console.WriteLine($"[Worker] AI request {request.RequestId} failed after {elapsed.TotalMilliseconds}ms: {ex.Message}");
            return CreateErrorResponse(request.RequestId, $"AI processing error: {ex.Message}");
        }
    }

    private async Task<WorkerResponse> ProcessMoveValidationRequest(WorkerRequest request)
    {
        try
        {
            var validationRequest = JsonSerializer.Deserialize<MoveValidationRequest>(request.Data);
            if (validationRequest == null)
            {
                return CreateErrorResponse(request.RequestId, "Invalid validation request data");
            }

            // Convert jagged array to 2D array
            var board2D = ConvertToRectangularArray(validationRequest.Board);

            var validationResponse = new MoveValidationResponse
            {
                IsValid = GameLogic.IsValidMove(board2D, validationRequest.Row, validationRequest.Col)
            };

            if (validationResponse.IsValid)
            {
                // Create a copy and apply the move to check win condition
                var tempBoard = GameLogic.CopyBoard(board2D);
                tempBoard[validationRequest.Row, validationRequest.Col] = validationRequest.PlayerSymbol;

                validationResponse.IsWinning = GameLogic.CheckWin(tempBoard, validationRequest.Row, validationRequest.Col, validationRequest.PlayerSymbol);
                validationResponse.IsDraw = !validationResponse.IsWinning && GameLogic.IsBoardFull(tempBoard);
            }
            else
            {
                validationResponse.ErrorMessage = "Invalid move: position is occupied or out of bounds";
            }

            return new WorkerResponse
            {
                RequestId = request.RequestId,
                Type = WorkerProtocol.VALIDATE_MOVE_RESPONSE,
                Status = WorkerProtocol.SUCCESS,
                Data = JsonSerializer.Serialize(validationResponse)
            };
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(request.RequestId, $"Validation error: {ex.Message}");
        }
    }

    private WorkerResponse CreateErrorResponse(string requestId, string errorMessage)
    {
        return new WorkerResponse
        {
            RequestId = requestId,
            Type = WorkerProtocol.ERROR_RESPONSE,
            Status = WorkerProtocol.ERROR,
            ErrorMessage = errorMessage
        };
    }

    private async Task SendResponse(NetworkStream stream, WorkerResponse response)
    {
        try
        {
            string json = JsonSerializer.Serialize(response);
            byte[] data = Encoding.UTF8.GetBytes(json + "\n");
            await stream.WriteAsync(data, 0, data.Length);
            await stream.FlushAsync();
            Console.WriteLine($"Sent response: {response.Type} - {response.Status}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send response: {ex.Message}");
        }
    }

    private async Task SendErrorResponse(NetworkStream stream, string requestId, string errorMessage)
    {
        var errorResponse = CreateErrorResponse(requestId, errorMessage);
        await SendResponse(stream, errorResponse);
    }

    private string[,] ConvertToRectangularArray(string[][] jaggedArray)
    {
        var result = new string[15, 15];
        for (int i = 0; i < 15; i++)
        {
            for (int j = 0; j < 15; j++)
            {
                result[i, j] = jaggedArray[i][j] ?? string.Empty;
            }
        }
        return result;
    }

    public void Stop()
    {
        isRunning = false;
        tcpListener?.Stop();
        Console.WriteLine("Worker Server stopped");
    }
}