using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using AndroidPCController.App.Pages;
using AndroidPCController.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AndroidPCController.App;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private readonly Services.TrayIconService _trayIconService;
    private bool _exiting;

    public MainWindow()
    {
        InitializeComponent();
        _trayIconService = App.Services.GetRequiredService<Services.TrayIconService>();
        Loaded += MainWindow_Loaded;
        StateChanged += MainWindow_StateChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.NavigationRequested += OnNavigationRequested;
        ViewModel.MinimizeRequested += OnMinimizeRequested;
        _trayIconService.StateChanged += OnTrayStateChanged;
        _trayIconService.RestoreRequested += OnTrayRestoreRequested;
        _trayIconService.ExitRequested += OnTrayExitRequested;
        NavigateToPage("Dashboard");

        var settings = App.Services.GetRequiredService<Core.Interfaces.ISettingsService>();
        var startMinimized = settings.Get<bool>(Core.Interfaces.SettingKeys.StartMinimized, false);
        if (startMinimized)
        {
            WindowState = WindowState.Minimized;
        }

        var minimizeToTray = settings.Get<bool>(Core.Interfaces.SettingKeys.MinimizeToTray, false);
        if (minimizeToTray)
        {
            _trayIconService.Enable();
        }
    }

    private void OnNavigationRequested(object? sender, string pageName)
    {
        NavigateToPage(pageName);
    }

    private void NavigateToPage(string pageName)
    {
        var services = App.Services;
        UserControl page = pageName switch
        {
            "Dashboard" => new DashboardPage { DataContext = services.GetRequiredService<DashboardViewModel>() },
            "Devices" => new DevicesPage { DataContext = services.GetRequiredService<DevicesViewModel>() },
            "Controller" => new ControllerPage { DataContext = services.GetRequiredService<ControllerViewModel>() },
            "Automation" => new AutomationPage { DataContext = services.GetRequiredService<AutomationViewModel>() },
            "Files" => new FilesPage { DataContext = services.GetRequiredService<FilesViewModel>() },
            "Apps" => new AppsPage { DataContext = services.GetRequiredService<AppsViewModel>() },
            "ScreenRecorder" => new ScreenRecorderPage { DataContext = services.GetRequiredService<ScreenRecorderViewModel>() },
            "Screenshots" => new ScreenshotsPage { DataContext = services.GetRequiredService<ScreenshotsViewModel>() },
            "Terminal" => new TerminalPage { DataContext = services.GetRequiredService<TerminalViewModel>() },
            "Logs" => new LogsPage { DataContext = services.GetRequiredService<LogsViewModel>() },
            "Developer" => new DeveloperPage { DataContext = services.GetRequiredService<DeveloperViewModel>() },
            "Settings" => new SettingsPage { DataContext = services.GetRequiredService<SettingsViewModel>() },
            "PhoneDesktop" => new PhoneDesktopPage { DataContext = services.GetRequiredService<PhoneDesktopViewModel>() },
            "Notifications" => new NotificationsPage { DataContext = services.GetRequiredService<NotificationsViewModel>() },
            _ => new DashboardPage { DataContext = services.GetRequiredService<DashboardViewModel>() }
        };

        if (page.DataContext is DashboardViewModel dashboard)
        {
            dashboard.NavigateRequested += OnDashboardNavigateRequested;
        }

        if ((ContentFrame.Content as UserControl)?.DataContext is NotificationsViewModel oldNotifications)
        {
            oldNotifications.Stop();
        }

        if (page.DataContext is NotificationsViewModel notifications)
        {
            notifications.Start();
        }

        ContentFrame.Content = page;
        PlaceholderText.Visibility = Visibility.Collapsed;
    }

    private void OnDashboardNavigateRequested(object? sender, string pageName)
    {
        NavigateToPage(pageName);
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (_trayIconService.IsEnabled && WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void OnMinimizeRequested(object? sender, EventArgs e)
    {
        if (_trayIconService.IsEnabled)
        {
            Hide();
        }
        else
        {
            WindowState = WindowState.Minimized;
        }
    }

    private void OnTrayStateChanged()
    {
        if (_trayIconService.IsEnabled && WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void OnTrayRestoreRequested()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OnTrayExitRequested()
    {
        _exiting = true;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MiniWindowButton_Click(object sender, RoutedEventArgs e)
    {
        var miniWindow = new MiniPhoneWindow();
        miniWindow.Show();
    }

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaximizeBtn.Content = "\uE922";
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeBtn.Content = "\uE923";
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_trayIconService.IsEnabled && !_exiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        ViewModel?.CleanupCommand.Execute(null);
        _trayIconService.Dispose();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Application.Current.Shutdown();
    }
}
