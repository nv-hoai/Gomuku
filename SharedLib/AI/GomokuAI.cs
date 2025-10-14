using SharedLib.GameEngine;

namespace SharedLib.AI;

public class GomokuAI
{
    private const int MAX_DEPTH = 3; // Reduced for faster response
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
        var startTime = DateTime.Now;
        const int maxTimeMs = 5000; // 5 second timeout
        
        int bestValue = -INFINITY;
        int bestRow = -1;
        int bestCol = -1;

        var moves = GetPossibleMoves(board);
        
        // If it's the first move, play in the center
        if (moves.Count == GameLogic.BOARD_SIZE * GameLogic.BOARD_SIZE)
        {
            return (GameLogic.BOARD_SIZE / 2, GameLogic.BOARD_SIZE / 2);
        }

        // Quick check for immediate win or block
        var criticalMove = FindCriticalMove(board);
        if (criticalMove.row != -1)
        {
            return criticalMove;
        }

        // Limit moves for performance if too many candidates
        if (moves.Count > 20)
        {
            moves = moves.Take(20).ToList();
        }

        foreach (var (row, col) in moves)
        {
            // Check timeout
            if ((DateTime.Now - startTime).TotalMilliseconds > maxTimeMs)
            {
                Console.WriteLine($"[AI] Timeout reached, returning best move so far: ({bestRow}, {bestCol})");
                break;
            }

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

        // Fallback to first valid move if no best move found
        if (bestRow == -1 && moves.Count > 0)
        {
            return moves[0];
        }

        return bestRow != -1 ? (bestRow, bestCol) : (-1, -1);
    }

    private (int row, int col) FindCriticalMove(string[,] board)
    {
        var moves = GetPossibleMoves(board);

        // First priority: Check if AI can win immediately
        foreach (var (row, col) in moves)
        {
            var testBoard = GameLogic.CopyBoard(board);
            testBoard[row, col] = aiSymbol;
            if (GameLogic.CheckWin(testBoard, row, col, aiSymbol))
            {
                return (row, col);
            }
        }

        // Second priority: Block immediate human win
        foreach (var (row, col) in moves)
        {
            var testBoard = GameLogic.CopyBoard(board);
            testBoard[row, col] = humanSymbol;
            if (GameLogic.CheckWin(testBoard, row, col, humanSymbol))
            {
                return (row, col);
            }
        }

        // Third priority: Check for AI winning opportunities (create 4 open)
        var aiWinningMove = FindWinningOpportunity(board, moves, aiSymbol);
        if (aiWinningMove.row != -1)
        {
            return aiWinningMove;
        }

        // Fourth priority: Block human threatening moves (3 open, double 3, etc.)
        var humanThreatMove = FindThreatMove(board, moves, humanSymbol);
        if (humanThreatMove.row != -1)
        {
            return humanThreatMove;
        }

        return (-1, -1); // No critical move found
    }

    private (int row, int col) FindWinningOpportunity(string[,] board, List<(int, int)> moves, string player)
    {
        // Look for moves that create multiple ways to win (double threats)
        foreach (var (row, col) in moves)
        {
            var testBoard = GameLogic.CopyBoard(board);
            testBoard[row, col] = player;
            
            // Count how many ways this move creates to win next turn
            int winningPaths = CountWinningPaths(testBoard, row, col, player);
            if (winningPaths >= 2) // Double threat
            {
                return (row, col);
            }
        }
        
        return (-1, -1);
    }

    private (int row, int col) FindThreatMove(string[,] board, List<(int, int)> moves, string player)
    {
        var bestMove = (-1, -1);
        int maxThreatLevel = 0;

        foreach (var (row, col) in moves)
        {
            int threatLevel = EvaluateThreatLevel(board, row, col, player);
            
            if (threatLevel > maxThreatLevel)
            {
                maxThreatLevel = threatLevel;
                bestMove = (row, col);
            }
        }

        // Only return move if threat level is significant
        return maxThreatLevel >= 100 ? bestMove : (-1, -1);
    }

    private int CountWinningPaths(string[,] board, int row, int col, string player)
    {
        int winPaths = 0;
        var possibleMoves = GetPossibleMoves(board);

        foreach (var (r, c) in possibleMoves)
        {
            var testBoard = GameLogic.CopyBoard(board);
            testBoard[r, c] = player;
            if (GameLogic.CheckWin(testBoard, r, c, player))
            {
                winPaths++;
            }
        }

        return winPaths;
    }

    private int EvaluateThreatLevel(string[,] board, int row, int col, string player)
    {
        // Simulate placing opponent's piece and evaluate the threat
        var testBoard = GameLogic.CopyBoard(board);
        testBoard[row, col] = player;
        
        int maxThreat = 0;
        int[] directions = { 1, 0, 1, 1, 0, 1, -1, 1 }; // horizontal, vertical, diagonal1, diagonal2

        for (int d = 0; d < 8; d += 2)
        {
            int dr = directions[d];
            int dc = directions[d + 1];
            
            int count = 1; // Count the piece we just placed
            int openEnds = 0;

            // Check positive direction
            int r = row + dr, c = col + dc;
            while (r >= 0 && r < GameLogic.BOARD_SIZE && c >= 0 && c < GameLogic.BOARD_SIZE && testBoard[r, c] == player)
            {
                count++;
                r += dr;
                c += dc;
            }
            if (r >= 0 && r < GameLogic.BOARD_SIZE && c >= 0 && c < GameLogic.BOARD_SIZE && string.IsNullOrEmpty(testBoard[r, c]))
            {
                openEnds++;
            }

            // Check negative direction  
            r = row - dr;
            c = col - dc;
            while (r >= 0 && r < GameLogic.BOARD_SIZE && c >= 0 && c < GameLogic.BOARD_SIZE && testBoard[r, c] == player)
            {
                count++;
                r -= dr;
                c -= dc;
            }
            if (r >= 0 && r < GameLogic.BOARD_SIZE && c >= 0 && c < GameLogic.BOARD_SIZE && string.IsNullOrEmpty(testBoard[r, c]))
            {
                openEnds++;
            }

            // Calculate threat level for this direction
            int directionThreat = GetThreatScore(count, openEnds);
            maxThreat = Math.Max(maxThreat, directionThreat);
        }

        return maxThreat;
    }

    private int GetThreatScore(int count, int openEnds)
    {
        if (count >= 5) return 10000; // Immediate win
        
        return count switch
        {
            4 when openEnds >= 1 => 5000,  // Can win next turn
            3 when openEnds == 2 => 500,   // Open three - very dangerous!
            3 when openEnds == 1 => 100,   // Semi-open three - still dangerous
            2 when openEnds == 2 => 50,    // Open two - potential threat
            _ => 0
        };
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