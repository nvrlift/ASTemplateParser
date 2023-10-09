using System.Diagnostics;

namespace nvrlift.AssettoServer.HostExtension;

public class RestartWatcher
{
    private readonly string _basePath;
    private readonly string _restartPath;
    private readonly string _assettoServerPath;
    private readonly string _restartFilter = "*.asrestart";
    private readonly FileSystemWatcher _watcher;
    private Process? CurrentProcess = null;

    public RestartWatcher()
    {
        _basePath = Environment.CurrentDirectory;
        _restartPath = Path.Join(_basePath, "cfg", "restart");
        _assettoServerPath = Path.Join(_basePath, "AssettoServer.exe");

        if (!Path.Exists(_restartPath))
            Directory.CreateDirectory(_restartPath);
        
        // Init File Watcher
        _watcher = new FileSystemWatcher()
        {
            Path = Path.Join(_restartPath),
            NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
                                                    | NotifyFilters.FileName,
            Filter = _restartFilter,
        };
        _watcher.Created += new FileSystemEventHandler(OnRestartFileCreated);

        _watcher.EnableRaisingEvents = true;
    }

    public void Init()
    {
        ConsoleLog($"Starting restart service.");
        ConsoleLog($"Base directory: {_basePath}");
        ConsoleLog($"Restart file directory: {_restartPath}");
        
        foreach (string sFile in Directory.GetFiles(_restartPath, "*.asrestart"))
        {
            File.Delete(sFile);
        }

        var initPath = Path.Join(_restartPath, "init.asrestart");
        var initFile = File.Create(initPath);
        initFile.Close();
        Thread.Sleep(2_000);
    }

    private void OnRestartFileCreated(object source, FileSystemEventArgs e)
    {
        if (CurrentProcess != null)
            StopAssettoServer(CurrentProcess);

        ConsoleLog($"Restart file found: {e.Name}");
        ConsoleLogSpacer();

        string preset = File.ReadAllText(e.FullPath);

        File.Delete(e.FullPath);
        string args = e.Name.Equals("init.asrestart") ? "" : $"--preset={preset.Trim()}";
        CurrentProcess = StartAssettoServer(_assettoServerPath, args);
        ConsoleLog($"Server restarted with Process-ID: {CurrentProcess?.Id}");
        ConsoleLog($"Using config preset: {CurrentProcess?.Id}");
    }

    private Process StartAssettoServer(string assettoServerPath, string assettoServerArgs)
    {
        var psi = new ProcessStartInfo(assettoServerPath, assettoServerArgs);
        psi.UseShellExecute = true;

        return Process.Start(psi);
    }

    private void StopAssettoServer(Process serverProcess)
    {
        while (!serverProcess.HasExited)
            serverProcess.Kill();
    }

    public void StopAssettoServer()
    {
        StopAssettoServer(CurrentProcess!);
    }

    private string ConsoleLogTime()
    {
        var date = DateTime.Now;
        return $"[{date:yyyy-MM-dd hh:mm:ss}]";
    }

    private void ConsoleLogSpacer()
    {
        Console.WriteLine("-----");
    }
    
    private void ConsoleLog(string log)
    {
        var output = $"{ConsoleLogTime()} {log}";
        Console.WriteLine(output);
    }
}
