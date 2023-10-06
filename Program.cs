namespace nvrlift.AssettoServer.HostExtension;

internal static class Program
{
    private static void Main()
    {
        RestartWatcher watcher = new();
        AppDomain.CurrentDomain.ProcessExit += new EventHandler((sender, e) => ProcessExit(sender, e, watcher));

        watcher.Init();

        Console.WriteLine("Type 'lift' to close this application.");
        string cancelInput;
        do
        {
            cancelInput = Console.ReadLine() ?? "";
        } while (cancelInput != "lift");
    }

    private static void ProcessExit(object sender, EventArgs e, RestartWatcher watcher)
    {
        watcher.StopAssettoServer();
    }
}
