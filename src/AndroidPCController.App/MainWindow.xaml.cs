using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using AndroidPCController.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AndroidPCController.App;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;
    private bool _isMinimizedToTray;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        StateChanged += MainWindow_StateChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = App.Services.GetRequiredService<Core.Interfaces.ISettingsService>();
        var startMinimized = settings.Get<bool>(Core.Interfaces.SettingKeys.StartMinimized, false);
        if (startMinimized)
        {
            WindowState = WindowState.Minimized;
            Hide();
            _isMinimizedToTray = true;
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        var settings = App.Services.GetRequiredService<Core.Interfaces.ISettingsService>();
        var minimizeToTray = settings.Get<bool>(Core.Interfaces.SettingKeys.MinimizeToTray, true);

        if (WindowState == WindowState.Minimized && minimizeToTray)
        {
            Hide();
            _isMinimizedToTray = true;
        }
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
        var settings = App.Services.GetRequiredService<Core.Interfaces.ISettingsService>();
        var minimizeToTray = settings.Get<bool>(Core.Interfaces.SettingKeys.MinimizeToTray, true);

        if (minimizeToTray && !_isMinimizedToTray)
        {
            WindowState = WindowState.Minimized;
            Hide();
            _isMinimizedToTray = true;
            e.Cancel = true;
            return;
        }

        ViewModel?.CleanupCommand.Execute(null);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Application.Current.Shutdown();
    }
}
