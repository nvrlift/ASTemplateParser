using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace nvrlift.AssettoServer.HostExtension;

public class PresetLoader : IDisposable
{
    private const string RegexPattern = @"^[\$([a-z0-9_-]+)]$";
    private readonly Dictionary<string, string> _config;
    private readonly string _templatePath;
    private readonly bool _useEnvVar;
    private readonly string _presetPath;
    
    public PresetLoader(string templatePath, bool useEnvVar)
    {
        _templatePath = templatePath;
        _useEnvVar = useEnvVar;
        var parent = Directory.GetParent(templatePath);
        if (parent == null) return;
        var basePath = parent.FullName;
        _presetPath = Path.Join(basePath, "presets");

        var cfgPath = Path.Join(templatePath, "template_cfg.json");
        if (Path.Exists(cfgPath))
        {
            var json = File.ReadAllText(cfgPath);
            _config = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
    }

    public void Load()
    {
        if (Path.Exists(_presetPath))
        {
            Directory.Move(_presetPath, $"{_presetPath}{DateTime.Now:yyyyMMdd}");
        }

        CopyFilesRecursively(new DirectoryInfo(_templatePath), new DirectoryInfo(_presetPath));
        
        UpdatePresets(_presetPath);
    }
    
    public void UpdatePresets(string path)
    {
        foreach (string file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
        {
            UpdateFile(file);
        }
    }

    public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target) {
        foreach (DirectoryInfo dir in source.GetDirectories())
            CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
        foreach (FileInfo file in source.GetFiles())
            file.CopyTo(Path.Combine(target.FullName, file.Name));
    }
    
    public void UpdateFile(string path)
    {
        var file = File.ReadAllText(path);
        var matches = Regex.Matches(file, RegexPattern, RegexOptions.IgnoreCase);
        Regex.Replace(file, RegexPattern, 
            m => TryGetValue(m.Groups[1].Value, out string variableValue) ? variableValue : m.Value);

        File.WriteAllText (path, file);
    }
    
    public bool TryGetValue(string property, out string result)
    {
        if (_useEnvVar)
        {
            var envVar = Environment.GetEnvironmentVariable(property);
            if (!string.IsNullOrEmpty(envVar))
            {
                result = envVar;
                return true;
            }

            result = "";
            return false;
        }

        var cfgVar = _config[property];
        if (!string.IsNullOrEmpty(cfgVar))
        {
            result = cfgVar;
            return true;
        }

        result = "";
        return false;
    }

    public void Dispose() { }
}
