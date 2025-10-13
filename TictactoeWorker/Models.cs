using System.Text.Json.Serialization;

namespace TictactoeWorker;

// Models shared between Server and Worker
public class MoveData
{
    public int row { get; set; }
    public int col { get; set; }
}

public class PlayerInfo
{
    public string playerName { get; set; } = string.Empty;
    public string avatarUrl { get; set; } = string.Empty;
}

public class WorkerInfo
{
    public string Ip { get; set; } = "localhost";
    public int Port { get; set; }
    public string Role { get; set; } = "Logic"; // "AI" or "Logic"
    public int CurrentLoad { get; set; } = 0;
}

public class WorkerRequest
{
    public string RequestType { get; set; } = string.Empty; // AI_MOVE, NORMAL_MOVE, REGISTER_WORKER
    public string RequestId { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string PlayerSymbol { get; set; } = string.Empty;
    
    // 🔥 SỬA: Đổi từ string[,] sang string[][] (jagged array)
    public string[][]? Board { get; set; } = null;
    
    public MoveData LastMove { get; set; } = new MoveData();
    public WorkerInfo WorkerInfo { get; set; } = new WorkerInfo();
}

public class WorkerResponse
{
    public string RequestId { get; set; } = string.Empty;
    public string ResponseType { get; set; } = string.Empty; // AI_MOVE_RESULT, MOVE_VALIDATION_RESULT
    public bool IsSuccess { get; set; } = true;
    public string ErrorMessage { get; set; } = string.Empty;
    public MoveData Move { get; set; } = new MoveData();
    public bool IsWinningMove { get; set; } = false;
}

// Utility class for game logic
public static class GameLogic
{
    // 🔥 SỬA: Overload methods để hỗ trợ cả string[,] và string[]
    public static bool CheckWin(string[,] board, int row, int col, string symbol)
    {
        return CheckWinInternal(board, row, col, symbol);
    }
    
    public static bool CheckWin(string[][] board, int row, int col, string symbol)
    {
        return CheckWinInternal(board, row, col, symbol);
    }

    private static bool CheckWinInternal<T>(T board, int row, int col, string symbol) where T : class
    {
        // Helper function to get cell value regardless of array type
        string GetCell(int r, int c)
        {
            if (board is string[,] multiArray)
                return multiArray[r, c] ?? string.Empty;
            else if (board is string[][] jaggedArray && r < jaggedArray.Length && c < jaggedArray[r].Length)
                return jaggedArray[r][c] ?? string.Empty;
            return string.Empty;
        }

        // Check directions for win condition (5 in a row)
        // Horizontal check
        if (CheckDirection(GetCell, row, col, 0, 1, symbol) + CheckDirection(GetCell, row, col, 0, -1, symbol) >= 4)
            return true;
        
        // Vertical check
        if (CheckDirection(GetCell, row, col, 1, 0, symbol) + CheckDirection(GetCell, row, col, -1, 0, symbol) >= 4)
            return true;
        
        // Diagonal (top-left to bottom-right)
        if (CheckDirection(GetCell, row, col, 1, 1, symbol) + CheckDirection(GetCell, row, col, -1, -1, symbol) >= 4)
            return true;
        
        // Diagonal (top-right to bottom-left)
        if (CheckDirection(GetCell, row, col, 1, -1, symbol) + CheckDirection(GetCell, row, col, -1, 1, symbol) >= 4)
            return true;
        
        return false;
    }

    private static int CheckDirection(Func<int, int, string> getCell, int startRow, int startCol, int rowDir, int colDir, string symbol)
    {
        int count = 0;
        int row = startRow + rowDir;
        int col = startCol + colDir;
        
        while (row >= 0 && row < 15 && col >= 0 && col < 15 && getCell(row, col) == symbol)
        {
            count++;
            row += rowDir;
            col += colDir;
        }
        
        return count;
    }

    // 🔥 SỬA: Overload cho IsBoardFull
    public static bool IsBoardFull(string[,] board)
    {
        for (int i = 0; i < 15; i++)
        {
            for (int j = 0; j < 15; j++)
            {
                if (string.IsNullOrEmpty(board[i, j]))
                    return false;
            }
        }
        return true;
    }
    
    public static bool IsBoardFull(string[][] board)
    {
        for (int i = 0; i < Math.Min(15, board.Length); i++)
        {
            for (int j = 0; j < Math.Min(15, board[i]?.Length ?? 0); j++)
            {
                if (string.IsNullOrEmpty(board[i][j]))
                    return false;
            }
        }
        return true;
    }

    // 🔥 SỬA: Overload cho IsValidMove
    public static bool IsValidMove(string[,] board, int row, int col)
    {
        if (row < 0 || row >= 15 || col < 0 || col >= 15)
            return false;

        return string.IsNullOrEmpty(board[row, col]);
    }
    
    public static bool IsValidMove(string[][] board, int row, int col)
    {
        if (row < 0 || row >= 15 || col < 0 || col >= 15)
            return false;
        
        if (row >= board.Length || col >= (board[row]?.Length ?? 0))
            return false;

        return string.IsNullOrEmpty(board[row][col]);
    }
}