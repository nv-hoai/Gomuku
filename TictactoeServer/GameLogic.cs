namespace TicTacToeServer;

public class GameLogic
{
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
                while (r >= 0 && r < 15 && c >= 0 && c < 15 && board[r, c] == player)
                {
                    count++;
                    r += dr;
                    c += dc;
                }

                // Check negative direction
                r = row - dr;
                c = col - dc;
                while (r >= 0 && r < 15 && c >= 0 && c < 15 && board[r, c] == player)
                {
                    count++;
                    r -= dr;
                    c -= dc;
                }

                if (count >= 5)
                    return true;
            }
        }

        return false;
    }

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
}
