using System;
using System.Collections.Generic;

namespace TictactoeWorker;

/// <summary>
/// Utility methods for game operations
/// </summary>
public static class GameUtils
{
    /// <summary>
    /// Generate a random valid move on the board (multidimensional array)
    /// </summary>
    /// <param name="board">The current game board</param>
    /// <returns>A valid MoveData</returns>
    public static MoveData GenerateRandomMove(string[,] board)
    {
        // Fast scan for empty cells
        var empties = new List<(int r, int c)>(225);
        for (int r = 0; r < 15; r++)
        {
            for (int c = 0; c < 15; c++)
            {
                if (string.IsNullOrEmpty(board[r, c]))
                {
                    empties.Add((r, c));
                }
            }
        }

        // If no empty cells, return an arbitrary position
        if (empties.Count == 0)
        {
            return new MoveData { row = 0, col = 0 };
        }

        // Pick a random empty cell
        var pick = Random.Shared.Next(empties.Count);
        var (row, col) = empties[pick];
        
        return new MoveData { row = row, col = col };
    }

    /// <summary>
    /// Generate a random valid move on the board (jagged array)
    /// </summary>
    /// <param name="board">The current game board</param>
    /// <returns>A valid MoveData</returns>
    public static MoveData GenerateRandomMove(string[][]? board)
    {
        if (board == null)
        {
            return new MoveData { row = 0, col = 0 };
        }

        // Fast scan for empty cells
        var empties = new List<(int r, int c)>(225);
        for (int r = 0; r < Math.Min(15, board.Length); r++)
        {
            for (int c = 0; c < Math.Min(15, board[r]?.Length ?? 0); c++)
            {
                if (string.IsNullOrEmpty(board[r][c]))
                {
                    empties.Add((r, c));
                }
            }
        }

        // If no empty cells, return an arbitrary position
        if (empties.Count == 0)
        {
            return new MoveData { row = 0, col = 0 };
        }

        // Pick a random empty cell
        var pick = Random.Shared.Next(empties.Count);
        var (row, col) = empties[pick];
        
        return new MoveData { row = row, col = col };
    }

    /// <summary>
    /// Convert string[,] to string[][] for JSON serialization
    /// </summary>
    public static string[][] ConvertToJaggedArray(string[,] multiArray)
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

    /// <summary>
    /// Convert string[][] to string[,] for internal processing
    /// </summary>
    public static string[,] ConvertToMultiArray(string[][]? jaggedArray)
    {
        if (jaggedArray == null)
        {
            var emptyBoard = new string[15, 15];
            for (int i = 0; i < 15; i++)
                for (int j = 0; j < 15; j++)
                    emptyBoard[i, j] = string.Empty;
            return emptyBoard;
        }

        string[,] multiArray = new string[15, 15];
        for (int r = 0; r < Math.Min(15, jaggedArray.Length); r++)
        {
            for (int c = 0; c < Math.Min(15, jaggedArray[r]?.Length ?? 0); c++)
            {
                multiArray[r, c] = jaggedArray[r][c] ?? string.Empty;
            }
        }
        
        // Fill remaining cells with empty strings
        for (int r = 0; r < 15; r++)
        {
            for (int c = 0; c < 15; c++)
            {
                if (multiArray[r, c] == null)
                    multiArray[r, c] = string.Empty;
            }
        }
        
        return multiArray;
    }
}