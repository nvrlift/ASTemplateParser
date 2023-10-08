using System.Diagnostics;

namespace nvrlift.AssettoServer.HostExtension;

public class RestartWatcher
{
    private readonly string _basePath;
    private readonly string _restartPath;
    private readonly string _assettoServerPath;
    private readonly string _assettoServerArgs;
    private readonly string _restartFilter = "*.asrestart";
    private FileSystemWatcher _watcher;
    private Process? CurrentProcess = null;

    public RestartWatcher()
    {
        _basePath = Environment.CurrentDirectory;
        _restartPath = Path.Join(_basePath, "cfg", "restart");
        _assettoServerPath = Path.Join(_basePath, "AssettoServer.exe");
        _assettoServerArgs = "";

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
        Console.WriteLine(_basePath);
        
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

        Console.WriteLine(CurrentProcess?.Id);
        Console.WriteLine(e.FullPath);
        Console.WriteLine("-----");

        File.Delete(e.FullPath);
        CurrentProcess = StartAssettoServer(_assettoServerPath, _assettoServerArgs);
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
        while (!CurrentProcess!.HasExited)
            CurrentProcess.Kill();
    }
}
