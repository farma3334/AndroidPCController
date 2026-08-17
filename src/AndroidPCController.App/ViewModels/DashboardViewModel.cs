using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Threading;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndroidPCController.App.ViewModels;

public partial class DashboardViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IDeviceManager _deviceManager;
    private readonly ILogService _logService;
    private readonly DispatcherTimer _statsTimer;
    private bool _disposed;
    private string _selectedSerial = string.Empty;
    private long _prevIdle;
    private long _prevTotal;

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

    [ObservableProperty]
    private double _cpuUsage;

    [ObservableProperty]
    private double _ramUsage;

    [ObservableProperty]
    private long _ramUsedBytes;

    [ObservableProperty]
    private long _ramTotalBytes;

    [ObservableProperty]
    private int _batteryLevel;

    [ObservableProperty]
    private string _batteryState = "Unknown";

    [ObservableProperty]
    private double _temperature;

    [ObservableProperty]
    private double _networkLatency;

    [ObservableProperty]
    private double _currentFps;

    [ObservableProperty]
    private string _currentApp = "N/A";

    [ObservableProperty]
    private string _connectionQuality = "Unknown";

    [ObservableProperty]
    private int _deviceHealthScore;

    [ObservableProperty]
    private long _storageUsedBytes;

    [ObservableProperty]
    private long _storageTotalBytes;

    [ObservableProperty]
    private bool _isDeviceConnected;

    [ObservableProperty]
    private string _deviceName = "No Device";

    [ObservableProperty]
    private string _connectionStatusText = "Disconnected";

    [ObservableProperty]
    private double _bandwidthMbps;

    public DashboardViewModel(IDeviceManager deviceManager, ILogService logService)
    {
        _deviceManager = deviceManager;
        _logService = logService;

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;

        _statsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _statsTimer.Tick += async (_, _) => await RefreshDeviceStatsAsync();
        _statsTimer.Start();

        _ = RefreshDevicesInternalAsync();
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

                if (_deviceManager.ActiveSessions.Count > 0)
                {
                    var session = _deviceManager.ActiveSessions[0];
                    var info = session.DeviceInfo;
                    SelectedDevice = info;
                    IsDeviceConnected = true;
                    DeviceName = $"{info.Manufacturer} {info.Model}";
                    ConnectionStatusText = info.ConnectionState;
                    _selectedSerial = info.Serial;
                }
                else
                {
                    SelectedDevice = null;
                    IsDeviceConnected = false;
                    DeviceName = "No Device";
                    ConnectionStatusText = "Disconnected";
                    _selectedSerial = string.Empty;
                }
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

    private async Task RefreshDeviceStatsAsync()
    {
        if (string.IsNullOrEmpty(_selectedSerial)) return;

        var session = _deviceManager.GetSession(_selectedSerial);
        if (session is null)
        {
            IsDeviceConnected = false;
            return;
        }

        IsDeviceConnected = true;
        DeviceName = $"{session.DeviceInfo.Manufacturer} {session.DeviceInfo.Model}";
        ConnectionStatusText = session.DeviceInfo.ConnectionState;

        try
        {
            PerformanceMetrics? metrics = null;
            try
            {
                metrics = await session.Diagnostics.GetPerformanceMetricsAsync(_selectedSerial);
            }
            catch
            {
                // Diagnostics service not available, fall back to ADB
            }

            if (metrics is not null)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CpuUsage = Math.Round(metrics.CpuUsage, 1);
                    RamUsedBytes = metrics.RamUsedBytes;
                    RamTotalBytes = metrics.RamTotalBytes;
                    RamUsage = metrics.RamTotalBytes > 0 ? Math.Round((double)metrics.RamUsedBytes / metrics.RamTotalBytes * 100, 1) : 0;
                    BatteryLevel = metrics.BatteryLevel;
                    BatteryState = metrics.BatteryState ?? "Unknown";
                    Temperature = Math.Round(metrics.Temperature, 1);
                    NetworkLatency = Math.Round(metrics.Latency.TotalMilliseconds, 0);
                    CurrentFps = Math.Round(metrics.CurrentFps, 1);
                    BandwidthMbps = Math.Round(metrics.NetworkMbps, 2);
                    ConnectionQuality = GetConnectionQuality(metrics.Latency.TotalMilliseconds);
                });
            }
            else
            {
                await FetchStatsFromAdb(session);
            }

            await FetchCurrentApp(session);
            await FetchStorageInfo(session);

            CalculateHealthScore();
        }
        catch (Exception ex)
        {
            _logService.Warning("Dashboard", $"Stats refresh error: {ex.Message}");
        }
    }

    private async Task FetchStatsFromAdb(IDeviceSession session)
    {
        try
        {
            var cpuOutput = await session.ExecuteShellCommandAsync("cat /proc/stat");
            ParseCpuStat(cpuOutput);
        }
        catch { }

        try
        {
            var memOutput = await session.ExecuteShellCommandAsync("cat /proc/meminfo");
            ParseMemInfo(memOutput);
        }
        catch { }

        try
        {
            var batteryOutput = await session.ExecuteShellCommandAsync("dumpsys battery");
            ParseBatteryInfo(batteryOutput);
        }
        catch { }
    }

    private void ParseCpuStat(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (!line.StartsWith("cpu ")) continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) continue;

            long idle = long.Parse(parts[4], CultureInfo.InvariantCulture);
            long total = 0;
            for (int i = 1; i < parts.Length; i++)
            {
                if (long.TryParse(parts[i], CultureInfo.InvariantCulture, out long val))
                    total += val;
            }

            if (_prevTotal > 0)
            {
                long deltaTotal = total - _prevTotal;
                long deltaIdle = idle - _prevIdle;
                if (deltaTotal > 0)
                {
                    double usage = (double)(deltaTotal - deltaIdle) / deltaTotal * 100;
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        CpuUsage = Math.Round(Math.Clamp(usage, 0, 100), 1));
                }
            }

            _prevIdle = idle;
            _prevTotal = total;
            break;
        }
    }

    private void ParseMemInfo(string output)
    {
        long memTotal = 0;
        long memAvail = 0;

        foreach (var line in output.Split('\n'))
        {
            if (line.StartsWith("MemTotal:"))
            {
                var val = ExtractKbValue(line);
                memTotal = val * 1024;
            }
            else if (line.StartsWith("MemAvailable:"))
            {
                var val = ExtractKbValue(line);
                memAvail = val * 1024;
            }
        }

        if (memTotal > 0)
        {
            long used = memTotal - memAvail;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                RamUsedBytes = used;
                RamTotalBytes = memTotal;
                RamUsage = Math.Round((double)used / memTotal * 100, 1);
            });
        }
    }

    private void ParseBatteryInfo(string output)
    {
        int level = -1;
        string? state = null;

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("level:"))
            {
                var val = trimmed.Substring(6).Trim();
                if (int.TryParse(val, out int lvl))
                    level = lvl;
            }
            else if (trimmed.StartsWith("status:"))
            {
                var val = trimmed.Substring(7).Trim();
                state = val switch
                {
                    "2" => "Charging",
                    "3" => "Discharging",
                    "4" => "Not charging",
                    "5" => "Full",
                    _ => val
                };
            }
            else if (trimmed.StartsWith("temperature:"))
            {
                var val = trimmed.Substring(12).Trim();
                if (double.TryParse(val, CultureInfo.InvariantCulture, out double temp))
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        Temperature = Math.Round(temp / 10.0, 1));
                }
            }
        }

        if (level >= 0 || state is not null)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (level >= 0) BatteryLevel = level;
                if (state is not null) BatteryState = state;
            });
        }
    }

    private async Task FetchCurrentApp(IDeviceSession session)
    {
        try
        {
            var output = await session.ExecuteShellCommandAsync("dumpsys activity activities | grep mResumedActivity");
            var trimmed = output.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                int lastSlash = trimmed.LastIndexOf('/');
                if (lastSlash > 0)
                {
                    int start = trimmed.LastIndexOf(' ', lastSlash) + 1;
                    int end = trimmed.IndexOf(' ', lastSlash);
                    string component = end > start
                        ? trimmed.Substring(start, end - start)
                        : trimmed.Substring(start);

                    int slashIdx = component.IndexOf('/');
                    string pkg = slashIdx > 0 ? component.Substring(0, slashIdx) : component;
                    var parts = pkg.Split('.');
                    string appName = parts.Length > 1 ? parts[^1] : pkg;

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        CurrentApp = appName);
                    return;
                }
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                CurrentApp = "N/A");
        }
        catch
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                CurrentApp = "N/A");
        }
    }

    private async Task FetchStorageInfo(IDeviceSession session)
    {
        try
        {
            var output = await session.ExecuteShellCommandAsync("df /data | tail -1");
            var parts = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4)
            {
                if (long.TryParse(parts[1], out long total) &&
                    long.TryParse(parts[2], out long used))
                {
                    long totalBytes = total * 1024;
                    long usedBytes = used * 1024;

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        StorageTotalBytes = totalBytes;
                        StorageUsedBytes = usedBytes;
                    });
                }
            }
        }
        catch { }
    }

    private void CalculateHealthScore()
    {
        int score = 100;

        if (CpuUsage > 90) score -= 25;
        else if (CpuUsage > 75) score -= 15;
        else if (CpuUsage > 60) score -= 5;

        if (BatteryLevel <= 10) score -= 20;
        else if (BatteryLevel <= 20) score -= 10;
        else if (BatteryLevel <= 30) score -= 5;

        if (Temperature > 45) score -= 25;
        else if (Temperature > 40) score -= 15;
        else if (Temperature > 35) score -= 5;

        if (RamUsage > 90) score -= 20;
        else if (RamUsage > 80) score -= 10;
        else if (RamUsage > 70) score -= 5;

        if (NetworkLatency > 200) score -= 15;
        else if (NetworkLatency > 100) score -= 10;
        else if (NetworkLatency > 50) score -= 5;

        DeviceHealthScore = Math.Clamp(score, 0, 100);
    }

    private static string GetConnectionQuality(double latencyMs)
    {
        if (latencyMs < 30) return "Excellent";
        if (latencyMs < 80) return "Good";
        if (latencyMs < 150) return "Fair";
        if (latencyMs < 300) return "Poor";
        return "Critical";
    }

    private static long ExtractKbValue(string line)
    {
        var parts = line.Split(':', 2);
        if (parts.Length < 2) return 0;

        var numStr = parts[1].Trim().Split(' ')[0];
        if (long.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out long val))
            return val;
        return 0;
    }

    [RelayCommand]
    private async Task TakeScreenshotAsync()
    {
        if (string.IsNullOrEmpty(_selectedSerial)) return;

        var session = _deviceManager.GetSession(_selectedSerial);
        if (session is null)
        {
            _logService.Warning("Dashboard", "No active session for the selected device.");
            return;
        }

        try
        {
            var downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "AndroidPCController");
            Directory.CreateDirectory(downloadDir);
            var path = await session.Screenshot.CaptureAndSaveAsync(downloadDir);
            _logService.Information("Dashboard", $"Screenshot saved: {path}");
        }
        catch (Exception ex)
        {
            _logService.Error("Dashboard", $"Screenshot failed: {ex.Message}", ex);
        }
    }

    public event EventHandler<string>? NavigateRequested;

    [RelayCommand]
    private void StartRecording()
    {
        if (string.IsNullOrEmpty(_selectedSerial))
        {
            _logService.Warning("Dashboard", "No device selected to record.");
            return;
        }

        _logService.Information("Dashboard", $"Recording started for {_selectedSerial}");
        NavigateRequested?.Invoke(this, "ScreenRecorder");
    }

    [RelayCommand]
    private void OpenTerminal()
    {
        _logService.Information("Dashboard", "Terminal requested");
        NavigateRequested?.Invoke(this, "Terminal");
    }

    [RelayCommand]
    private void OpenFileBrowser()
    {
        _logService.Information("Dashboard", "File browser requested");
        NavigateRequested?.Invoke(this, "Files");
    }

    [RelayCommand]
    private void OpenControl()
    {
        if (string.IsNullOrEmpty(_selectedSerial)) return;
        _logService.Information("Dashboard", $"Control requested for {_selectedSerial}");
        NavigateRequested?.Invoke(this, "Controller");
    }

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

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        _statsTimer.Stop();
        _deviceManager.DeviceConnected -= OnDeviceConnected;
        _deviceManager.DeviceDisconnected -= OnDeviceDisconnected;

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
