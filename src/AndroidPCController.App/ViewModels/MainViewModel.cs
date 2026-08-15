using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AndroidPCController.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IAdbClient _adbClient;
    private readonly IDeviceManager _deviceManager;
    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;
    private CancellationTokenSource? _devicePollCts;

    [ObservableProperty]
    private string _currentPageTitle = "Dashboard";

    [ObservableProperty]
    private string _connectionStatusText = "No device connected";

    [ObservableProperty]
    private string _statusBarText = "Ready";

    [ObservableProperty]
    private bool _isDeviceConnected;

    [ObservableProperty]
    private DeviceInfo? _selectedDevice;

    [ObservableProperty]
    private string _adbVersion = string.Empty;

    public ObservableCollection<DeviceInfo> Devices { get; } = new();

    public MainViewModel()
    {
        _adbClient = App.Services.GetRequiredService<IAdbClient>();
        _deviceManager = App.Services.GetRequiredService<IDeviceManager>();
        _logService = App.Services.GetRequiredService<ILogService>();
        _settingsService = App.Services.GetRequiredService<ISettingsService>();

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;

        _ = InitializeAsync();
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        try
        {
            StatusBarText = "Initializing ADB connection...";

            var version = await _adbClient.GetVersionAsync();
            AdbVersion = version;
            _logService.Information("Main", $"ADB version: {version}");

            await RefreshDevicesAsync();
            StartDevicePolling();

            StatusBarText = $"Connected | ADB {AdbVersion}";
        }
        catch (Exception ex)
        {
            _logService.Error("Main", "Failed to initialize ADB", ex);
            StatusBarText = "ADB initialization failed. Check if ADB is installed.";
        }
    }

    [RelayCommand]
    private void Navigate(string page)
    {
        CurrentPageTitle = page;
        _logService.Debug("Navigation", $"Navigated to {page}");
    }

    [RelayCommand]
    private async Task RefreshDevicesAsync()
    {
        try
        {
            StatusBarText = "Scanning for devices...";
            var devices = await _deviceManager.GetAvailableDevicesAsync();

            Devices.Clear();
            foreach (var device in devices)
            {
                Devices.Add(device);
            }

            if (devices.Count > 0)
            {
                StatusBarText = $"Found {devices.Count} device(s)";
                _logService.Information("Main", $"Found {devices.Count} device(s)");
            }
            else
            {
                StatusBarText = "No devices found. Connect a device via USB or wireless.";
                IsDeviceConnected = false;
                ConnectionStatusText = "No device connected";
            }
        }
        catch (Exception ex)
        {
            _logService.Error("Main", "Failed to refresh devices", ex);
            StatusBarText = "Failed to scan devices";
        }
    }

    [RelayCommand]
    private async Task ConnectDeviceAsync(DeviceInfo? device)
    {
        if (device is null) return;

        try
        {
            StatusBarText = $"Connecting to {device.Model} ({device.Serial})...";
            var session = await _deviceManager.ConnectAsync(device.Serial, device.ConnectionType);

            SelectedDevice = device;
            IsDeviceConnected = true;
            ConnectionStatusText = $"Connected: {device.Model} ({device.Serial})";
            StatusBarText = $"Connected to {device.Model}";

            _logService.Information("Main", $"Connected to device: {device.Model} ({device.Serial})");
        }
        catch (Exception ex)
        {
            _logService.Error("Main", $"Failed to connect to {device.Serial}", ex);
            StatusBarText = $"Connection failed: {ex.Message}";
            IsDeviceConnected = false;
            ConnectionStatusText = "Connection failed";
        }
    }

    [RelayCommand]
    private async Task DisconnectDeviceAsync()
    {
        if (SelectedDevice is null) return;

        try
        {
            StatusBarText = $"Disconnecting from {SelectedDevice.Model}...";
            await _deviceManager.DisconnectAsync(SelectedDevice.Serial);

            SelectedDevice = null;
            IsDeviceConnected = false;
            ConnectionStatusText = "No device connected";
            StatusBarText = "Disconnected";

            _logService.Information("Main", "Device disconnected");
        }
        catch (Exception ex)
        {
            _logService.Error("Main", "Failed to disconnect device", ex);
            StatusBarText = $"Disconnect failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void MinimizeToTray()
    {
        _logService.Debug("Main", "Minimized to tray");
    }

    [RelayCommand]
    private void Cleanup()
    {
        _devicePollCts?.Cancel();
        _devicePollCts?.Dispose();

        _deviceManager.DeviceConnected -= OnDeviceConnected;
        _deviceManager.DeviceDisconnected -= OnDeviceDisconnected;

        _logService.Information("Main", "Application cleanup completed");
    }

    private void StartDevicePolling()
    {
        _devicePollCts?.Cancel();
        _devicePollCts = new CancellationTokenSource();

        _ = PollDevicesAsync(_devicePollCts.Token);
    }

    private async Task PollDevicesAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                await RefreshDevicesAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logService.Warning("Main", $"Device polling error: {ex.Message}");
            }
        }
    }

    private void OnDeviceConnected(object? sender, DeviceConnectedEventArgs e)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            if (!Devices.Any(d => d.Serial == e.Device.Serial))
            {
                Devices.Add(e.Device);
            }

            if (SelectedDevice is null)
            {
                SelectedDevice = e.Device;
                IsDeviceConnected = true;
                ConnectionStatusText = $"Connected: {e.Device.Model} ({e.Device.Serial})";
                StatusBarText = $"Device connected: {e.Device.Model}";
            }

            _logService.Information("Main", $"Device connected: {e.Device.Model} ({e.Device.Serial})");
        });
    }

    private void OnDeviceDisconnected(object? sender, DeviceDisconnectedEventArgs e)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            var device = Devices.FirstOrDefault(d => d.Serial == e.Serial);
            if (device is not null)
            {
                Devices.Remove(device);
            }

            if (SelectedDevice?.Serial == e.Serial)
            {
                SelectedDevice = null;
                IsDeviceConnected = false;
                ConnectionStatusText = "No device connected";
                StatusBarText = $"Device disconnected: {e.Serial}";
            }

            _logService.Information("Main", $"Device disconnected: {e.Serial}");
        });
    }
}
