using System.Diagnostics;
using Serilog;
using static nvrlift.AssettoServer.HostExtension.Const;

namespace nvrlift.AssettoServer.HostExtension;

public class RestartWatcher
{
    private readonly string _basePath;
    private readonly string _startPreset = "";
    private readonly string _asExecutable = "";
    private Process? _currentProcess;
    private readonly string _presetsPath;
    private FileSystemWatcher _fileWatcher = null!;
    private readonly bool _useDocker;
    private bool _exit = false;

    private CancellationTokenSource _cancellationTokenSource = new();
   
    public RestartWatcher(string basePath, string startPreset, bool useDocker)
    {
        // Init Paths
        _basePath = basePath;
        _presetsPath = Path.Join(_basePath, "presets");
        var restartPath = Path.Join(_basePath, "cfg", "restart");
        
        // Runtime options
        _useDocker = useDocker;
        if (startPreset != "")
            if (Path.Exists(Path.Join(_presetsPath, startPreset)))
                _startPreset = startPreset;
        
        
        if (File.Exists(Path.Join(_basePath, AssettoServerLinux)))
            _asExecutable = Path.Join(_basePath, AssettoServerLinux);
        else if (File.Exists(Path.Join(_basePath, AssettoServerWindows)))
            _asExecutable = Path.Join(_basePath, AssettoServerWindows);
        else
            Log.Information($"AssettoServer not found.");

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
        if (preset == "")
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
        GC.KeepAlive(_fileWatcher);  
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

        // Start the IO passthrough
        if (!_cancellationTokenSource.IsCancellationRequested)
            _cancellationTokenSource.Cancel();

        _cancellationTokenSource = new CancellationTokenSource();
            
        OutputReader(asProcess);
        ErrorReader(asProcess);
        InputReader(asProcess);
        
        Log.Information($"Server restarted with Process-ID: {asProcess.Id}");
        Log.Information($"Using config preset: {preset}");
        
        return asProcess;
    }

    private void StopAssettoServer(Process serverProcess)
    {
        if (!_cancellationTokenSource.IsCancellationRequested)
            _cancellationTokenSource.Cancel();
        
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
    
    
    // MORE Process Handling
    
    /// https://stackoverflow.com/a/30517342
    /// <summary>
    /// Continuously copies data from one stream to the other.
    /// </summary>
    /// <param name="inStream">The input stream.</param>
    /// <param name="outStream">The output stream.</param>
    private void PassThrough(Stream inStream, Stream outStream)
    {
        var task = new Task(() =>
        {
            inStream.CopyToAsync(outStream);
        }, _cancellationTokenSource.Token);
        task.Start();
    }

    private void OutputReader(Process p)
    {
        var process = p;
        // Pass the standard output of the child to our standard output
        PassThrough(process.StandardOutput.BaseStream, Console.OpenStandardOutput());
    }

    private void ErrorReader(Process p)
    {
        var process = p;
        // Pass the standard error of the child to our standard error
        PassThrough(process.StandardError.BaseStream, Console.OpenStandardError());
    }

    private void InputReader(Process p)
    {
        var process = p;
        // Pass our standard input into the standard input of the child
        PassThrough(Console.OpenStandardInput(), process.StandardInput.BaseStream);
    }
}
