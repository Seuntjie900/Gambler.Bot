using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Threading.Tasks;
using Velopack;

namespace Gambler.Bot.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Initialise Serilog first so the global exception handlers below can write
        // to the log file even when the crash occurs before Avalonia is initialised.
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Warning()
#endif
            .WriteTo.File("gamblerbotlog.log")
            .CreateLogger();

        // On .NET 8, an unhandled exception on any ThreadPool thread (Task.Run body,
        // System.Timers.Timer callback, async void re-throw) does NOT propagate to
        // the try/catch below — it fires AppDomain.UnhandledException with
        // IsTerminating=true and terminates the process. On Windows Server 2022
        // WER dialogs are suppressed by group policy, so without this handler
        // the crash is completely silent.
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Log.Logger.Fatal(ex, "Unhandled CLR exception (terminating={IsTerminating})", e.IsTerminating);
            Log.CloseAndFlush();
        };

        // Faulted Tasks whose exceptions are never observed are silently swallowed.
        // Log them so they surface in the log file.
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Log.Logger.Error(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };

        try
        {
            // It's important to Run() the VelopackApp as early as possible in app startup.
            VelopackApp.Build()
                .Run();
            Log.Logger.Information("App starting");
            // Now it's time to run Avalonia
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            string message = "Unhandled exception: " + ex.ToString();
            Log.Logger.Fatal(ex, "Unhandled exception in Main");
            Log.CloseAndFlush();
            Console.WriteLine(message);
            throw;
        }

    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var config = new ConfigurationBuilder()
           .AddUserSecrets<Program>()           
           .Build();
        
        var builder =  AppBuilder.Configure<App>()                
              .UsePlatformDetect()
#if DEBUG
              .LogToTrace(Avalonia.Logging.LogEventLevel.Debug)
#else
              .LogToTrace(Avalonia.Logging.LogEventLevel.Warning)
#endif
              .WithInterFont()
              .LogToTrace()
              .UseReactiveUI()
              ;
        try
        {
            builder.RegisterActiproLicense(config.GetValue<string>("ActiproLicense:Licensee"), config.GetValue<string>("ActiproLicense:LisenceKey"));
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error registering Actipro license");
        }
        return builder;
    }
}
