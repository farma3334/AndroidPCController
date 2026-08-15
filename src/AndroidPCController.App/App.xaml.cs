using System.Windows;
using System.Windows.Threading;
using AndroidPCController.Adb;
using AndroidPCController.App.ViewModels;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Devices;
using AndroidPCController.Infrastructure;
using AndroidPCController.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AndroidPCController.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public static IServiceProvider Services => Current switch
    {
        App app => app._serviceProvider ?? throw new InvalidOperationException("Services not initialized."),
        _ => throw new InvalidOperationException("Application not running.")
    };

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var settings = _serviceProvider.GetRequiredService<ISettingsService>();
        settings.Load();

        var logService = _serviceProvider.GetRequiredService<ILogService>();
        logService.Information("App", "Application starting up.");

        var mainWindow = new MainWindow
        {
            DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        else
        {
            _serviceProvider?.Dispose();
        }

        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
        });

        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ISecurityService, SecurityService>();
        services.AddSingleton<IAdbClient, AdbClient>();
        services.AddSingleton<IDeviceManager, DeviceManager>();

        services.AddTransient<IDeviceSession>(sp =>
        {
            var deviceManager = sp.GetRequiredService<IDeviceManager>();
            return deviceManager.ActiveSessions.FirstOrDefault()
                ?? throw new InvalidOperationException("No active device sessions.");
        });

        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<DevicesViewModel>();
        services.AddTransient<ControllerViewModel>();
        services.AddTransient<FilesViewModel>();
        services.AddTransient<AppsViewModel>();
        services.AddTransient<TerminalViewModel>();
        services.AddTransient<LogsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ScreenshotsViewModel>();
        services.AddTransient<ScreenRecorderViewModel>();
        services.AddTransient<DeveloperViewModel>();
        services.AddSingleton<MiniPhoneViewModel>();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var logService = _serviceProvider?.GetService<ILogService>();
        logService?.Critical("App", $"Unhandled UI exception: {e.Exception.Message}", e.Exception);

        Console.WriteLine($"UNHANDLED UI EXCEPTION: {e.Exception}");

        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        var logService = _serviceProvider?.GetService<ILogService>();
        logService?.Critical("App", $"Unhandled domain exception: {exception?.Message}", exception);

        Dispatcher.Invoke(() =>
        {
            MessageBox.Show(
                $"A critical error occurred:\n\n{exception?.Message ?? "Unknown error"}\n\nThe application will now close.",
                "Critical Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown();
        });
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var logService = _serviceProvider?.GetService<ILogService>();
        logService?.Error("App", $"Unobserved task exception: {e.Exception.Message}", e.Exception);
        e.SetObserved();
    }
}
