namespace nvrlift.AssettoServer.HostExtension;

internal static class Program
{
    private static void Main()
    {
        RestartWatcher watcher = new();
        
        watcher.Init();
        
        Console.WriteLine("Type 'lift' to close this application.");
        string cancelInput;
        do {
            cancelInput = Console.ReadLine() ?? "";                
        } while (cancelInput != "lift");
        
        watcher.StopAssettoServer();
    }
}
