namespace TicTacToeServer;

public interface IGameServer
{
    Task<GameRoom> FindOrCreateRoom(ClientHandler client);
    Task StartGame(GameRoom room);
    Task<bool> ProcessGameMove(GameRoom room, ClientHandler player, MoveData move);
    Task LeaveRoom(ClientHandler client, GameRoom room);
    void RemoveClient(ClientHandler client);
}