using System.Diagnostics;

namespace nvrlift.AssettoServer.HostExtension;

public class RestartWatcher
{
    private readonly string _basePath;
    private readonly string _restartPath;
    private readonly string _asExecutable = "";
    private readonly string _restartFilter = "*.asrestart";
    private Process? _currentProcess = null;
    private readonly string _presetsPath;
    private List<FileSystemWatcher> _fileWatchers = new();

    public RestartWatcher()
    {
        _basePath = Environment.CurrentDirectory;
        _restartPath = Path.Join(_basePath, "cfg", "restart");
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
        
        if (!Path.Exists(_restartPath))
            Directory.CreateDirectory(_restartPath);
        if (!Path.Exists(_presetsPath))
            Directory.CreateDirectory(_presetsPath);
        foreach (var path in Directory.GetDirectories(_presetsPath))
        {
            var presetRestartPath = Path.Join(path, "restart");
            if (!Path.Exists(presetRestartPath))
                Directory.CreateDirectory(presetRestartPath);
        }
        
        Init();
    }

    private void Init()
    {
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
        
        StartAllWatchers();
        
        Thread.Sleep(2_000);
    }

    private void StartAllWatchers()
    {
        /*
        // I don't think i need to re-add the file watchers 
        if (FileWatchers.Count > 0)
        {
            foreach (var watcher in FileWatchers)
                watcher.Dispose();
            FileWatchers.Clear();
        }
        */
        
        // Init File Watcher
        _fileWatchers = new() {
            StartWatcher(_restartPath)
        };

        foreach (var path in Directory.GetDirectories(_presetsPath))
            _fileWatchers.Add(StartWatcher(Path.Join(path, "restart")));
        
        GC.KeepAlive(_fileWatchers);  
    }
    
    private FileSystemWatcher StartWatcher(string path)
    {
        var watcher = new FileSystemWatcher()
        {
            Path = Path.Join(path),
            NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
                                                    | NotifyFilters.FileName,
            Filter = _restartFilter,
        };
        watcher.Created += OnRestartFileCreated;

        watcher.EnableRaisingEvents = true;

        return watcher;
    }
    
    private void OnRestartFileCreated(object source, FileSystemEventArgs e)
    {
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
