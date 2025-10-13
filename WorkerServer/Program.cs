var workerServer = new WorkerServer.WorkerServer(6000);

Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    workerServer.Stop();
};

Console.WriteLine("Starting Worker Server...");

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