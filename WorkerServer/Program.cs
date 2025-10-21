var mainServerHost = args.Length > 0 ? args[0] : "localhost";
var mainServerPort = args.Length > 1 && int.TryParse(args[1], out int port) ? port : 5001;

var workerServer = new WorkerServer.WorkerServer(mainServerHost, mainServerPort);

Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    workerServer.Stop();
};

Console.WriteLine($"Starting Worker Server - connecting to MainServer at {mainServerHost}:{mainServerPort}...");
Console.WriteLine("Usage: WorkerServer.exe [mainServerHost] [mainServerPort]");
Console.WriteLine("Example: WorkerServer.exe localhost 5001");
Console.WriteLine();

try
{
    await workerServer.StartAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Worker Server error: {ex.Message}");
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();