using System;

namespace TicTacToeServer;

public class GameRoom
{
    public string RoomId { get; set; }
    public ClientHandler Player1 { get; set; }
    public ClientHandler Player2 { get; set; }
    public bool Player1Ready { get; set; } = false;
    public bool Player2Ready { get; set; } = false;
    public string[,] Board { get; set; } = new string[15, 15];
    public string CurrentPlayer { get; set; } = "X";
    public bool IsGameActive { get; set; } = false;
    public DateTime LastActivity { get; set; } = DateTime.Now;

    // PvE flag: Player2 is AI if true
    public bool IsVsAI { get; set; } = false;

    public GameRoom(string roomId)
    {
        RoomId = roomId;
        InitializeBoard();
    }

    private void InitializeBoard()
    {
        for (int i = 0; i < 15; i++)
        {
            for (int j = 0; j < 15; j++)
            {
                Board[i, j] = string.Empty;
            }
        }
    }

    public bool IsFull => IsVsAI ? Player1 != null : Player1 != null && Player2 != null;
    public bool IsEmpty => Player1 == null && (!IsVsAI ? Player2 == null : true);
    public bool BothPlayersReady => IsVsAI ? Player1Ready : Player1Ready && Player2Ready && IsFull;

    public ClientHandler GetOpponent(ClientHandler player)
    {
        if (IsVsAI)
        {
            // No actual opponent client when playing vs AI
            return null;
        }
        return player == Player1 ? Player2 : Player1;
    }

    public bool AddPlayer(ClientHandler client)
    {
        if (Player1 == null)
        {
            Player1 = client;
            client.PlayerSymbol = "X";
            return true;
        }
        else if (!IsVsAI && Player2 == null)
        {
            Player2 = client;
            client.PlayerSymbol = "O";
            return true;
        }
        return false;
    }

    public void SetupVsAI()
    {
        IsVsAI = true;
        Player2 = null;      // AI placeholder
        Player2Ready = true; // AI is always ready
    }

    public void RemovePlayer(ClientHandler client)
    {
        if (Player1 == client)
            Player1 = null;
        else if (!IsVsAI && Player2 == client)
            Player2 = null;

        IsGameActive = false;
    }
}
