using System.Text.Json;
using System.Text.RegularExpressions;
using Serilog;

namespace nvrlift.AssettoServer.TemplateParser;

public class TemplateLoader : IDisposable
{
    private const string RegexPattern = @"\[\$([a-z0-9_-]+)\]";
    private Dictionary<string, string>? _config;
    private readonly string _templatePath;
    private readonly bool _useEnvVar;
    private readonly bool _deleteOldPresets;
    private readonly string _presetPath;
    
    public TemplateLoader(string basePath, bool useEnvVar, bool deleteOldPresets)
    {
        _useEnvVar = useEnvVar;
        _deleteOldPresets = deleteOldPresets;
        _presetPath = Path.Join(basePath, "presets");
        _templatePath = Path.Join(basePath, "templates");
    }

    public void Load()
    {
        if (Path.Exists(_presetPath))
        {
            try
            {
                if (_deleteOldPresets)
                    Directory.Delete(_presetPath, true);
                else
                    Directory.Move(_presetPath, $"{_presetPath}{DateTime.Now:yyyyMMdd-HHmmss}");
            }
            catch
            {
                Log.Error($"Unable to move/delete old presets folder.");
            }
        }

        if (!Path.Exists(_templatePath))
        {
            Directory.CreateDirectory(_templatePath);
            Log.Error($"No template folder found.");
            return;
        }
        
        var cfgPath = Path.Join(_templatePath, "template_cfg.json");
        if (!Path.Exists(cfgPath))
        {
            Log.Error($"template_cfg.json not found.");
            return;
        }
        var json = File.ReadAllText(cfgPath);
        _config = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();

        Log.Information($"Starting to copy '{_templatePath}' into '{_presetPath}'...");
        CopyFilesRecursively(new DirectoryInfo(_templatePath), new DirectoryInfo(_presetPath));
        Log.Information($"Generating presets with templates finished.");
    }

    private void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target) {
        foreach (DirectoryInfo dir in source.GetDirectories())
            CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
        foreach (FileInfo file in source.GetFiles())
            CopyFile(file.DirectoryName!, target.FullName, file.Name);
    }
    
    private void CopyFile(string sourcePath, string targetPath, string fileName)
    {
        using var input = File.OpenText(Path.Join(sourcePath, fileName));
        using var output = new StreamWriter(Path.Join(targetPath, fileName));
        string? line;
        while (null != (line = input.ReadLine())) {
            var modifiedLine = Regex.Replace(line, RegexPattern,
                m => TryGetValue(m.Groups[1].Value, out string variableValue) ? variableValue : m.Value, RegexOptions.IgnoreCase);
                
            output.WriteLine(modifiedLine);
        }
    }

    private bool TryGetValue(string property, out string result)
    {
        if (_config!.TryGetValue(property, out var cfgVar))
        {
            if (!string.IsNullOrEmpty(cfgVar))
            {
                result = cfgVar;
                return true;
            }
        }
        else if (_useEnvVar)
        {
            var envVar = Environment.GetEnvironmentVariable(property);
            if (!string.IsNullOrEmpty(envVar))
            {
                result = envVar;
                return true;
            }

            Log.Warning($"Environment variable '{property}' not found.");
        }
        else
            Log.Warning($"Config variable '{property}' not found.");

        result = "";
        return false;
    }

    public void Dispose() { }
}
