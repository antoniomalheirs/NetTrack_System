using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using NetTrack.Application.Interfaces;
using NetTrack.Infrastructure.Services;
using NetTrack.Client.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace NetTrack.Client;

public partial class App : System.Windows.Application
{
    public static IHost? AppHost { get; private set; }

    public App()
    {
        // Configure Serilog
        var logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "nettrack-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("NetTrack starting...");

        AppHost = Host.CreateDefaultBuilder()
            .UseSerilog() // Use Serilog as the logging provider
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton<MainWindow>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<AlertViewModel>();
                services.AddSingleton<ICaptureService, CaptureService>();
                services.AddSingleton<IPacketParser, PacketParser>();
                services.AddSingleton<IConnectionTracker, ConnectionTracker>();
                services.AddSingleton<IPortMonitor, PortMonitor>();
                services.AddSingleton<IFilterService, FilterService>();
                services.AddSingleton<IStorageService, StorageService>();
                services.AddSingleton<IExportService, ExportService>();
                services.AddSingleton<IAlertService, AlertService>();
                services.AddSingleton<IDashboardService, DashboardService>();
                services.AddSingleton<ISessionReplayService, SessionReplayService>();
                services.AddSingleton<IPacketPipelineService, PacketPipelineService>();
            })
            .Build();

        // Global Exception Handling
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "A fatal UI exception occurred.");
        MessageBox.Show($"A terminal error occurred: {e.Exception.Message}\n\nCheck logs for details.", "NetTrack Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(-1);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        Log.Fatal(ex, "A fatal non-UI exception occurred.");
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load Styles programmatically to avoid XAML compiler bugs
        // Styles are now loaded via App.xaml
        // try { ... } catch { ... }

        try
        {
            // Start the host
            await AppHost!.StartAsync();

            var startupForm = AppHost.Services.GetRequiredService<MainWindow>();
            startupForm.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host failed to start.");
            MessageBox.Show($"Failed to initialize system services.\n\nError: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }

        // base.OnStartup(e); (Already called at top)
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("NetTrack shutting down...");
        await AppHost!.StopAsync();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

