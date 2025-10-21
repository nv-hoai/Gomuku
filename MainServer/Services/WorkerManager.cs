using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SharedLib.Communication;

namespace MainServer.Services;

public class WorkerManager
{
    private readonly List<WorkerNode> workerNodes = new();
    private readonly object lockObject = new object();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<WorkerResponse>> pendingRequests = new();

    public class WorkerNode
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public TcpClient? Connection { get; set; }
        public NetworkStream? Stream { get; set; }
        public bool IsConnected { get; set; } = false;
        public bool IsHealthy { get; set; } = true;
        public DateTime LastHealthCheck { get; set; } = DateTime.UtcNow;
        public int ActiveRequests { get; set; } = 0;
    }

    public async Task<bool> AddWorkerAsync(string host, int port)
    {
        var worker = new WorkerNode { Host = host, Port = port };
        
        if (await ConnectToWorkerAsync(worker))
        {
            lock (lockObject)
            {
                workerNodes.Add(worker);
            }
            
            // Start listening for responses from this worker
            _ = Task.Run(() => ListenForResponsesAsync(worker));
            
            Console.WriteLine($"Worker {worker.Id} added: {host}:{port}");
            return true;
        }
        
        Console.WriteLine($"Failed to connect to worker: {host}:{port}");
        return false;
    }

    private async Task<bool> ConnectToWorkerAsync(WorkerNode worker)
    {
        try
        {
            worker.Connection = new TcpClient();
            await worker.Connection.ConnectAsync(worker.Host, worker.Port);
            worker.Stream = worker.Connection.GetStream();
            worker.IsConnected = true;
            worker.IsHealthy = true; // Assume healthy initially
            
            Console.WriteLine($"TCP connection established to worker {worker.Host}:{worker.Port}");
            
            // Don't send health check immediately - let it settle first
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to worker {worker.Host}:{worker.Port}: {ex.Message}");
            worker.IsConnected = false;
            return false;
        }
    }

    public async Task<WorkerResponse?> SendAIRequestAsync(AIRequest aiRequest, int timeoutMs = 7000)
    {
        var worker = GetAvailableWorker();
        if (worker == null)
        {
            Console.WriteLine("No available workers for AI request");
            return null;
        }

        var request = new WorkerRequest
        {
            Type = WorkerProtocol.AI_MOVE_REQUEST,
            Data = JsonSerializer.Serialize(aiRequest)
        };

        return await SendRequestAsync(worker, request, timeoutMs);
    }

    public async Task<WorkerResponse?> SendMoveValidationRequestAsync(MoveValidationRequest validationRequest, int timeoutMs = 5000)
    {
        var worker = GetAvailableWorker();
        if (worker == null)
        {
            Console.WriteLine("No available workers for validation request");
            return null;
        }

        var request = new WorkerRequest
        {
            Type = WorkerProtocol.VALIDATE_MOVE_REQUEST,
            Data = JsonSerializer.Serialize(validationRequest)
        };

        return await SendRequestAsync(worker, request, timeoutMs);
    }

    private async Task<WorkerResponse?> SendRequestAsync(WorkerNode worker, WorkerRequest request, int timeoutMs)
    {
        if (worker.Stream == null || !worker.IsConnected)
        {
            await ReconnectWorkerAsync(worker);
            if (worker.Stream == null || !worker.IsConnected)
                return null;
        }

        try
        {
            // Create task completion source for this request
            var tcs = new TaskCompletionSource<WorkerResponse>();
            pendingRequests[request.RequestId] = tcs;

            // Increment active requests
            worker.ActiveRequests++;

            // Send request
            string json = JsonSerializer.Serialize(request);
            byte[] data = Encoding.UTF8.GetBytes(json + "\n");
            await worker.Stream!.WriteAsync(data, 0, data.Length);
            await worker.Stream.FlushAsync();

            Console.WriteLine($"Sent request {request.RequestId} to worker {worker.Id}");

            // Wait for response with timeout
            using var cts = new CancellationTokenSource(timeoutMs);
            var timeoutTask = Task.Delay(timeoutMs, cts.Token);
            var responseTask = tcs.Task;

            var completedTask = await Task.WhenAny(responseTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                Console.WriteLine($"Request {request.RequestId} timed out");
                pendingRequests.TryRemove(request.RequestId, out _);
                return null;
            }

            cts.Cancel(); // Cancel the timeout task
            return await responseTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending request to worker {worker.Id}: {ex.Message}");
            worker.IsConnected = false;
            return null;
        }
        finally
        {
            worker.ActiveRequests--;
        }
    }

    private async Task ListenForResponsesAsync(WorkerNode worker)
    {
        var buffer = new byte[4096];
        var messageBuilder = new StringBuilder();

        try
        {
            while (worker.IsConnected && worker.Stream != null)
            {
                int bytesRead = await worker.Stream.ReadAsync(buffer, 0, buffer.Length);
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
                        await ProcessWorkerResponse(message);
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
            Console.WriteLine($"Error listening to worker {worker.Id}: {ex.Message}");
        }
        finally
        {
            worker.IsConnected = false;
            Console.WriteLine($"Worker {worker.Id} disconnected");
        }
    }

    private async Task ProcessWorkerResponse(string message)
    {
        try
        {
            var response = JsonSerializer.Deserialize<WorkerResponse>(message);
            if (response != null && pendingRequests.TryRemove(response.RequestId, out var tcs))
            {
                tcs.SetResult(response);
                Console.WriteLine($"Received response for request {response.RequestId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing worker response: {ex.Message}");
        }
    }

    private WorkerNode? GetAvailableWorker()
    {
        lock (lockObject)
        {
            // Find worker with least active requests
            return workerNodes
                .Where(w => w.IsConnected && w.IsHealthy)
                .OrderBy(w => w.ActiveRequests)
                .FirstOrDefault();
        }
    }

    private async Task<bool> HealthCheckAsync(WorkerNode worker)
    {
        try
        {
            var request = new WorkerRequest
            {
                Type = WorkerProtocol.HEALTH_CHECK,
                Data = ""
            };

            var response = await SendRequestAsync(worker, request, 3000);
            worker.IsHealthy = response != null && response.Status == WorkerProtocol.SUCCESS;
            worker.LastHealthCheck = DateTime.UtcNow;
            
            return worker.IsHealthy;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Health check failed for worker {worker.Id}: {ex.Message}");
            worker.IsHealthy = false;
            return false;
        }
    }

    private async Task ReconnectWorkerAsync(WorkerNode worker)
    {
        try
        {
            worker.Connection?.Close();
            await ConnectToWorkerAsync(worker);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to reconnect worker {worker.Id}: {ex.Message}");
        }
    }

    public async Task StartHealthCheckTask()
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    var workers = workerNodes.ToList();
                    foreach (var worker in workers)
                    {
                        if (DateTime.UtcNow - worker.LastHealthCheck > TimeSpan.FromMinutes(1))
                        {
                            await HealthCheckAsync(worker);
                        }
                    }

                    await Task.Delay(30000); // Health check every 30 seconds
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Health check task error: {ex.Message}");
                }
            }
        });
    }

    public bool HasAvailableWorkers()
    {
        lock (lockObject)
        {
            return workerNodes.Any(w => w.IsConnected && w.IsHealthy);
        }
    }

    public void RemoveWorker(string workerId)
    {
        lock (lockObject)
        {
            var worker = workerNodes.FirstOrDefault(w => w.Id == workerId);
            if (worker != null)
            {
                worker.Connection?.Close();
                workerNodes.Remove(worker);
                Console.WriteLine($"Worker {workerId} removed");
            }
        }
    }
}