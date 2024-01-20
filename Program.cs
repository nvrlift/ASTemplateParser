using System.Text.Json;
using JetBrains.Annotations;
using CommandLine;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace nvrlift.AssettoServer.TemplateParser;

internal static class Program
{
    [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
    private class Options
    {
        [Option('e',"use-env-vars", Required = false, HelpText = "Use environment variables when key is not found in config file (templates/template_cfg.json).")]
        public bool UseEnvironmentVariables { get; set; } = false;
        
        [Option('x', "delete-old-presets", Required = false, HelpText = "Should old presets/ be deleted instead of moved for backup.")]
        public bool DeleteOldPresets { get; set; } = false;
    }
    
    private static int Main(string[] args)
    {
        // Initialize logger
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .WriteTo.Console(theme: SystemConsoleTheme.Literate, applyThemeToRedirectedOutput: true)
            .WriteTo.File($"logs/restarter-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        var options = Parser.Default.ParseArguments<Options>(args).Value;
        if (options == null)
        {
            Log.Error($"Failed to create Options object.");
            return 2;
        }
        
        AppDomain.CurrentDomain.ProcessExit += (_,_) => ExitProcess();
        AppDomain.CurrentDomain.UnhandledException += (_, e) => OnUnhandledException(e);
        
        Log.Information($"Loading presets.");

        using TemplateLoader loader = new(Environment.CurrentDirectory, options.UseEnvironmentVariables, options.DeleteOldPresets);
        loader.Load();
        
        
        Console.WriteLine("Press any key to exit");
        Console.ReadKey();
        return 0;
    }

    private static void ExitProcess()
    {
        Log.CloseAndFlush();
    }

    private static void OnUnhandledException(UnhandledExceptionEventArgs args)
    {
        Log.Fatal((Exception)args.ExceptionObject, "Unhandled exception occurred");
        ExitProcess();
        Environment.Exit(1);
    }
}
