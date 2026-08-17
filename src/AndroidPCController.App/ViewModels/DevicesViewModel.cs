using System.Collections.ObjectModel;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndroidPCController.App.ViewModels;

public partial class DevicesViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IDeviceManager _deviceManager;
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;
    private bool _disposed;

    private const string FriendlyNameKeyPrefix = "friendlyname_";

    [ObservableProperty]
    private ObservableCollection<DeviceInfo> _availableDevices = [];

    [ObservableProperty]
    private ObservableCollection<DeviceInfo> _connectedDevices = [];

    [ObservableProperty]
    private ObservableCollection<DeviceInfo> _filteredDevices = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private DeviceInfo? _selectedDevice;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private DeviceCapabilities? _selectedDeviceCapabilities;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public DevicesViewModel(IDeviceManager deviceManager, ISettingsService settingsService, ILogService logService)
    {
        _deviceManager = deviceManager;
        _settingsService = settingsService;
        _logService = logService;

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;

        _ = RefreshDevicesAsync();
    }

    [RelayCommand]
    private async Task RefreshDevicesAsync()
    {
        if (IsScanning) return;

        try
        {
            IsScanning = true;
            _logService.Information("Devices", "Scanning for devices...");

            var devices = await _deviceManager.GetAvailableDevicesAsync();

            foreach (var device in devices)
            {
                var friendlyName = _settingsService.Get(FriendlyNameKeyPrefix + device.Serial, string.Empty);
                if (!string.IsNullOrEmpty(friendlyName) && device.Model != friendlyName)
                {
                    device.Model = friendlyName;
                }
            }

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

                ApplyFilter();
            });

            StatusText = $"Found {devices.Count} device(s): {ConnectedDevices.Count} connected, {AvailableDevices.Count} available";
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

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredDevices.Clear();

        var query = SearchText?.Trim();
        var all = ConnectedDevices.Concat(AvailableDevices);

        foreach (var device in all)
        {
            if (string.IsNullOrEmpty(query) ||
                device.Serial.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                device.Model.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                device.Manufacturer.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredDevices.Add(device);
            }
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedDevice))]
    private async Task ConnectAsync()
    {
        if (SelectedDevice is null) return;

        if (ConnectedDevices.Contains(SelectedDevice))
        {
            StatusText = $"Already connected to {SelectedDevice.Model}. Open Phone Desktop to view the screen.";
            return;
        }

        try
        {
            IsScanning = true;
            StatusText = $"Connecting to {SelectedDevice.Model}...";
            _logService.Information("Devices", $"Connecting to {SelectedDevice.Model} ({SelectedDevice.Serial})...");

            await _deviceManager.ConnectAsync(SelectedDevice.Serial, SelectedDevice.ConnectionType);
            StatusText = $"Connected to {SelectedDevice.Model}";
            _logService.Information("Devices", $"Connected to {SelectedDevice.Model}");

            await RefreshDevicesAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Connection failed: {ex.Message}";
            _logService.Error("Devices", $"Connection failed: {ex.Message}", ex);
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedDevice))]
    private async Task DisconnectAsync()
    {
        if (SelectedDevice is null) return;

        if (!ConnectedDevices.Contains(SelectedDevice))
        {
            StatusText = $"{SelectedDevice.Model} is not connected.";
            return;
        }

        try
        {
            IsScanning = true;
            StatusText = $"Disconnecting {SelectedDevice.Model}...";
            _logService.Information("Devices", $"Disconnecting {SelectedDevice.Model}...");

            await _deviceManager.DisconnectAsync(SelectedDevice.Serial);
            StatusText = $"Disconnected {SelectedDevice.Model}";
            _logService.Information("Devices", $"Disconnected {SelectedDevice.Model}");

            SelectedDeviceCapabilities = null;
            await RefreshDevicesAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Disconnect failed: {ex.Message}";
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
            StatusText = "Connect the device first to rename it.";
            _logService.Warning("Devices", "Device not connected. Connect first to rename.");
            return;
        }

        var dialog = new Controls.InputDialog("Rename Device", $"Rename '{SelectedDevice.Model}':", SelectedDevice.Model)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (dialog.ShowDialog() != true) return;

        var newName = dialog.InputValue.Trim();
        if (string.IsNullOrEmpty(newName) || newName == SelectedDevice.Model) return;

        _settingsService.Set(FriendlyNameKeyPrefix + SelectedDevice.Serial, newName);
        SelectedDevice.Model = newName;
        StatusText = $"Renamed to {newName}";
        _logService.Information("Devices", $"Renamed {SelectedDevice.Serial} to {newName}");
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

            await RefreshDevicesAsync();
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

    private bool HasSelectedDevice => SelectedDevice is not null;

    private void OnDeviceConnected(object? sender, DeviceConnectedEventArgs e)
    {
        _logService.Information("Devices", $"Device connected: {e.Device.Model}");
        _ = RefreshDevicesAsync();
    }

    private void OnDeviceDisconnected(object? sender, DeviceDisconnectedEventArgs e)
    {
        _logService.Information("Devices", $"Device disconnected: {e.Serial}");
        _ = RefreshDevicesAsync();
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
