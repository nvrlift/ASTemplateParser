using System.Diagnostics;
using Serilog;
using static nvrlift.AssettoServer.HostExtension.Const;

namespace nvrlift.AssettoServer.HostExtension;

public class RestartWatcher : IDisposable
{
    private readonly string _basePath;
    private readonly string _startPreset = "";
    private readonly string _asExecutable = "";
    private Process? _currentProcess;
    private readonly string _presetsPath;
    private FileSystemWatcher _fileWatcher = null!;
    private readonly bool _useDocker;
    private readonly bool _debug;
    private bool _exit = false;
   
    public RestartWatcher(string basePath, string startPreset, bool useDocker, bool debug)
    {
        // Init Paths
        _basePath = basePath;
        _presetsPath = Path.Join(_basePath, "presets");
        var restartPath = Path.Join(_basePath, "cfg", "restart");
        
        // Runtime options
        _useDocker = useDocker;
        _debug = debug;
        if (!string.IsNullOrEmpty(startPreset))
            if (Path.Exists(Path.Join(_presetsPath, startPreset)))
                _startPreset = startPreset;

        var appPath = _useDocker ? "/app" : _basePath;
        if (_debug)
            Log.Information($"App path: {appPath}");
        
        if (File.Exists(Path.Join(appPath, AssettoServerLinux)))
            _asExecutable = Path.Join(appPath, AssettoServerLinux);
        else if (File.Exists(Path.Join(appPath, AssettoServerWindows)))
            _asExecutable = Path.Join(appPath, AssettoServerWindows);
        else
            Log.Information($"AssettoServer not found at '{appPath}'.");

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
    }

    public async Task RunAsync()
    {
        string preset = _startPreset;
        if (string.IsNullOrEmpty(preset))
        {
            var randomPreset = RandomPreset();
            if (randomPreset == null)
            {
                Log.Error($"No preset found.");

                return;
            }

            preset = randomPreset;
        }
        
        Log.Information($"Base directory: {_basePath}");
        Log.Information($"Preset directory: {_presetsPath}");

        _currentProcess = StartAssettoServer(preset);
        await Task.Delay(1_000);
        
        _fileWatcher = StartWatcher(_basePath);
        // GC.KeepAlive(_fileWatcher);  
        while (!_exit)
            await Task.Delay(2_000);
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
        Log.Information($"Restart file found: {e.Name}");
        Thread.Sleep(500);
        
        if (_currentProcess != null)
            StopAssettoServer(_currentProcess);

        var preset = File.ReadAllText(e.FullPath);

        Log.Information(Separator);
        
        _currentProcess = StartAssettoServer(preset);
        
        File.Delete(e.FullPath);
    }
    
    private void OnError(object source, ErrorEventArgs e)
    {
        Log.Error(e.GetException().GetType() == typeof(InternalBufferOverflowException)
            ? $"Restart listener internal overflow."
            : $"Directory inaccessible.");
        NotAccessibleError(_fileWatcher ,e);
    }

    private void NotAccessibleError(FileSystemWatcher source, ErrorEventArgs e)
    {
        var i = 0;
        while ((!Directory.Exists(source.Path) || source.EnableRaisingEvents == false) && i < 120)
        {
            i += 1;
            try
            {
                source.EnableRaisingEvents = false;
                if (!Directory.Exists(source.Path))
                {
                    Log.Error($"Directory inaccessible: {source.Path}.");
                    Thread.Sleep(30_000);
                }
                else
                {
                    var path = source.Path;
                    // ReInitialize the Component
                    source.Dispose();
                    source = StartWatcher(path);
                    Log.Warning($"Try to restart Restart-Listener.");
                }
            }
            catch (Exception error)
            {
                Log.Error($"Error trying restart Restart-Listener {error.StackTrace}");
                source.EnableRaisingEvents = false;
                Thread.Sleep(5_000);
            }
        }
    }

    private Process StartAssettoServer(string? preset = null)
    {
        string args = preset != null ? $"--preset=\"{preset.Trim()}\"" : "";
        if (_useDocker)
            args += " --plugins-from-workdir";
        args = args.Trim();


        if (_debug)
        {
            Log.Debug($"Executable: {_asExecutable}");
            Log.Debug($"Arguments:  {args}");
        }
        
        var psi = new ProcessStartInfo()
        {
            FileName = _asExecutable,
            Arguments = args,
#if (DEBUG)            
            WorkingDirectory = _basePath,
#endif            
            
            UseShellExecute = false,
            CreateNoWindow = true,

            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true
        };
        
        Process asProcess = new();
        asProcess.StartInfo = psi;
        asProcess.Start();
        
        Log.Information($"Server restarted with Process-ID: {asProcess.Id}");
        Log.Information($"Using config preset: {preset}");
        
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
        Log.Information($"Exiting AssettoServer");
        if (_currentProcess != null)
            StopAssettoServer(_currentProcess);
        Log.Information($"Exiting AS-Restarter");
        Thread.Sleep(500);
    }

    private string? RandomPreset()
    {
        var directories = Directory.GetDirectories(_presetsPath);
        if (directories.Length == 0)
            return null;
        var presets = directories.Select(Path.GetFileName).ToList();
        var randomPreset = presets[Random.Shared.Next(presets.Count)];
        return randomPreset;
    }

    public void Dispose()
    {
        Exit();
    }
}
