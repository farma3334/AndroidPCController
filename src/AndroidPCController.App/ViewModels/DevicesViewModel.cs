using System.Collections.ObjectModel;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndroidPCController.App.ViewModels;

[ObservableObject]
public partial class DevicesViewModel : IAsyncDisposable
{
    private readonly IDeviceManager _deviceManager;
    private readonly ILogService _logService;
    private bool _disposed;

    [ObservableProperty]
    private ObservableCollection<DeviceInfo> _availableDevices = [];

    [ObservableProperty]
    private ObservableCollection<DeviceInfo> _connectedDevices = [];

    [ObservableProperty]
    private DeviceInfo? _selectedDevice;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private DeviceCapabilities? _selectedDeviceCapabilities;

    public DevicesViewModel(IDeviceManager deviceManager, ILogService logService)
    {
        _deviceManager = deviceManager;
        _logService = logService;

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;

        _ = ScanAsync();
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsScanning) return;

        try
        {
            IsScanning = true;
            _logService.Information("Devices", "Scanning for devices...");

            var devices = await _deviceManager.GetAvailableDevicesAsync();

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                AvailableDevices.Clear();
                ConnectedDevices.Clear();

                foreach (var device in devices)
                {
                    var session = _deviceManager.GetSession(device.Serial);
                    if (session is not null)
                    {
                        ConnectedDevices.Add(device);
                    }
                    else
                    {
                        AvailableDevices.Add(device);
                    }
                }
            });

            _logService.Information("Devices", $"Found {devices.Count} device(s): {ConnectedDevices.Count} connected, {AvailableDevices.Count} available");
        }
        catch (Exception ex)
        {
            _logService.Error("Devices", $"Scan failed: {ex.Message}", ex);
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedAvailableDevice))]
    private async Task ConnectAsync()
    {
        if (SelectedDevice is null) return;

        try
        {
            IsScanning = true;
            _logService.Information("Devices", $"Connecting to {SelectedDevice.Model} ({SelectedDevice.Serial})...");

            await _deviceManager.ConnectAsync(SelectedDevice.Serial, SelectedDevice.ConnectionType);
            _logService.Information("Devices", $"Connected to {SelectedDevice.Model}");

            await ScanAsync();
        }
        catch (Exception ex)
        {
            _logService.Error("Devices", $"Connection failed: {ex.Message}", ex);
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedConnectedDevice))]
    private async Task DisconnectAsync()
    {
        if (SelectedDevice is null) return;

        try
        {
            IsScanning = true;
            _logService.Information("Devices", $"Disconnecting {SelectedDevice.Model}...");

            await _deviceManager.DisconnectAsync(SelectedDevice.Serial);
            _logService.Information("Devices", $"Disconnected {SelectedDevice.Model}");

            SelectedDeviceCapabilities = null;
            await ScanAsync();
        }
        catch (Exception ex)
        {
            _logService.Error("Devices", $"Disconnect failed: {ex.Message}", ex);
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedDevice))]
    private async Task RenameAsync()
    {
        if (SelectedDevice is null) return;

        var session = _deviceManager.GetSession(SelectedDevice.Serial);
        if (session is null)
        {
            _logService.Warning("Devices", "Device not connected. Connect first to rename.");
            return;
        }

        _logService.Information("Devices", $"Rename requested for {SelectedDevice.Model} ({SelectedDevice.Serial})");
        await Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedDevice))]
    private async Task RefreshDeviceInfoAsync()
    {
        if (SelectedDevice is null) return;

        try
        {
            IsScanning = true;
            var session = _deviceManager.GetSession(SelectedDevice.Serial);

            if (session is not null)
            {
                await session.RefreshDeviceInfoAsync();
                SelectedDeviceCapabilities = session.Capabilities;
                _logService.Information("Devices", $"Refreshed info for {SelectedDevice.Model}");
            }
            else
            {
                var info = await _deviceManager.GetDeviceInfoAsync(SelectedDevice.Serial);
                if (info is not null)
                {
                    _logService.Information("Devices", $"Info refreshed for {info.Model} (not connected)");
                }
            }

            await ScanAsync();
        }
        catch (Exception ex)
        {
            _logService.Error("Devices", $"Refresh failed: {ex.Message}", ex);
        }
        finally
        {
            IsScanning = false;
        }
    }

    private bool HasSelectedAvailableDevice => SelectedDevice is not null && AvailableDevices.Contains(SelectedDevice);
    private bool HasSelectedConnectedDevice => SelectedDevice is not null && ConnectedDevices.Contains(SelectedDevice);
    private bool HasSelectedDevice => SelectedDevice is not null;

    private void OnDeviceConnected(object? sender, DeviceConnectedEventArgs e)
    {
        _logService.Information("Devices", $"Device connected: {e.Device.Model}");
        _ = ScanAsync();
    }

    private void OnDeviceDisconnected(object? sender, DeviceDisconnectedEventArgs e)
    {
        _logService.Information("Devices", $"Device disconnected: {e.Serial}");
        _ = ScanAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _deviceManager.DeviceConnected -= OnDeviceConnected;
        _deviceManager.DeviceDisconnected -= OnDeviceDisconnected;

        GC.SuppressFinalize(this);
    }
}
