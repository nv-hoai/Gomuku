using SharedLib.GameEngine;

namespace SharedLib.AI;

public class GomokuAI
{
    private const int MAX_DEPTH = 4;
    private const int INFINITY = 999999;
    
    private readonly string aiSymbol;
    private readonly string humanSymbol;

    public GomokuAI(string aiSymbol)
    {
        this.aiSymbol = aiSymbol;
        this.humanSymbol = aiSymbol == "X" ? "O" : "X";
    }

    public (int row, int col) GetBestMove(string[,] board)
    {
        int bestValue = -INFINITY;
        int bestRow = -1;
        int bestCol = -1;

        var moves = GetPossibleMoves(board);
        
        // If it's the first move, play in the center
        if (moves.Count == GameLogic.BOARD_SIZE * GameLogic.BOARD_SIZE)
        {
            return (GameLogic.BOARD_SIZE / 2, GameLogic.BOARD_SIZE / 2);
        }

        foreach (var (row, col) in moves)
        {
            var newBoard = GameLogic.CopyBoard(board);
            newBoard[row, col] = aiSymbol;

            int moveValue = Minimax(newBoard, MAX_DEPTH - 1, false, -INFINITY, INFINITY);

            if (moveValue > bestValue)
            {
                bestValue = moveValue;
                bestRow = row;
                bestCol = col;
            }
        }

        return bestRow != -1 ? (bestRow, bestCol) : (-1, -1);
    }

    private int Minimax(string[,] board, int depth, bool isMaximizing, int alpha, int beta)
    {
        int score = EvaluateBoard(board);
        
        // Terminal conditions
        if (Math.Abs(score) >= 10000 || depth == 0 || GameLogic.IsBoardFull(board))
        {
            return score;
        }

        var moves = GetPossibleMoves(board);
        
        if (isMaximizing)
        {
            int maxEval = -INFINITY;
            foreach (var (row, col) in moves)
            {
                var newBoard = GameLogic.CopyBoard(board);
                newBoard[row, col] = aiSymbol;
                
                int eval = Minimax(newBoard, depth - 1, false, alpha, beta);
                maxEval = Math.Max(maxEval, eval);
                alpha = Math.Max(alpha, eval);
                
                if (beta <= alpha)
                    break; // Alpha-beta pruning
            }
            return maxEval;
        }
        else
        {
            int minEval = INFINITY;
            foreach (var (row, col) in moves)
            {
                var newBoard = GameLogic.CopyBoard(board);
                newBoard[row, col] = humanSymbol;
                
                int eval = Minimax(newBoard, depth - 1, true, alpha, beta);
                minEval = Math.Min(minEval, eval);
                beta = Math.Min(beta, eval);
                
                if (beta <= alpha)
                    break; // Alpha-beta pruning
            }
            return minEval;
        }
    }

    private List<(int row, int col)> GetPossibleMoves(string[,] board)
    {
        var moves = new List<(int, int)>();
        var occupied = new HashSet<(int, int)>();

        // Find all occupied positions
        for (int i = 0; i < GameLogic.BOARD_SIZE; i++)
        {
            for (int j = 0; j < GameLogic.BOARD_SIZE; j++)
            {
                if (!string.IsNullOrEmpty(board[i, j]))
                {
                    occupied.Add((i, j));
                }
            }
        }

        // If no moves yet, return center
        if (occupied.Count == 0)
        {
            moves.Add((GameLogic.BOARD_SIZE / 2, GameLogic.BOARD_SIZE / 2));
            return moves;
        }

        // Get moves within 2 squares of existing pieces
        var candidates = new HashSet<(int, int)>();
        foreach (var (row, col) in occupied)
        {
            for (int dr = -2; dr <= 2; dr++)
            {
                for (int dc = -2; dc <= 2; dc++)
                {
                    int newRow = row + dr;
                    int newCol = col + dc;
                    
                    if (GameLogic.IsValidMove(board, newRow, newCol) && !occupied.Contains((newRow, newCol)))
                    {
                        candidates.Add((newRow, newCol));
                    }
                }
            }
        }

        return candidates.ToList();
    }

    private int EvaluateBoard(string[,] board)
    {
        int aiScore = 0;
        int humanScore = 0;

        // Check all directions for patterns
        for (int i = 0; i < GameLogic.BOARD_SIZE; i++)
        {
            for (int j = 0; j < GameLogic.BOARD_SIZE; j++)
            {
                if (!string.IsNullOrEmpty(board[i, j]))
                {
                    if (board[i, j] == aiSymbol)
                    {
                        aiScore += EvaluatePosition(board, i, j, aiSymbol);
                    }
                    else
                    {
                        humanScore += EvaluatePosition(board, i, j, humanSymbol);
                    }
                }
            }
        }

        return aiScore - humanScore;
    }

    private int EvaluatePosition(string[,] board, int row, int col, string player)
    {
        int score = 0;
        int[] directions = { 1, 0, 1, 1, 0, 1, -1, 1 }; // horizontal, vertical, diagonal1, diagonal2

        for (int d = 0; d < 8; d += 2)
        {
            int dr = directions[d];
            int dc = directions[d + 1];
            
            score += EvaluateLine(board, row, col, dr, dc, player);
        }

        return score;
    }

    private int EvaluateLine(string[,] board, int row, int col, int dr, int dc, string player)
    {
        int count = 1; // Current piece
        int openEnds = 0;

        // Check positive direction
        int r = row + dr, c = col + dc;
        while (r >= 0 && r < GameLogic.BOARD_SIZE && c >= 0 && c < GameLogic.BOARD_SIZE && board[r, c] == player)
        {
            count++;
            r += dr;
            c += dc;
        }
        if (r >= 0 && r < GameLogic.BOARD_SIZE && c >= 0 && c < GameLogic.BOARD_SIZE && string.IsNullOrEmpty(board[r, c]))
        {
            openEnds++;
        }

        // Check negative direction  
        r = row - dr;
        c = col - dc;
        while (r >= 0 && r < GameLogic.BOARD_SIZE && c >= 0 && c < GameLogic.BOARD_SIZE && board[r, c] == player)
        {
            count++;
            r -= dr;
            c -= dc;
        }
        if (r >= 0 && r < GameLogic.BOARD_SIZE && c >= 0 && c < GameLogic.BOARD_SIZE && string.IsNullOrEmpty(board[r, c]))
        {
            openEnds++;
        }

        return GetPatternScore(count, openEnds);
    }

    private int GetPatternScore(int count, int openEnds)
    {
        if (count >= 5) return 50000; // Win
        
        return count switch
        {
            4 when openEnds == 2 => 4000,  // Open four
            4 when openEnds == 1 => 1000,  // Semi-open four
            3 when openEnds == 2 => 300,   // Open three
            3 when openEnds == 1 => 100,   // Semi-open three
            2 when openEnds == 2 => 30,    // Open two
            2 when openEnds == 1 => 10,    // Semi-open two
            _ => count
        };
    }
}