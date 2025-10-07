using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace TicTacToeServer;
public class TicTacToeServer : IGameServer
{
    private TcpListener tcpListener;
    private readonly int port;
    private bool isRunning = false;
    private readonly ConcurrentDictionary<string, ClientHandler> clients = new();
    private readonly MatchmakingService matchmakingService = new();

    public TicTacToeServer(int port = 5000)
    {
        this.port = port;
    }

    public async Task StartAsync()
    {
        tcpListener = new TcpListener(IPAddress.Any, port);
        tcpListener.Start();
        isRunning = true;

        Console.WriteLine($"Tic-Tac-Toe Server started on port {port}");

        // Start cleanup task
        _ = Task.Run(CleanupRoomsAsync);

        while (isRunning)
        {
            try
            {
                var tcpClient = await tcpListener.AcceptTcpClientAsync();
                var clientHandler = new ClientHandler(tcpClient, this);
                clients[clientHandler.ClientId] = clientHandler;

                Console.WriteLine($"New client connected: {clientHandler.ClientId}");

                // Handle client in background
                _ = Task.Run(() => clientHandler.HandleClientAsync());
            }
            catch (Exception ex)
            {
                if (isRunning)
                {
                    Console.WriteLine($"Error accepting client: {ex.Message}");
                }
            }
        }
    }

    public Task<GameRoom> FindOrCreateRoom(ClientHandler client)
    {
        return matchmakingService.FindOrCreateRoom(client);
    }

    public async Task StartGame(GameRoom room)
    {
        if (room.IsGameActive)
            return;

        room.IsGameActive = true;
        room.LastActivity = DateTime.Now;

        var startMessage = $"GAME_START:{{\"roomId\":\"{room.RoomId}\",\"currentPlayer\":\"{room.CurrentPlayer}\"}}";

        if (room.Player1 != null)
            await room.Player1.SendMessage(startMessage);

        if (room.Player2 != null)
            await room.Player2.SendMessage(startMessage);

        Console.WriteLine($"Game started in room {room.RoomId}");
    }

    public async Task<bool> ProcessGameMove(GameRoom room, ClientHandler player, MoveData move)
    {
        // Validate move
        if (move.row < 0 || move.row >= 15 || move.col < 0 || move.col >= 15)
        {
            await player.SendMessage("ERROR:Invalid move coordinates");
            return false;
        }

        if (!string.IsNullOrEmpty(room.Board[move.row, move.col]))
        {
            await player.SendMessage("ERROR:Cell already occupied");
            return false;
        }

        // Apply move
        room.Board[move.row, move.col] = player.PlayerSymbol;
        room.LastActivity = DateTime.Now;

        // Check for win condition
        if (GameLogic.CheckWin(room.Board, move.row, move.col, player.PlayerSymbol))
        {
            await EndGame(room, player, "WIN");
            return true;
        }

        // Check for draw
        if (GameLogic.IsBoardFull(room.Board))
        {
            await EndGame(room, null, "DRAW");
            return true;
        }

        // Switch turns
        room.CurrentPlayer = room.CurrentPlayer == "X" ? "O" : "X";

        var turnMessage = $"TURN_CHANGE:{{\"currentPlayer\":\"{room.CurrentPlayer}\"}}";
        if (room.Player1 != null)
            await room.Player1.SendMessage(turnMessage);
        if (room.Player2 != null)
            await room.Player2.SendMessage(turnMessage);

        return true;
    }

    private async Task EndGame(GameRoom room, ClientHandler winner, string reason)
    {
        room.IsGameActive = false;

        string endMessage = $"GAME_END:{{\"reason\":\"{reason}\",\"winner\":\"{winner?.PlayerSymbol ?? "NONE"}\"}}";

        if (room.Player1 != null)
            await room.Player1.SendMessage(endMessage);
        if (room.Player2 != null)
            await room.Player2.SendMessage(endMessage);

        Console.WriteLine($"Game ended in room {room.RoomId}: {reason}");
    }

    public async Task LeaveRoom(ClientHandler client, GameRoom room)
    {
        matchmakingService.LeaveRoom(client, room);

        var opponent = room.GetOpponent(client);
        if (opponent != null)
        {
            await opponent.SendMessage("OPPONENT_LEFT:Your opponent left the game");
        }
    }

    public void RemoveClient(ClientHandler client)
    {
        clients.TryRemove(client.ClientId, out _);
    }

    private async Task CleanupRoomsAsync()
    {
        while (isRunning)
        {
            try
            {
                await matchmakingService.CleanupRoomsAsync();
                await Task.Delay(TimeSpan.FromMinutes(1));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in cleanup task: {ex.Message}");
            }
        }
    }

    public void Stop()
    {
        isRunning = false;
        tcpListener?.Stop();
        Console.WriteLine("Server stopped");
    }
}
