using System.Diagnostics;

namespace MainServer.Services;

public class LoadBalancer
{
    private readonly PerformanceCounter? cpuCounter;
    private readonly PerformanceCounter? ramCounter;
    private int currentLoad = 0;
    private readonly object loadLock = new();

    // Load thresholds
    private const int HIGH_LOAD_THRESHOLD = 80; // Percentage
    private const int MEDIUM_LOAD_THRESHOLD = 50;
    private const int MAX_CONCURRENT_GAMES = 100;

    public LoadBalancer()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Performance counters not available: {ex.Message}");
        }
    }

    public LoadLevel GetCurrentLoadLevel()
    {
        var systemLoad = GetSystemLoadPercentage();
        var gameLoad = GetGameLoadPercentage();
        
        var overallLoad = Math.Max(systemLoad, gameLoad);

        if (overallLoad >= HIGH_LOAD_THRESHOLD)
            return LoadLevel.High;
        else if (overallLoad >= MEDIUM_LOAD_THRESHOLD)
            return LoadLevel.Medium;
        else
            return LoadLevel.Low;
    }

    public bool ShouldUseWorker(OperationType operationType)
    {
        var loadLevel = GetCurrentLoadLevel();
        
        return operationType switch
        {
            OperationType.AIMove => loadLevel >= LoadLevel.Medium, // Use worker for AI at medium load
            OperationType.MoveValidation => loadLevel >= LoadLevel.High, // Use worker for validation only at high load
            OperationType.GameLogic => loadLevel >= LoadLevel.High,
            _ => false
        };
    }

    private int GetSystemLoadPercentage()
    {
        try
        {
            if (cpuCounter != null && OperatingSystem.IsWindows())
            {
                var cpuUsage = cpuCounter.NextValue();
                
                // Wait a bit for more accurate reading
                Thread.Sleep(100);
                cpuUsage = cpuCounter.NextValue();
                
                return (int)Math.Min(cpuUsage, 100);
            }
            
            // Fallback method for non-Windows or when performance counters not available
            return GetCpuUsageFallback();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting system load: {ex.Message}");
            return 0;
        }
    }

    private int GetCpuUsageFallback()
    {
        // Simple approximation based on process count and memory usage
        var process = Process.GetCurrentProcess();
        var totalMemory = GC.GetTotalMemory(false);
        var workingSet = process.WorkingSet64;

        // Rough estimation (this is very basic)
        var memoryPressure = (int)((totalMemory / (1024.0 * 1024.0)) / 100.0); // MB to percentage approximation
        
        return Math.Min(memoryPressure, 100);
    }

    private int GetGameLoadPercentage()
    {
        lock (loadLock)
        {
            return (int)((double)currentLoad / MAX_CONCURRENT_GAMES * 100);
        }
    }

    public void IncrementLoad()
    {
        lock (loadLock)
        {
            currentLoad++;
        }
    }

    public void DecrementLoad()
    {
        lock (loadLock)
        {
            if (currentLoad > 0)
                currentLoad--;
        }
    }

    public LoadStats GetLoadStats()
    {
        return new LoadStats
        {
            SystemLoad = GetSystemLoadPercentage(),
            GameLoad = GetGameLoadPercentage(),
            CurrentGames = currentLoad,
            LoadLevel = GetCurrentLoadLevel(),
            Timestamp = DateTime.UtcNow
        };
    }
}

public enum LoadLevel
{
    Low,
    Medium,
    High
}

public enum OperationType
{
    AIMove,
    MoveValidation,
    GameLogic,
    Matchmaking
}

public class LoadStats
{
    public int SystemLoad { get; set; }
    public int GameLoad { get; set; }
    public int CurrentGames { get; set; }
    public LoadLevel LoadLevel { get; set; }
    public DateTime Timestamp { get; set; }
}