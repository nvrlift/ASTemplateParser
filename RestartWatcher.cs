using System;
using System.Diagnostics;
using System.IO;

namespace nvrlift.AssettoServer.HostExtension;

public class RestartWatcher
{
    private readonly string _basePath;
    private readonly string _assettoServerPath;
    private readonly string _assettoServerArgs;
    private readonly string _restartFilter = "*.asrestart";
    private FileSystemWatcher _watcher;
    private Process? CurrentProcess = null;
    public RestartWatcher()
    {
        _basePath = Environment.CurrentDirectory;
        _assettoServerPath = Path.Join(_basePath, "AssettoServer.exe");
        _assettoServerArgs = "";
        
        // Init File Watcher
        _watcher = new FileSystemWatcher()
        {
            Path = _basePath,
            NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
                                                    | NotifyFilters.FileName,
            Filter = _restartFilter,
        };
        _watcher.Created += new FileSystemEventHandler(OnRestartFileCreated);
        
        _watcher.EnableRaisingEvents = true;
    }

    public void Init()
    {
        var initPath = Path.Join(_basePath, "init.asrestart");
        File.Create(initPath);
    }

    private void OnRestartFileCreated(object source, FileSystemEventArgs e) 
    {
        if (CurrentProcess != null)
            StopAssettoServer(CurrentProcess);
        
        File.Delete(e.FullPath);
        CurrentProcess = StartAssettoServer(_assettoServerPath, _assettoServerArgs);
    }

    public Process StartAssettoServer(string assettoServerPath, string assettoServerArgs)
    {
        return Process.Start(assettoServerPath, assettoServerArgs);
    }
    
    public void StopAssettoServer(Process serverProcess)
    {
        while(!serverProcess.HasExited)
            serverProcess.Kill();
    }
}
