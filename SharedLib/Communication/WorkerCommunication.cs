namespace SharedLib.Communication;

// Protocol constants
public static class WorkerProtocol
{
    // Request types
    public const string AI_MOVE_REQUEST = "AI_MOVE_REQUEST";
    public const string VALIDATE_MOVE_REQUEST = "VALIDATE_MOVE_REQUEST";
    public const string HEALTH_CHECK = "HEALTH_CHECK";
    public const string WORKER_REGISTRATION = "WORKER_REGISTRATION";
    public const string PING = "PING";

    // Response types
    public const string AI_MOVE_RESPONSE = "AI_MOVE_RESPONSE";
    public const string VALIDATE_MOVE_RESPONSE = "VALIDATE_MOVE_RESPONSE";
    public const string HEALTH_CHECK_RESPONSE = "HEALTH_CHECK_RESPONSE";
    public const string WORKER_REGISTRATION_ACK = "WORKER_REGISTRATION_ACK";
    public const string PONG = "PONG";
    public const string ERROR_RESPONSE = "ERROR_RESPONSE";

    // Status
    public const string SUCCESS = "SUCCESS";
    public const string ERROR = "ERROR";
}

// Base request/response classes
public class WorkerRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class WorkerResponse
{
    public string RequestId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// Specific request/response data classes
public class AIRequest
{
    public string[][] Board { get; set; } = new string[15][];
    public string AISymbol { get; set; } = string.Empty;
    public string RoomId { get; set; } = string.Empty;

    public AIRequest()
    {
        // Initialize jagged array
        Board = new string[15][];
        for (int i = 0; i < 15; i++)
        {
            Board[i] = new string[15];
            for (int j = 0; j < 15; j++)
            {
                Board[i][j] = string.Empty;
            }
        }
    }
}

public class AIResponse
{
    public int Row { get; set; }
    public int Col { get; set; }
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class MoveValidationRequest
{
    public string[][] Board { get; set; } = new string[15][];
    public int Row { get; set; }
    public int Col { get; set; }
    public string PlayerSymbol { get; set; } = string.Empty;

    public MoveValidationRequest()
    {
        // Initialize jagged array
        Board = new string[15][];
        for (int i = 0; i < 15; i++)
        {
            Board[i] = new string[15];
            for (int j = 0; j < 15; j++)
            {
                Board[i][j] = string.Empty;
            }
        }
    }
}

public class MoveValidationResponse
{
    public bool IsValid { get; set; }
    public bool IsWinning { get; set; }
    public bool IsDraw { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}