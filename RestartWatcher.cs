using System.Diagnostics;

namespace nvrlift.AssettoServer.HostExtension;

public class RestartWatcher
{
    private readonly string _basePath;
    private readonly string _asExecutable = "";
    private Process? _currentProcess;
    private readonly string _presetsPath;
    private FileSystemWatcher _fileWatcher = null!;

    public RestartWatcher()
    {
        _basePath = Environment.CurrentDirectory;
        var restartPath = Path.Join(_basePath, "cfg", "restart");
        _presetsPath = Path.Join(_basePath, "presets");
        const string assettoServerLinux = "AssettoServer.exe";
        const string assettoServerWindows = "AssettoServer";
        if (File.Exists(Path.Join(_basePath, assettoServerLinux)))
            _asExecutable = assettoServerLinux;
        else if (File.Exists(Path.Join(_basePath, assettoServerWindows)))
            _asExecutable = assettoServerWindows;
        else
        {
            ConsoleLog($"AssettoServer not found.");
        }

        if (_asExecutable == "") return;
        
        if (!Path.Exists(restartPath))
            Directory.CreateDirectory(restartPath);
        if (!Path.Exists(_presetsPath))
            Directory.CreateDirectory(_presetsPath);
        foreach (var path in Directory.GetDirectories(_presetsPath))
        {
            var presetRestartPath = Path.Join(path, "restart");
            if (!Path.Exists(presetRestartPath))
                Directory.CreateDirectory(presetRestartPath);
        }
        
        var randomPreset = RandomPreset();
        if (randomPreset == null)
        {
            ConsoleLog($"No preset found.");

            return;
        }
        ConsoleLog($"Starting restart service.");
        ConsoleLog($"Base directory: {_basePath}");
        ConsoleLog($"Preset directory: {_presetsPath}");

        _currentProcess = StartAssettoServer(randomPreset);
        Thread.Sleep(1_000);
        
        ConsoleLog($"Server restarted with Process-ID: {_currentProcess?.Id}");
        ConsoleLog($"Using config preset: {randomPreset}");
        
        _fileWatcher = StartWatcher(_basePath);
        GC.KeepAlive(_fileWatcher);  
        
        Thread.Sleep(2_000);
    }
    
    private FileSystemWatcher StartWatcher(string path)
    {
        var watcher = new FileSystemWatcher()
        {
            Path = Path.Join(path),
            NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName
                                                       | NotifyFilters.CreationTime,
            Filter = @"*.asrestart",
            IncludeSubdirectories = true,
        };
        watcher.BeginInit();
        
        watcher.Created += OnRestartFileCreated;
        watcher.Error += OnError;
        watcher.EnableRaisingEvents = true;
        
        watcher.EndInit();

        return watcher;
    }
    
    private void OnRestartFileCreated(object source, FileSystemEventArgs e)
    {
        if (!Path.GetFileName(Path.GetDirectoryName(e.FullPath))!.Equals("restart",
                StringComparison.InvariantCultureIgnoreCase)) return;
        ConsoleLog($"Restart file found: {e.Name}");
        Thread.Sleep(500);
        
        if (_currentProcess != null)
            StopAssettoServer(_currentProcess);

        var preset = File.ReadAllText(e.FullPath);

        ConsoleLogSpacer();
        
        _currentProcess = StartAssettoServer(preset);
        ConsoleLog($"Server restarted with Process-ID: {_currentProcess?.Id}");
        ConsoleLog($"Using config preset: {preset}");
        
        File.Delete(e.FullPath);
    }
    
    private void OnError(object source, ErrorEventArgs e)
    {
        ConsoleLog(e.GetException().GetType() == typeof(InternalBufferOverflowException)
            ? $"Restart listener internal overflow."
            : $"Directory inaccessible.");
        NotAccessibleError(_fileWatcher ,e);
    }
    
    void NotAccessibleError(FileSystemWatcher source, ErrorEventArgs e)
    {
        int i = 0;
        while ((!Directory.Exists(source.Path) || source.EnableRaisingEvents == false) && i < 120)
        {
            i += 1;
            try
            {
                source.EnableRaisingEvents = false;
                if (!Directory.Exists(source.Path))
                {
                    ConsoleLog($"Directory inaccessible: {source.Path}.");
                    Thread.Sleep(30_000);
                }
                else
                {
                    var path = source.Path;
                    // ReInitialize the Component
                    source.Dispose();
                    source = StartWatcher(path);
                    ConsoleLog($"Try to restart Restart-Listener.");
                }
            }
            catch (Exception error)
            {
                ConsoleLog($"Error trying restart Restart-Listener {error.StackTrace}");
                source.EnableRaisingEvents = false;
                Thread.Sleep(5_000);
            }
        }
    }

    private Process StartAssettoServer(string? preset = null)
    {
        string args = preset != null ? $"--preset=\"{preset.Trim()}\"" : "";
        
        var psi = new ProcessStartInfo(_asExecutable)
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            Arguments = args,
            WorkingDirectory = _basePath,
            CreateNoWindow = true
        };

        Process asProcess = new Process();
        asProcess.StartInfo = psi;
        asProcess.Start();
        
        return asProcess;
    }

    private void StopAssettoServer(Process serverProcess)
    {
        while (!serverProcess.HasExited)
        {
            serverProcess.Kill();
            Thread.Sleep(500);
        }
    }

    public void Exit()
    {
        ConsoleLog($"Exiting AssettoServer");
        StopAssettoServer(_currentProcess!);
        ConsoleLog($"Exiting AS-Restarter");
        Thread.Sleep(500);
    }

    private string? RandomPreset()
    {
        var directories = Directory.GetDirectories(_presetsPath);
        if (directories.Length == 0)
            return null;
        var presets = directories.Select(f => Path.GetFileName(f)).ToList();
        var randomPreset = presets[Random.Shared.Next(presets.Count)];
        return randomPreset;
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
