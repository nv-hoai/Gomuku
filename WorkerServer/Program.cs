var workerServer = new WorkerServer.WorkerServer();

Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    workerServer.Stop();
};

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