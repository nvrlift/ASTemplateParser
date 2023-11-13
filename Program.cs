using System.Text.Json;
using JetBrains.Annotations;
using CommandLine;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace nvrlift.AssettoServer.HostExtension;

internal static class Program
{
    [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
    private class Options
    {
        [Option('p', "preset", Required = false, SetName = "AssettoServer", HelpText = "Configuration preset")]
        public string Preset { get; set; } = "";
        
        [Option('d',"use-docker", Required = false, SetName = "AssettoServer", HelpText = "Used mainly for docker; Additionally load plugins from working directory")]
        public bool UseDocker { get; set; } = false;
        
        [Option('e',"use-env-vars", Required = false, HelpText = "Use environment variables instead of a config file (templates/template_cfg.json).")]
        public bool UseEnvironmentVariables { get; set; } = false;
        
        [Option('t',"use-template", Required = false, HelpText = "Use templates to create plugins from (environment) variables.")]
        public bool UseTemplate { get; set; } = false;
        
        [Option("debug", Required = false, HelpText = "Debug output.")]
        public bool Debug { get; set; } = false;
    }
    private static async Task Main(string[] args)
    {
        var options = Parser.Default.ParseArguments<Options>(args).Value;
        if (options == null) return;
        if (options.Preset.StartsWith('='))
            options.Preset = options.Preset[1..];
        
        var basePath = Environment.CurrentDirectory;
        
        // Initialize logger
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Preset", options.Preset)
            .WriteTo.Console(theme: SystemConsoleTheme.Literate, applyThemeToRedirectedOutput: true)
            .WriteTo.File($"logs/restarter-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        if (options.Debug)
            Log.Debug(JsonSerializer.Serialize(options));
        
        if (options.UseTemplate)
        {
            Log.Information($"Loading presets.");

            using TemplateLoader loader = new(basePath, options.UseEnvironmentVariables);
            loader.Load();
        }
        else
            Log.Information($"Preset loader skipped.");
        
        Log.Information($"Starting restart service.");
        
        RestartWatcher watcher = new(basePath, options.Preset, options.UseDocker, options.Debug);
        AppDomain.CurrentDomain.ProcessExit += (sender, e) => ProcessExit(sender, e, watcher);
        AppDomain.CurrentDomain.UnhandledException += (sender, e) => OnUnhandledException(sender, e, watcher);

        await watcher.RunAsync();
    }

    private static void ProcessExit(object? sender, EventArgs e, RestartWatcher watcher)
    {
        Log.CloseAndFlush();
        watcher.Exit();
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args, RestartWatcher watcher)
    {
        Log.Fatal((Exception)args.ExceptionObject, "Unhandled exception occurred");
        Log.CloseAndFlush();
        watcher.Exit();
        Environment.Exit(1);
    }
}
