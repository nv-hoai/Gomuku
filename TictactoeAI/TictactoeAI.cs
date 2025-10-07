namespace TictactoeAI;

public class TictactoeAI
{
    private readonly Random _random = new Random();
    private readonly string _difficulty;

    public TictactoeAI(string difficulty = "medium")
    {
        _difficulty = difficulty;
    }

    /// <summary>
    /// Calculate the best move for the AI based on difficulty level
    /// </summary>
    public (int row, int col) GetBestMove(string[,] board, string aiSymbol)
    {
        string opponentSymbol = aiSymbol == "X" ? "O" : "X";

        return _difficulty switch
        {
            "easy" => GetRandomMove(board),
            "medium" => GetMediumMove(board, aiSymbol, opponentSymbol),
            "hard" => GetHardMove(board, aiSymbol, opponentSymbol),
            _ => GetMediumMove(board, aiSymbol, opponentSymbol)
        };
    }

    private (int row, int col) GetRandomMove(string[,] board)
    {
        var availableMoves = GetAvailableMoves(board);
        if (availableMoves.Count == 0)
            return (-1, -1);

        return availableMoves[_random.Next(availableMoves.Count)];
    }

    private (int row, int col) GetMediumMove(string[,] board, string aiSymbol, string opponentSymbol)
    {
        // 1. Try to win
        var winMove = FindWinningMove(board, aiSymbol);
        if (winMove.row != -1)
            return winMove;

        // 2. Block opponent from winning
        var blockMove = FindWinningMove(board, opponentSymbol);
        if (blockMove.row != -1)
            return blockMove;

        // 3. Look for good strategic positions
        var strategicMove = FindStrategicMove(board, aiSymbol);
        if (strategicMove.row != -1)
            return strategicMove;

        // 4. Random move
        return GetRandomMove(board);
    }

    private (int row, int col) GetHardMove(string[,] board, string aiSymbol, string opponentSymbol)
    {
        // 1. Try to win
        var winMove = FindWinningMove(board, aiSymbol);
        if (winMove.row != -1)
            return winMove;

        // 2. Block opponent from winning
        var blockMove = FindWinningMove(board, opponentSymbol);
        if (blockMove.row != -1)
            return blockMove;

        // 3. Use Minimax with alpha-beta pruning for optimal move
        var bestMove = MinimaxAlphaBeta(board, aiSymbol, opponentSymbol, 3);
        if (bestMove.row != -1)
            return bestMove;

        // 4. Fallback to strategic move
        var strategicMove = FindStrategicMove(board, aiSymbol);
        if (strategicMove.row != -1)
            return strategicMove;

        // 5. Random fallback
        return GetRandomMove(board);
    }

    private (int row, int col) FindWinningMove(string[,] board, string symbol)
    {
        var moves = GetAvailableMoves(board);

        foreach (var move in moves)
        {
            // Try the move
            board[move.row, move.col] = symbol;

            // Check if it wins
            bool wins = CheckWin(board, move.row, move.col, symbol);

            // Undo the move
            board[move.row, move.col] = string.Empty;

            if (wins)
                return move;
        }

        return (-1, -1);
    }

    private (int row, int col) FindStrategicMove(string[,] board, string symbol)
    {
        var moves = GetAvailableMoves(board);
        var bestMove = (-1, -1);
        int bestScore = -1;

        foreach (var move in moves)
        {
            board[move.row, move.col] = symbol;
            int score = EvaluatePosition(board, move.row, move.col, symbol);
            board[move.row, move.col] = string.Empty;

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }

        return bestMove;
    }

    private (int row, int col) MinimaxAlphaBeta(string[,] board, string aiSymbol, string opponentSymbol, int maxDepth)
    {
        var bestMove = (-1, -1);
        int bestScore = int.MinValue;
        var moves = GetAvailableMoves(board);

        foreach (var move in moves)
        {
            board[move.row, move.col] = aiSymbol;
            int score = Minimax(board, 0, maxDepth, false, int.MinValue, int.MaxValue, aiSymbol, opponentSymbol);
            board[move.row, move.col] = string.Empty;

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }

        return bestMove;
    }

    private int Minimax(string[,] board, int depth, int maxDepth, bool isMaximizing, int alpha, int beta, string aiSymbol, string opponentSymbol)
    {
        // Check terminal states
        if (depth >= maxDepth || IsBoardFull(board))
            return EvaluateBoard(board, aiSymbol, opponentSymbol);

        if (isMaximizing)
        {
            int maxEval = int.MinValue;
            var moves = GetAvailableMoves(board);

            foreach (var move in moves)
            {
                board[move.row, move.col] = aiSymbol;
                int eval = Minimax(board, depth + 1, maxDepth, false, alpha, beta, aiSymbol, opponentSymbol);
                board[move.row, move.col] = string.Empty;

                maxEval = Math.Max(maxEval, eval);
                alpha = Math.Max(alpha, eval);
                if (beta <= alpha)
                    break; // Beta cutoff
            }
            return maxEval;
        }
        else
        {
            int minEval = int.MaxValue;
            var moves = GetAvailableMoves(board);

            foreach (var move in moves)
            {
                board[move.row, move.col] = opponentSymbol;
                int eval = Minimax(board, depth + 1, maxDepth, true, alpha, beta, aiSymbol, opponentSymbol);
                board[move.row, move.col] = string.Empty;

                minEval = Math.Min(minEval, eval);
                beta = Math.Min(beta, eval);
                if (beta <= alpha)
                    break; // Alpha cutoff
            }
            return minEval;
        }
    }

    private int EvaluateBoard(string[,] board, string aiSymbol, string opponentSymbol)
    {
        int score = 0;
        int boardSize = board.GetLength(0);

        // Evaluate all positions
        for (int row = 0; row < boardSize; row++)
        {
            for (int col = 0; col < boardSize; col++)
            {
                if (!string.IsNullOrEmpty(board[row, col]))
                {
                    score += EvaluatePosition(board, row, col, board[row, col]) * 
                            (board[row, col] == aiSymbol ? 1 : -1);
                }
            }
        }

        return score;
    }

    private int EvaluatePosition(string[,] board, int row, int col, string symbol)
    {
        int score = 0;

        // Count consecutive symbols in all directions
        score += CountConsecutive(board, row, col, 1, 0, symbol);   // Horizontal
        score += CountConsecutive(board, row, col, 0, 1, symbol);   // Vertical
        score += CountConsecutive(board, row, col, 1, 1, symbol);   // Diagonal \
        score += CountConsecutive(board, row, col, 1, -1, symbol);  // Diagonal /

        return score;
    }

    private int CountConsecutive(string[,] board, int row, int col, int dRow, int dCol, string symbol)
    {
        int count = 1;
        int boardSize = board.GetLength(0);

        // Count forward
        int r = row + dRow, c = col + dCol;
        while (r >= 0 && r < boardSize && c >= 0 && c < boardSize && board[r, c] == symbol)
        {
            count++;
            r += dRow;
            c += dCol;
        }

        // Count backward
        r = row - dRow;
        c = col - dCol;
        while (r >= 0 && r < boardSize && c >= 0 && c < boardSize && board[r, c] == symbol)
        {
            count++;
            r -= dRow;
            c -= dCol;
        }

        return count >= 5 ? 1000 : count * count; // Heavy bonus for winning line
    }

    private List<(int row, int col)> GetAvailableMoves(string[,] board)
    {
        var moves = new List<(int row, int col)>();
        int boardSize = board.GetLength(0);

        for (int row = 0; row < boardSize; row++)
        {
            for (int col = 0; col < boardSize; col++)
            {
                if (string.IsNullOrEmpty(board[row, col]))
                {
                    moves.Add((row, col));
                }
            }
        }

        return moves;
    }

    private bool CheckWin(string[,] board, int row, int col, string symbol)
    {
        return CheckDirection(board, row, col, 1, 0, symbol) ||  // Horizontal
               CheckDirection(board, row, col, 0, 1, symbol) ||  // Vertical
               CheckDirection(board, row, col, 1, 1, symbol) ||  // Diagonal \
               CheckDirection(board, row, col, 1, -1, symbol);   // Diagonal /
    }

    private bool CheckDirection(string[,] board, int row, int col, int dRow, int dCol, string symbol)
    {
        int count = 1;
        int boardSize = board.GetLength(0);

        // Count forward
        int r = row + dRow, c = col + dCol;
        while (r >= 0 && r < boardSize && c >= 0 && c < boardSize && board[r, c] == symbol)
        {
            count++;
            r += dRow;
            c += dCol;
        }

        // Count backward
        r = row - dRow;
        c = col - dCol;
        while (r >= 0 && r < boardSize && c >= 0 && c < boardSize && board[r, c] == symbol)
        {
            count++;
            r -= dRow;
            c -= dCol;
        }

        return count >= 5;
    }

    private bool IsBoardFull(string[,] board)
    {
        int boardSize = board.GetLength(0);
        for (int row = 0; row < boardSize; row++)
        {
            for (int col = 0; col < boardSize; col++)
            {
                if (string.IsNullOrEmpty(board[row, col]))
                    return false;
            }
        }
        return true;
    }
}