namespace SharedLib.GameEngine;

public class GameLogic
{
    public const int BOARD_SIZE = 15;
    public const int WIN_CONDITION = 5;

    public static bool CheckWin(string[,] board, int row, int col, string player)
    {
        int[] directions = { -1, 0, 1 };

        foreach (int dr in directions)
        {
            foreach (int dc in directions)
            {
                if (dr == 0 && dc == 0) continue;

                int count = 1;

                // Check positive direction
                int r = row + dr, c = col + dc;
                while (r >= 0 && r < BOARD_SIZE && c >= 0 && c < BOARD_SIZE && board[r, c] == player)
                {
                    count++;
                    r += dr;
                    c += dc;
                }

                // Check negative direction
                r = row - dr;
                c = col - dc;
                while (r >= 0 && r < BOARD_SIZE && c >= 0 && c < BOARD_SIZE && board[r, c] == player)
                {
                    count++;
                    r -= dr;
                    c -= dc;
                }

                if (count >= WIN_CONDITION)
                    return true;
            }
        }

        return false;
    }

    public static bool IsBoardFull(string[,] board)
    {
        for (int i = 0; i < BOARD_SIZE; i++)
        {
            for (int j = 0; j < BOARD_SIZE; j++)
            {
                if (string.IsNullOrEmpty(board[i, j]))
                    return false;
            }
        }
        return true;
    }

    public static bool IsValidMove(string[,] board, int row, int col)
    {
        if (row < 0 || row >= BOARD_SIZE || col < 0 || col >= BOARD_SIZE)
            return false;

        return string.IsNullOrEmpty(board[row, col]);
    }

    public static string[,] CreateEmptyBoard()
    {
        var board = new string[BOARD_SIZE, BOARD_SIZE];
        for (int i = 0; i < BOARD_SIZE; i++)
        {
            for (int j = 0; j < BOARD_SIZE; j++)
            {
                board[i, j] = string.Empty;
            }
        }
        return board;
    }

    public static string[,] CopyBoard(string[,] board)
    {
        var newBoard = new string[BOARD_SIZE, BOARD_SIZE];
        for (int i = 0; i < BOARD_SIZE; i++)
        {
            for (int j = 0; j < BOARD_SIZE; j++)
            {
                newBoard[i, j] = board[i, j];
            }
        }
        return newBoard;
    }
}