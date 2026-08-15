using System.Collections.ObjectModel;
using System.IO;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndroidPCController.App.ViewModels;

[ObservableObject]
public partial class DashboardViewModel : IAsyncDisposable
{
    private readonly IDeviceManager _deviceManager;
    private readonly ILogService _logService;
    private readonly Timer _refreshTimer;
    private bool _disposed;

    [ObservableProperty]
    private ObservableCollection<DeviceInfo> _devices = [];

    [ObservableProperty]
    private DeviceInfo? _selectedDevice;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _connectedCount;

    [ObservableProperty]
    private int _usbCount;

    [ObservableProperty]
    private int _wirelessCount;

    public DashboardViewModel(IDeviceManager deviceManager, ILogService logService)
    {
        _deviceManager = deviceManager;
        _logService = logService;

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;

        _refreshTimer = new Timer(async _ => await RefreshDevicesInternalAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    [RelayCommand]
    private async Task RefreshDevicesAsync()
    {
        await RefreshDevicesInternalAsync();
    }

    private async Task RefreshDevicesInternalAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            var devices = await _deviceManager.GetAvailableDevicesAsync();

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Devices.Clear();
                foreach (var device in devices)
                {
                    Devices.Add(device);
                }

                ConnectedCount = _deviceManager.ActiveSessions.Count;
                UsbCount = devices.Count(d => d.ConnectionType == ConnectionType.Usb);
                WirelessCount = devices.Count(d => d.ConnectionType == ConnectionType.Wireless);
            });
        }
        catch (Exception ex)
        {
            _logService.Error("Dashboard", $"Failed to refresh devices: {ex.Message}", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedDevice))]
    private async Task ConnectDeviceAsync()
    {
        if (SelectedDevice is null) return;

        try
        {
            IsLoading = true;
            await _deviceManager.ConnectAsync(SelectedDevice.Serial, SelectedDevice.ConnectionType);
            _logService.Information("Dashboard", $"Connected to {SelectedDevice.Model} ({SelectedDevice.Serial})");
            await RefreshDevicesInternalAsync();
        }
        catch (Exception ex)
        {
            _logService.Error("Dashboard", $"Failed to connect to {SelectedDevice?.Serial}: {ex.Message}", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedDevice))]
    private async Task OpenControlAsync()
    {
        if (SelectedDevice is null) return;

        var session = _deviceManager.GetSession(SelectedDevice.Serial);
        if (session is null)
        {
            _logService.Warning("Dashboard", "Device not connected. Connect first.");
            return;
        }

        _logService.Information("Dashboard", $"Opening control for {SelectedDevice.Model}");
        await Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedDevice))]
    private async Task OpenFilesAsync()
    {
        if (SelectedDevice is null) return;

        var session = _deviceManager.GetSession(SelectedDevice.Serial);
        if (session is null)
        {
            _logService.Warning("Dashboard", "Device not connected. Connect first.");
            return;
        }

        _logService.Information("Dashboard", $"Opening files for {SelectedDevice.Model}");
        await Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedDevice))]
    private async Task OpenDetailsAsync()
    {
        if (SelectedDevice is null) return;

        _logService.Information("Dashboard", $"Opening details for {SelectedDevice.Model}");
        await Task.CompletedTask;
    }

    private bool HasSelectedDevice => SelectedDevice is not null;

    private void OnDeviceConnected(object? sender, DeviceConnectedEventArgs e)
    {
        _logService.Information("Dashboard", $"Device connected: {e.Device.Model} ({e.Device.Serial})");
        _ = RefreshDevicesInternalAsync();
    }

    private void OnDeviceDisconnected(object? sender, DeviceDisconnectedEventArgs e)
    {
        _logService.Information("Dashboard", $"Device disconnected: {e.Serial} - {e.Reason}");
        _ = RefreshDevicesInternalAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _deviceManager.DeviceConnected -= OnDeviceConnected;
        _deviceManager.DeviceDisconnected -= OnDeviceDisconnected;

        await _refreshTimer.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
