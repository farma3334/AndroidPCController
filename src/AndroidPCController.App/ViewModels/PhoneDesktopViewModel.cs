using System.Collections.ObjectModel;
using AndroidPCController.App.Models;
using AndroidPCController.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace AndroidPCController.App.ViewModels;

[ObservableObject]
public partial class PhoneDesktopViewModel
{
    private readonly IAdbClient _adbClient;
    private readonly IDeviceManager _deviceManager;
    private readonly ILogService _logService;

    [ObservableProperty]
    private string _deviceName = "No device connected";

    [ObservableProperty]
    private string _batteryLevel = "--";

    [ObservableProperty]
    private string _connectionStatus = "Disconnected";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _currentTime = DateTime.Now.ToString("HH:mm");

    [ObservableProperty]
    private string _currentDate = DateTime.Now.ToString("ddd, MMM d");

    public ObservableCollection<AppTile> InstalledApps { get; } = new();

    public PhoneDesktopViewModel()
    {
        _adbClient = App.Services.GetRequiredService<IAdbClient>();
        _deviceManager = App.Services.GetRequiredService<IDeviceManager>();
        _logService = App.Services.GetRequiredService<ILogService>();

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;

        LoadDefaultApps();
        RefreshDeviceInfo();
    }

    [RelayCommand]
    private void RefreshApps()
    {
        LoadDefaultApps();
        RefreshDeviceInfo();
        _logService.Debug("PhoneDesktop", "App grid refreshed");
    }

    [RelayCommand]
    private async Task LaunchAppAsync(string? packageName)
    {
        if (string.IsNullOrEmpty(packageName)) return;

        var session = _deviceManager.ActiveSessions.FirstOrDefault();
        if (session is null)
        {
            _logService.Warning("PhoneDesktop", "No device connected. Cannot launch app.");
            return;
        }

        try
        {
            var command = $"monkey -p {packageName} -c android.intent.category.LAUNCHER 1";
            await _adbClient.ExecuteCommandAsync(session.Serial, command);
            _logService.Information("PhoneDesktop", $"Launched app: {packageName}");
        }
        catch (Exception ex)
        {
            _logService.Error("PhoneDesktop", $"Failed to launch {packageName}: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task GoHomeAsync()
    {
        var session = _deviceManager.ActiveSessions.FirstOrDefault();
        if (session is null) return;

        try
        {
            await _adbClient.ExecuteCommandAsync(session.Serial, "input keyevent KEYCODE_HOME");
            _logService.Debug("PhoneDesktop", "Sent HOME key event");
        }
        catch (Exception ex)
        {
            _logService.Error("PhoneDesktop", $"Failed to send HOME: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        var session = _deviceManager.ActiveSessions.FirstOrDefault();
        if (session is null) return;

        try
        {
            await _adbClient.ExecuteCommandAsync(session.Serial, "input keyevent KEYCODE_BACK");
            _logService.Debug("PhoneDesktop", "Sent BACK key event");
        }
        catch (Exception ex)
        {
            _logService.Error("PhoneDesktop", $"Failed to send BACK: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task OpenRecentsAsync()
    {
        var session = _deviceManager.ActiveSessions.FirstOrDefault();
        if (session is null) return;

        try
        {
            await _adbClient.ExecuteCommandAsync(session.Serial, "input keyevent KEYCODE_APP_SWITCH");
            _logService.Debug("PhoneDesktop", "Sent APP_SWITCH key event");
        }
        catch (Exception ex)
        {
            _logService.Error("PhoneDesktop", $"Failed to open recents: {ex.Message}", ex);
        }
    }

    private void LoadDefaultApps()
    {
        InstalledApps.Clear();

        var apps = new List<AppTile>
        {
            new() { PackageName = "com.whatsapp", AppName = "WhatsApp", IconChar = "W", IconColor = "#25D366" },
            new() { PackageName = "com.android.chrome", AppName = "Chrome", IconChar = "C", IconColor = "#4285F4" },
            new() { PackageName = "com.google.android.youtube", AppName = "YouTube", IconChar = "Y", IconColor = "#FF0000" },
            new() { PackageName = "com.android.camera", AppName = "Camera", IconChar = "\U0001F4F7", IconColor = "#FF5722" },
            new() { PackageName = "com.google.android.apps.photos", AppName = "Gallery", IconChar = "\U0001F5BC", IconColor = "#9C27B0" },
            new() { PackageName = "com.android.filemanager", AppName = "Files", IconChar = "\U0001F4C1", IconColor = "#607D8B" },
            new() { PackageName = "com.discord", AppName = "Discord", IconChar = "D", IconColor = "#5865F2" },
            new() { PackageName = "com.android.settings", AppName = "Settings", IconChar = "\u2699", IconColor = "#757575" },
            new() { PackageName = "com.google.android.gm", AppName = "Gmail", IconChar = "G", IconColor = "#EA4335" },
            new() { PackageName = "com.google.android.apps.maps", AppName = "Maps", IconChar = "M", IconColor = "#34A853" },
            new() { PackageName = "com.google.android.calendar", AppName = "Calendar", IconChar = "\U0001F4C5", IconColor = "#4285F4" },
            new() { PackageName = "com.google.android.keep", AppName = "Notes", IconChar = "\U0001F4DD", IconColor = "#FBBC04" },
            new() { PackageName = "com.google.android.apps.youtube.music", AppName = "Music", IconChar = "\u266B", IconColor = "#FF6D00" },
            new() { PackageName = "com.google.android.calculator", AppName = "Calculator", IconChar = "C", IconColor = "#00BCD4" },
            new() { PackageName = "com.google.android.deskclock", AppName = "Clock", IconChar = "\U0001F550", IconColor = "#673AB7" },
            new() { PackageName = "com.google.android.dialer", AppName = "Phone", IconChar = "\U0001F4DE", IconColor = "#4CAF50" },
        };

        foreach (var app in apps)
        {
            InstalledApps.Add(app);
        }
    }

    private void RefreshDeviceInfo()
    {
        var session = _deviceManager.ActiveSessions.FirstOrDefault();
        if (session is not null)
        {
            DeviceName = session.DeviceInfo.Model;
            IsConnected = true;
            ConnectionStatus = $"Connected · {session.DeviceInfo.Serial}";
            _ = UpdateBatteryAsync(session.Serial);
        }
        else
        {
            DeviceName = "No device connected";
            IsConnected = false;
            ConnectionStatus = "Disconnected";
            BatteryLevel = "--";
        }

        CurrentTime = DateTime.Now.ToString("HH:mm");
        CurrentDate = DateTime.Now.ToString("ddd, MMM d");
    }

    private async Task UpdateBatteryAsync(string serial)
    {
        try
        {
            var batteryInfo = await _adbClient.GetBatteryInfoAsync(serial);
            if (batteryInfo.Contains("level"))
            {
                var levelStart = batteryInfo.IndexOf("level:") + 6;
                if (levelStart > 6)
                {
                    var levelStr = batteryInfo[levelStart..].Trim().Split(' ')[0].Trim();
                    if (int.TryParse(levelStr, out var level))
                    {
                        BatteryLevel = $"{level}";
                    }
                }
            }
        }
        catch
        {
            BatteryLevel = "--";
        }
    }

    private void OnDeviceConnected(object? sender, DeviceConnectedEventArgs e)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            DeviceName = e.Device.Model;
            IsConnected = true;
            ConnectionStatus = $"Connected · {e.Device.Serial}";
            _ = UpdateBatteryAsync(e.Device.Serial);
        });
    }

    private void OnDeviceDisconnected(object? sender, DeviceDisconnectedEventArgs e)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            DeviceName = "No device connected";
            IsConnected = false;
            ConnectionStatus = "Disconnected";
            BatteryLevel = "--";
        });
    }
}
