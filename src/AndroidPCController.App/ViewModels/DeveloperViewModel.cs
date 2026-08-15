using System.IO;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndroidPCController.App.ViewModels;

[ObservableObject]
public partial class DeveloperViewModel : IAsyncDisposable
{
    private readonly IAdbClient _adbClient;
    private readonly IDeviceManager _deviceManager;
    private readonly ILogService _logService;
    private bool _disposed;
    private CancellationTokenSource? _logcatCts;
    private CancellationTokenSource? _recordingCts;

    [ObservableProperty]
    private DeviceInfo? _selectedDevice;

    [ObservableProperty]
    private bool _isDeviceConnected;

    [ObservableProperty]
    private string _logcatOutput = string.Empty;

    [ObservableProperty]
    private string _deviceInfoText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _androidVersion = "-";

    [ObservableProperty]
    private string _apiLevel = "-";

    [ObservableProperty]
    private string _buildNumber = "-";

    [ObservableProperty]
    private string _securityPatch = "-";

    [ObservableProperty]
    private string _kernelVersion = "-";

    [ObservableProperty]
    private string _serial = "-";

    [ObservableProperty]
    private bool _isLogcatRunning;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _packageName = string.Empty;

    public DeveloperViewModel(IAdbClient adbClient, IDeviceManager deviceManager, ILogService logService)
    {
        _adbClient = adbClient;
        _deviceManager = deviceManager;
        _logService = logService;

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;
    }

    [RelayCommand]
    private async Task RestartAdbAsync()
    {
        if (!IsDeviceConnected) return;
        try
        {
            StatusText = "Restarting ADB server...";
            await _adbClient.StopServerAsync();
            await _adbClient.StartServerAsync();
            StatusText = "ADB server restarted successfully";
            _logService.Information("Developer", "ADB server restarted");
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to restart ADB: {ex.Message}";
            _logService.Error("Developer", "ADB restart failed", ex);
        }
    }

    [RelayCommand]
    private async Task CaptureLogcatAsync()
    {
        if (SelectedDevice is null) return;
        try
        {
            StatusText = "Capturing logcat...";
            var log = await _adbClient.GetLogcatAsync(SelectedDevice.Serial, 5000);

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Logcat",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"logcat_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                await File.WriteAllTextAsync(dialog.FileName, log);
                StatusText = $"Logcat saved to {Path.GetFileName(dialog.FileName)}";
                _logService.Information("Developer", $"Logcat saved to {dialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to capture logcat: {ex.Message}";
            _logService.Error("Developer", "Logcat capture failed", ex);
        }
    }

    [RelayCommand]
    private async Task InstallApkAsync()
    {
        if (SelectedDevice is null) return;
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select APK to install",
                Filter = "APK files (*.apk)|*.apk|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                StatusText = $"Installing {Path.GetFileName(dialog.FileName)}...";
                await _adbClient.InstallApkAsync(SelectedDevice.Serial, dialog.FileName);
                StatusText = $"Installed {Path.GetFileName(dialog.FileName)} successfully";
                _logService.Information("Developer", $"APK installed: {dialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to install APK: {ex.Message}";
            _logService.Error("Developer", "APK install failed", ex);
        }
    }

    [RelayCommand]
    private async Task ClearAppDataAsync()
    {
        if (SelectedDevice is null || string.IsNullOrWhiteSpace(PackageName)) return;
        try
        {
            StatusText = $"Clearing data for {PackageName}...";
            await _adbClient.ClearAppDataAsync(SelectedDevice.Serial, PackageName);
            StatusText = $"Cleared data for {PackageName}";
            _logService.Information("Developer", $"Cleared app data: {PackageName}");
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to clear data: {ex.Message}";
            _logService.Error("Developer", "Clear app data failed", ex);
        }
    }

    [RelayCommand]
    private async Task ForceStopAsync()
    {
        if (SelectedDevice is null || string.IsNullOrWhiteSpace(PackageName)) return;
        try
        {
            StatusText = $"Force stopping {PackageName}...";
            await _adbClient.ForceStopAppAsync(SelectedDevice.Serial, PackageName);
            StatusText = $"Force stopped {PackageName}";
            _logService.Information("Developer", $"Force stopped: {PackageName}");
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to force stop: {ex.Message}";
            _logService.Error("Developer", "Force stop failed", ex);
        }
    }

    [RelayCommand]
    private async Task TakeScreenshotAsync()
    {
        if (SelectedDevice is null) return;
        try
        {
            StatusText = "Taking screenshot...";
            var data = await _adbClient.TakeScreenshotAsync(SelectedDevice.Serial);

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Screenshot",
                Filter = "PNG files (*.png)|*.png|All files (*.*)|*.*",
                FileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };

            if (dialog.ShowDialog() == true)
            {
                await File.WriteAllBytesAsync(dialog.FileName, data);
                StatusText = $"Screenshot saved to {Path.GetFileName(dialog.FileName)}";
                _logService.Information("Developer", $"Screenshot saved to {dialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to take screenshot: {ex.Message}";
            _logService.Error("Developer", "Screenshot failed", ex);
        }
    }

    [RelayCommand]
    private async Task ToggleRecordingAsync()
    {
        if (SelectedDevice is null) return;

        if (IsRecording)
        {
            _recordingCts?.Cancel();
            IsRecording = false;
            StatusText = "Recording stopped";
            return;
        }

        try
        {
            IsRecording = true;
            _recordingCts = new CancellationTokenSource();
            StatusText = "Recording screen...";

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Screen Recording",
                Filter = "MP4 files (*.mp4)|*.mp4|All files (*.*)|*.*",
                FileName = $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4"
            };

            if (dialog.ShowDialog() == true)
            {
                StatusText = "Recording... Click again to stop";
                _logService.Information("Developer", "Screen recording started");
            }
            else
            {
                IsRecording = false;
            }
        }
        catch (Exception ex)
        {
            IsRecording = false;
            StatusText = $"Recording failed: {ex.Message}";
            _logService.Error("Developer", "Recording failed", ex);
        }
    }

    [RelayCommand]
    private async Task PullBugReportAsync()
    {
        if (SelectedDevice is null) return;
        try
        {
            StatusText = "Pulling bug report (this may take a while)...";

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Bug Report",
                Filter = "ZIP files (*.zip)|*.zip|All files (*.*)|*.*",
                FileName = $"bugreport_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
            };

            if (dialog.ShowDialog() == true)
            {
                await _adbClient.ExecuteCommandAsync(SelectedDevice.Serial, "bugreport");
                StatusText = "Bug report pulled successfully";
                _logService.Information("Developer", "Bug report pulled");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to pull bug report: {ex.Message}";
            _logService.Error("Developer", "Bug report failed", ex);
        }
    }

    [RelayCommand]
    private async Task ListPackagesAsync()
    {
        if (SelectedDevice is null) return;
        try
        {
            StatusText = "Listing packages...";
            var result = await _adbClient.ExecuteCommandAsync(SelectedDevice.Serial, "pm list packages");
            LogcatOutput = result;
            StatusText = "Packages listed";
            _logService.Information("Developer", "Listed installed packages");
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to list packages: {ex.Message}";
            _logService.Error("Developer", "List packages failed", ex);
        }
    }

    [RelayCommand]
    private async Task ShowScreenDensityAsync()
    {
        if (SelectedDevice is null) return;
        try
        {
            StatusText = "Getting screen density...";
            var result = await _adbClient.ExecuteCommandAsync(SelectedDevice.Serial, "wm density");
            LogcatOutput = result;
            StatusText = "Screen density retrieved";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to get density: {ex.Message}";
            _logService.Error("Developer", "Get density failed", ex);
        }
    }

    [RelayCommand]
    private async Task ShowNetworkInfoAsync()
    {
        if (SelectedDevice is null) return;
        try
        {
            StatusText = "Getting network info...";
            var result = await _adbClient.ExecuteCommandAsync(SelectedDevice.Serial, "ifconfig");
            LogcatOutput = result;
            StatusText = "Network info retrieved";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to get network info: {ex.Message}";
            _logService.Error("Developer", "Get network info failed", ex);
        }
    }

    [RelayCommand]
    private async Task ShowProcessListAsync()
    {
        if (SelectedDevice is null) return;
        try
        {
            StatusText = "Getting process list...";
            var result = await _adbClient.ExecuteCommandAsync(SelectedDevice.Serial, "ps");
            LogcatOutput = result;
            StatusText = "Process list retrieved";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to get process list: {ex.Message}";
            _logService.Error("Developer", "Get process list failed", ex);
        }
    }

    [RelayCommand]
    private async Task StartLogcatAsync()
    {
        if (SelectedDevice is null || IsLogcatRunning) return;

        try
        {
            _logcatCts = new CancellationTokenSource();
            IsLogcatRunning = true;
            StatusText = "Live logcat started";

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!_logcatCts.Token.IsCancellationRequested)
                    {
                        var log = await _adbClient.GetLogcatAsync(SelectedDevice.Serial, 100, _logcatCts.Token);
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            LogcatOutput = log;
                        });
                        await Task.Delay(1000, _logcatCts.Token);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        StatusText = $"Logcat error: {ex.Message}";
                        IsLogcatRunning = false;
                    });
                }
            }, _logcatCts.Token);
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to start logcat: {ex.Message}";
            IsLogcatRunning = false;
        }
    }

    [RelayCommand]
    private void StopLogcat()
    {
        _logcatCts?.Cancel();
        IsLogcatRunning = false;
        StatusText = "Live logcat stopped";
    }

    [RelayCommand]
    private void ClearLogcat()
    {
        LogcatOutput = string.Empty;
        StatusText = "Logcat cleared";
    }

    private async Task LoadDeviceInfoAsync()
    {
        if (SelectedDevice is null) return;

        try
        {
            AndroidVersion = SelectedDevice.AndroidVersion ?? "-";
            ApiLevel = SelectedDevice.ApiLevel.ToString();
            BuildNumber = SelectedDevice.BuildNumber ?? "-";
            SecurityPatch = SelectedDevice.SecurityPatch ?? "-";
            KernelVersion = SelectedDevice.KernelVersion ?? "-";
            Serial = SelectedDevice.Serial;
        }
        catch (Exception ex)
        {
            _logService.Error("Developer", $"Failed to load device info: {ex.Message}", ex);
        }
    }

    private void OnDeviceConnected(object? sender, DeviceConnectedEventArgs e)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            SelectedDevice = e.Device;
            IsDeviceConnected = true;
            _ = LoadDeviceInfoAsync();
        });
    }

    private void OnDeviceDisconnected(object? sender, DeviceDisconnectedEventArgs e)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            if (SelectedDevice?.Serial == e.Serial)
            {
                SelectedDevice = null;
                IsDeviceConnected = false;
                AndroidVersion = "-";
                ApiLevel = "-";
                BuildNumber = "-";
                SecurityPatch = "-";
                KernelVersion = "-";
                Serial = "-";
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _logcatCts?.Cancel();
        _logcatCts?.Dispose();
        _recordingCts?.Cancel();
        _recordingCts?.Dispose();

        _deviceManager.DeviceConnected -= OnDeviceConnected;
        _deviceManager.DeviceDisconnected -= OnDeviceDisconnected;

        GC.SuppressFinalize(this);
    }
}
