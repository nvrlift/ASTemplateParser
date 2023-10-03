namespace nvrlift.AssettoServer.HostExtension;

internal static class Program
{
    private static void Main()
    {
        RestartWatcher watcher = new();
        
        watcher.Init();
        
        string cancelInput;
        do {
            cancelInput = Console.ReadLine() ?? "";                
        } while (cancelInput != "lift");
    }
}
