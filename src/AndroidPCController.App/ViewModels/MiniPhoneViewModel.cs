using AndroidPCController.App.Controls;
using AndroidPCController.App.Services;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace AndroidPCController.App.ViewModels;

public partial class MiniPhoneViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IDeviceManager _deviceManager;
    private readonly ILogService _logService;
    private readonly Timer _batteryTimer;
    private bool _disposed;

    [ObservableProperty]
    private string _deviceName = "No Device";

    [ObservableProperty]
    private int _batteryLevel;

    [ObservableProperty]
    private string _batteryState = "Unknown";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isTopmost = true;

    [ObservableProperty]
    private bool _isScrcpyActive;

    private DeviceInfo? _currentDevice;
    private ScrcpyManager? _scrcpyManager;

    public event EventHandler<IntPtr>? ScrcpyWindowReady;

    public MiniPhoneViewModel(IDeviceManager deviceManager, ILogService logService)
    {
        _deviceManager = deviceManager;
        _logService = logService;

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;

        _batteryTimer = new Timer(async _ => await RefreshBatteryAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    [RelayCommand]
    private void ToggleTopmost()
    {
        IsTopmost = !IsTopmost;
    }

    [RelayCommand]
    private void CloseWindow()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var window in System.Windows.Application.Current.Windows)
            {
                if (window is MiniPhoneWindow miniWindow)
                {
                    miniWindow.Close();
                    break;
                }
            }
        });
    }

    public async Task StartMiniStreamAsync()
    {
        if (_currentDevice is null)
        {
            _logService.Warning("MiniPhone", "No device connected. Connect a device first.");
            return;
        }

        try
        {
            _scrcpyManager ??= App.Services.GetRequiredService<ScrcpyManager>();

            IntPtr hwnd;
            if (_scrcpyManager.IsRunning)
            {
                hwnd = _scrcpyManager.WindowHandle;
            }
            else
            {
                var options = new ScrcpyOptions
                {
                    Serial = _currentDevice.Serial,
                    MaxFps = 60,
                    BitRate = 12_000_000,
                    Codec = "H264",
                    MaxSize = 720,
                    LowLatency = true,
                    AudioEnabled = false
                };
                hwnd = await _scrcpyManager.StartAsync(options);
            }

            if (hwnd == IntPtr.Zero)
            {
                _logService.Error("MiniPhone", "scrcpy window could not be found.");
                return;
            }

            IsScrcpyActive = true;
            ScrcpyWindowReady?.Invoke(this, hwnd);
            _logService.Information("MiniPhone", $"Live screen attached for {_currentDevice.Serial}");
        }
        catch (Exception ex)
        {
            _logService.Error("MiniPhone", $"Failed to start live screen: {ex.Message}", ex);
        }
    }

    public void NotifyMiniClosed()
    {
        IsScrcpyActive = false;
        if (ScrcpyHost.LiveHosts.Count == 0)
        {
            _ = _scrcpyManager?.StopAsync();
        }
    }

    private async Task RefreshBatteryAsync()
    {
        if (_currentDevice is null || !IsConnected) return;

        try
        {
            var session = _deviceManager.GetSession(_currentDevice.Serial);
            if (session is null) return;

            var batteryInfo = await session.ExecuteShellCommandAsync("dumpsys battery");

            App.Current.Dispatcher.Invoke(() =>
            {
                foreach (var line in batteryInfo.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("level:"))
                    {
                        if (int.TryParse(trimmed.Split(':').Last().Trim(), out var level))
                            BatteryLevel = level;
                    }
                    else if (trimmed.StartsWith("status:"))
                    {
                        BatteryState = trimmed.Split(':').Last().Trim() switch
                        {
                            "2" => "Charging",
                            "3" => "Discharging",
                            "5" => "Full",
                            _ => "Unknown"
                        };
                    }
                }
            });
        }
        catch
        {
            // Silently fail on battery refresh
        }
    }

    private void OnDeviceConnected(object? sender, DeviceConnectedEventArgs e)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            _currentDevice = e.Device;
            DeviceName = e.Device.Model;
            IsConnected = true;
            BatteryLevel = e.Device.BatteryLevel ?? 0;
            _ = RefreshBatteryAsync();
        });
    }

    private void OnDeviceDisconnected(object? sender, DeviceDisconnectedEventArgs e)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            if (_currentDevice?.Serial == e.Serial)
            {
                _currentDevice = null;
                DeviceName = "No Device";
                IsConnected = false;
                BatteryLevel = 0;
                BatteryState = "Unknown";
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _batteryTimer.DisposeAsync();

        _deviceManager.DeviceConnected -= OnDeviceConnected;
        _deviceManager.DeviceDisconnected -= OnDeviceDisconnected;

        GC.SuppressFinalize(this);
    }
}
