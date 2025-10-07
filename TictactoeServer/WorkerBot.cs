using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using TictactoeAI;

namespace TicTacToeServer;

public class WorkerBot
{
    private readonly string _botId;
    private readonly string _difficulty;
    private readonly TictactoeAI.TictactoeAI _ai;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private string? _playerSymbol;
    private bool _isRunning;
    private string[,] _gameBoard = new string[15, 15];
    private bool _isInGame;
    private GameRoom? _currentRoom;

    public string BotId => _botId;
    public bool IsInGame => _isInGame;

    public WorkerBot(string botId, string difficulty = "medium")
    {
        _botId = botId;
        _difficulty = difficulty;
        _ai = new TictactoeAI.TictactoeAI(difficulty);
        InitializeBoard();
    }

    private void InitializeBoard()
    {
        for (int i = 0; i < 15; i++)
        {
            for (int j = 0; j < 15; j++)
            {
                _gameBoard[i, j] = string.Empty;
            }
        }
    }

    public async Task ConnectAndPlayAsync(string serverHost, int serverPort)
    {
        while (true) // Auto-reconnect loop
        {
            try
            {
                Console.WriteLine($"[{_botId}] Connecting to server {serverHost}:{serverPort}...");
                
                _client = new TcpClient();
                await _client.ConnectAsync(serverHost, serverPort);
                _stream = _client.GetStream();
                _isRunning = true;
                _isInGame = false;

                Console.WriteLine($"[{_botId}] Connected successfully!");

                // Send bot identification and player info
                var playerInfo = new PlayerInfo
                {
                    playerId = _botId,
                    playerName = $"AI-{_difficulty.ToUpper()}",
                    playerLevel = _difficulty switch
                    {
                        "easy" => 1,
                        "medium" => 5,
                        "hard" => 10,
                        _ => 5
                    },
                    playerElo = _difficulty switch
                    {
                        "easy" => 800,
                        "medium" => 1200,
                        "hard" => 1800,
                        _ => 1200
                    }
                };

                await SendMessageAsync($"PLAYER_INFO:{JsonSerializer.Serialize(playerInfo)}");

                // Start matchmaking
                await Task.Delay(1000); // Small delay before finding match
                await SendMessageAsync("FIND_MATCH");

                // Listen for game events
                await ListenForMessagesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_botId}] Error: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }

            // Wait before reconnecting
            await Task.Delay(5000);
        }
    }

    private async Task ListenForMessagesAsync()
    {
        var buffer = new byte[4096];
        var messageBuilder = new StringBuilder();

        while (_isRunning && _stream != null && _client != null && _client.Connected)
        {
            try
            {
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    Console.WriteLine($"[{_botId}] Server disconnected");
                    break;
                }

                string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuilder.Append(data);

                string messages = messageBuilder.ToString();
                string[] lines = messages.Split('\n');

                for (int i = 0; i < lines.Length - 1; i++)
                {
                    string message = lines[i].Trim();
                    if (!string.IsNullOrEmpty(message))
                    {
                        await ProcessMessageAsync(message);
                    }
                }

                messageBuilder.Clear();
                if (lines.Length > 0)
                {
                    messageBuilder.Append(lines[lines.Length - 1]);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_botId}] Error reading message: {ex.Message}");
                break;
            }
        }
    }

    private async Task ProcessMessageAsync(string message)
    {
        Console.WriteLine($"[{_botId}] Received: {message}");

        try
        {
            if (message.StartsWith("PLAYER_SYMBOL:"))
            {
                _playerSymbol = message.Split(':')[1];
                Console.WriteLine($"[{_botId}] Assigned symbol: {_playerSymbol}");
            }
            else if (message.StartsWith("JOIN_ROOM:"))
            {
                var roomId = message.Split(':')[1];
                Console.WriteLine($"[{_botId}] Joined room: {roomId}");
            }
            else if (message.StartsWith("MATCH_FOUND:"))
            {
                Console.WriteLine($"[{_botId}] Match found! Preparing to start game...");
                await Task.Delay(1000);
                await SendMessageAsync("START_GAME");
            }
            else if (message.StartsWith("GAME_START:"))
            {
                await HandleGameStartAsync(message);
            }
            else if (message.StartsWith("TURN_CHANGE:"))
            {
                await HandleTurnChangeAsync(message);
            }
            else if (message.StartsWith("GAME_MOVE:"))
            {
                await HandleOpponentMoveAsync(message);
            }
            else if (message.StartsWith("GAME_END:"))
            {
                await HandleGameEndAsync(message);
            }
            else if (message.StartsWith("WAITING_FOR_OPPONENT:"))
            {
                Console.WriteLine($"[{_botId}] Waiting for opponent...");
            }
            else if (message.StartsWith("OPPONENT_INFO:"))
            {
                Console.WriteLine($"[{_botId}] Opponent information received");
            }
            else if (message.StartsWith("ERROR:"))
            {
                Console.WriteLine($"[{_botId}] Server error: {message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_botId}] Error processing message: {ex.Message}");
        }
    }

    private async Task HandleGameStartAsync(string message)
    {
        Console.WriteLine($"[{_botId}] Game started!");
        _isInGame = true;
        InitializeBoard();
        
        // Extract current player from message
        var parts = message.Split(new[] { "currentPlayer\":\"" }, StringSplitOptions.None);
        if (parts.Length > 1)
        {
            var currentPlayer = parts[1].Split('"')[0];
            
            if (currentPlayer == _playerSymbol)
            {
                Console.WriteLine($"[{_botId}] My turn to start!");
                await Task.Delay(GetThinkingTime());
                await MakeAIMoveAsync();
            }
            else
            {
                Console.WriteLine($"[{_botId}] Waiting for opponent's move...");
            }
        }
    }

    private async Task HandleTurnChangeAsync(string message)
    {
        var parts = message.Split(new[] { "currentPlayer\":\"" }, StringSplitOptions.None);
        if (parts.Length > 1)
        {
            var currentPlayer = parts[1].Split('"')[0];
            
            if (currentPlayer == _playerSymbol)
            {
                Console.WriteLine($"[{_botId}] My turn!");
                await Task.Delay(GetThinkingTime());
                await MakeAIMoveAsync();
            }
            else
            {
                Console.WriteLine($"[{_botId}] Opponent's turn");
            }
        }
    }

    private async Task HandleOpponentMoveAsync(string message)
    {
        try
        {
            var json = message.Substring("GAME_MOVE:".Length);
            var moveData = JsonSerializer.Deserialize<MoveData>(json);
            
            if (moveData != null)
            {
                // Update local board with opponent's move
                var opponentSymbol = _playerSymbol == "X" ? "O" : "X";
                _gameBoard[moveData.row, moveData.col] = opponentSymbol;
                
                Console.WriteLine($"[{_botId}] Opponent moved to ({moveData.row}, {moveData.col})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_botId}] Error processing opponent move: {ex.Message}");
        }
    }

    private async Task HandleGameEndAsync(string message)
    {
        Console.WriteLine($"[{_botId}] Game ended: {message}");
        _isInGame = false;
        
        // Wait before finding a new match
        await Task.Delay(3000);
        
        if (_isRunning)
        {
            Console.WriteLine($"[{_botId}] Looking for new match...");
            await SendMessageAsync("FIND_MATCH");
        }
    }

    private async Task MakeAIMoveAsync()
    {
        if (string.IsNullOrEmpty(_playerSymbol))
            return;

        var (row, col) = _ai.GetBestMove(_gameBoard, _playerSymbol);
        
        if (row == -1 || col == -1)
        {
            Console.WriteLine($"[{_botId}] No valid move found");
            return;
        }

        // Update local board
        _gameBoard[row, col] = _playerSymbol;

        Console.WriteLine($"[{_botId}] Making move: ({row}, {col})");
        
        var moveData = new MoveData { row = row, col = col };
        var json = JsonSerializer.Serialize(moveData);
        await SendMessageAsync($"GAME_MOVE:{json}");
    }

    private int GetThinkingTime()
    {
        return _difficulty switch
        {
            "easy" => new Random().Next(500, 1500),
            "medium" => new Random().Next(800, 2000),
            "hard" => new Random().Next(1000, 3000),
            _ => 1000
        };
    }

    private async Task SendMessageAsync(string message)
    {
        if (_stream == null || !_client?.Connected == true)
            return;

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message + "\n");
            await _stream.WriteAsync(data, 0, data.Length);
            await _stream.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_botId}] Error sending message: {ex.Message}");
        }
    }

    private void Disconnect()
    {
        _isRunning = false;
        _isInGame = false;
        _stream?.Close();
        _client?.Close();
        Console.WriteLine($"[{_botId}] Disconnected");
    }

    public void Stop()
    {
        _isRunning = false;
        Disconnect();
    }
}