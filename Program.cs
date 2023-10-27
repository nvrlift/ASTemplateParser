namespace nvrlift.AssettoServer.HostExtension;

internal static class Program
{
    private static void Main()
    {
        Console.WriteLine("Press any key to close this application.");
        RestartWatcher watcher = new();
        AppDomain.CurrentDomain.ProcessExit += (sender, e) => ProcessExit(sender, e, watcher);

        Console.ReadKey();
    }

    private static void ProcessExit(object? sender, EventArgs e, RestartWatcher watcher)
    {
        
        watcher.Exit();
    }
}
