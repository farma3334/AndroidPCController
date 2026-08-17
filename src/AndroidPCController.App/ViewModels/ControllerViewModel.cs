using System.IO;
using AndroidPCController.App.Services;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace AndroidPCController.App.ViewModels;

public partial class ControllerViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IDeviceManager _deviceManager;
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;
    private IDeviceSession? _currentSession;
    private bool _disposed;
    private Timer? _durationTimer;
    private DeviceOrientation _currentOrientation = DeviceOrientation.Portrait;

    [ObservableProperty]
    private DeviceInfo? _selectedDevice;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private int _currentFps;

    [ObservableProperty]
    private int _currentBitrate;

    [ObservableProperty]
    private int _streamWidth;

    [ObservableProperty]
    private int _streamHeight;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private TimeSpan _recordingDuration;

    [ObservableProperty]
    private int _streamFps = 60;

    [ObservableProperty]
    private int _streamMaxWidth = 1920;

    [ObservableProperty]
    private int _streamMaxHeight = 1080;

    [ObservableProperty]
    private int _streamBitrate = 8_000_000;

    [ObservableProperty]
    private string _streamCodec = "H264";

    [ObservableProperty]
    private int _bitrateMbps = 8;

    [ObservableProperty]
    private string _resolution = "Native";

    [ObservableProperty]
    private bool _lowLatency = true;

    [ObservableProperty]
    private bool _audioEnabled;

    [ObservableProperty]
    private bool _isScrcpyActive;

    [ObservableProperty]
    private string _liveFpsText = "--";

    [ObservableProperty]
    private string _liveBitrateText = "--";

    [ObservableProperty]
    private string _liveResolutionText = "--";

    [ObservableProperty]
    private string _liveOverlayText = "";

    public event EventHandler<IntPtr>? ScrcpyWindowReady;

    public event EventHandler? ScrcpyStopped;

    public bool HasDeviceSession => _currentSession is not null;

    private ScrcpyManager? _scrcpyManager;

    public IReadOnlyList<string> AvailableCodecs { get; } = ["H264", "H265"];

    public IReadOnlyList<string> AvailableResolutions { get; } = ["Native", "1920x1080", "1280x720", "800x480"];

    private const int KeycodePower = 26;
    private const int KeycodeVolumeUp = 24;
    private const int KeycodeVolumeDown = 25;

    public ControllerViewModel(IDeviceManager deviceManager, ISettingsService settingsService, ILogService logService)
    {
        _deviceManager = deviceManager;
        _settingsService = settingsService;
        _logService = logService;

        LoadSettings();

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;

        SetSession(_deviceManager.ActiveSessions.FirstOrDefault());
    }

    private void LoadSettings()
    {
        StreamFps = _settingsService.Get(SettingKeys.DefaultFps, 60);
        StreamBitrate = _settingsService.Get(SettingKeys.DefaultBitrate, 12_000_000);
        StreamCodec = _settingsService.Get(SettingKeys.DefaultCodec, "H264");
        BitrateMbps = StreamBitrate / 1_000_000;

        var resolution = _settingsService.Get(SettingKeys.DefaultResolution, "Native");
        Resolution = resolution;
        if (resolution != "Native" && resolution.Contains('x'))
        {
            var parts = resolution.Split('x');
            if (int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
            {
                StreamMaxWidth = w;
                StreamMaxHeight = h;
            }
        }
    }

    partial void OnBitrateMbpsChanged(int value) => StreamBitrate = value * 1_000_000;

    partial void OnResolutionChanged(string value)
    {
        if (value == "Native" || !value.Contains('x')) return;
        var parts = value.Split('x');
        if (int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
        {
            StreamMaxWidth = w;
            StreamMaxHeight = h;
        }
    }

    public void SetSession(IDeviceSession? session)
    {
        _currentSession = session;
        SelectedDevice = session?.DeviceInfo;
    }

    [RelayCommand]
    private async Task StartStreamAsync()
    {
        if (_currentSession is null)
        {
            _logService.Warning("Controller", "No device session. Connect a device first.");
            return;
        }

        if (IsStreaming) return;

        await StartScrcpyAsync();
    }

    [RelayCommand]
    public async Task StopStreamAsync()
    {
        if (!IsScrcpyActive) return;
        await StopScrcpyAsync();
    }

    [RelayCommand]
    private async Task TakeScreenshotAsync()
    {
        if (_currentSession is null)
        {
            _logService.Warning("Controller", "No device session. Connect a device first.");
            return;
        }

        try
        {
            var downloadDir = _settingsService.Get(SettingKeys.DownloadDirectory, string.Empty);
            if (string.IsNullOrEmpty(downloadDir))
            {
                downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "AndroidPCController");
            }

            Directory.CreateDirectory(downloadDir);
            var path = await _currentSession.Screenshot.CaptureAndSaveAsync(downloadDir);
            _logService.Information("Controller", $"Screenshot saved: {path}");
        }
        catch (Exception ex)
        {
            _logService.Error("Controller", $"Screenshot failed: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task StartRecordingAsync()
    {
        if (_currentSession is null || IsRecording) return;

        try
        {
            var downloadDir = _settingsService.Get(SettingKeys.DownloadDirectory, string.Empty);
            if (string.IsNullOrEmpty(downloadDir))
            {
                downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "AndroidPCController");
            }

            var settings = new RecordingSettings
            {
                Fps = StreamFps,
                Bitrate = StreamBitrate,
                Codec = StreamCodec,
                RecordAudio = AudioEnabled,
                OutputDirectory = downloadDir
            };

            _currentSession.ScreenRecorder.RecordingStateChanged += OnRecordingStateChanged;
            await _currentSession.ScreenRecorder.StartAsync(settings);

            _durationTimer = new Timer(_ =>
            {
                if (_currentSession?.ScreenRecorder is { IsRecording: true })
                {
                    RecordingDuration = _currentSession.ScreenRecorder.CurrentDuration;
                }
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

            _logService.Information("Controller", "Recording started");
        }
        catch (Exception ex)
        {
            _logService.Error("Controller", $"Failed to start recording: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task StopRecordingAsync()
    {
        if (_currentSession is null || !IsRecording) return;

        try
        {
            if (_durationTimer is not null)
            {
                await _durationTimer.DisposeAsync();
                _durationTimer = null;
            }

            await _currentSession.ScreenRecorder.StopAsync();
            _logService.Information("Controller", $"Recording stopped. Duration: {RecordingDuration:hh\\:mm\\:ss}");
        }
        catch (Exception ex)
        {
            _logService.Error("Controller", $"Failed to stop recording: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task PressBackAsync()
    {
        if (_currentSession is null) return;
        try { await _currentSession.InputController.PressBackAsync(); }
        catch (Exception ex) { _logService.Error("Controller", $"Back press failed: {ex.Message}", ex); }
    }

    [RelayCommand]
    private async Task PressHomeAsync()
    {
        if (_currentSession is null) return;
        try { await _currentSession.InputController.PressHomeAsync(); }
        catch (Exception ex) { _logService.Error("Controller", $"Home press failed: {ex.Message}", ex); }
    }

    [RelayCommand]
    private async Task PressRecentAsync()
    {
        if (_currentSession is null) return;
        try { await _currentSession.InputController.PressRecentAppsAsync(); }
        catch (Exception ex) { _logService.Error("Controller", $"Recent apps failed: {ex.Message}", ex); }
    }

    [RelayCommand]
    private void OpenKeyboard()
    {
        if (_scrcpyManager is null || !IsScrcpyActive)
        {
            _logService.Warning("Controller", "Start the stream first to use the keyboard");
            return;
        }

        _scrcpyManager.FocusWindow();
        _logService.Information("Controller", "Keyboard input focused on the live screen");
    }

    [RelayCommand]
    private async Task RotateScreenAsync()
    {
        if (_currentSession is null) return;
        try
        {
            _currentOrientation = _currentOrientation switch
            {
                DeviceOrientation.Portrait => DeviceOrientation.Landscape,
                DeviceOrientation.Landscape => DeviceOrientation.ReversePortrait,
                DeviceOrientation.ReversePortrait => DeviceOrientation.ReverseLandscape,
                _ => DeviceOrientation.Portrait
            };
            await _currentSession.InputController.RotateScreenAsync(_currentOrientation);
        }
        catch (Exception ex) { _logService.Error("Controller", $"Rotate failed: {ex.Message}", ex); }
    }

    [RelayCommand]
    private async Task ScreenshotButtonAsync()
    {
        await TakeScreenshotAsync();
    }

    [RelayCommand]
    private async Task RecordButtonAsync()
    {
        if (IsRecording)
            await StopRecordingAsync();
        else
            await StartRecordingAsync();
    }

    [RelayCommand]
    private async Task OpenClipboardAsync()
    {
        if (_currentSession is null) return;

        try
        {
            var content = await _currentSession.Clipboard.GetClipboardTextAsync() ?? string.Empty;
            var dialog = new Controls.ClipboardDialog(content)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                await _currentSession.Clipboard.SetClipboardTextAsync(dialog.ClipboardText);
                _logService.Information("Controller", "Clipboard synced to device");
            }
        }
        catch (Exception ex) { _logService.Error("Controller", $"Clipboard failed: {ex.Message}", ex); }
    }

    [RelayCommand]
    private async Task PressPowerAsync()
    {
        if (_currentSession is null) return;
        try { await _currentSession.InputController.SendKeyEventAsync(KeycodePower); }
        catch (Exception ex) { _logService.Error("Controller", $"Power press failed: {ex.Message}", ex); }
    }

    [RelayCommand]
    private async Task VolumeUpAsync()
    {
        if (_currentSession is null) return;
        try { await _currentSession.InputController.SendKeyEventAsync(KeycodeVolumeUp); }
        catch (Exception ex) { _logService.Error("Controller", $"Volume up failed: {ex.Message}", ex); }
    }

    [RelayCommand]
    private async Task VolumeDownAsync()
    {
        if (_currentSession is null) return;
        try { await _currentSession.InputController.SendKeyEventAsync(KeycodeVolumeDown); }
        catch (Exception ex) { _logService.Error("Controller", $"Volume down failed: {ex.Message}", ex); }
    }

    [RelayCommand]
    private async Task ApplySettingsAsync()
    {
        try
        {
            _settingsService.Set(SettingKeys.DefaultFps, StreamFps);
            _settingsService.Set(SettingKeys.DefaultBitrate, StreamBitrate);
            _settingsService.Set(SettingKeys.DefaultCodec, StreamCodec);
            _settingsService.Set(SettingKeys.DefaultResolution, Resolution);
            _logService.Information("Controller", "Stream settings saved");

            if (IsScrcpyActive)
            {
                await StopScrcpyAsync();
                await StartScrcpyAsync();
                _logService.Information("Controller", "Stream restarted with new settings");
            }
        }
        catch (Exception ex)
        {
            _logService.Error("Controller", $"Failed to save settings: {ex.Message}", ex);
        }
    }

    private async Task StartScrcpyAsync()
    {
        if (_currentSession is null || IsScrcpyActive) return;

        try
        {
            _scrcpyManager ??= App.Services.GetRequiredService<Services.ScrcpyManager>();
            _scrcpyManager.StatsUpdated += OnScrcpyStatsUpdated;
            _scrcpyManager.WindowClosed += OnScrcpyWindowClosed;

            var maxSize = 0;
            if (Resolution != "Native" && Resolution.Contains('x'))
            {
                var parts = Resolution.Split('x');
                if (int.TryParse(parts[0], out var w)) maxSize = w;
            }

            var options = new ScrcpyOptions
            {
                Serial = _currentSession.Serial,
                MaxFps = StreamFps,
                BitRate = StreamBitrate,
                Codec = StreamCodec,
                MaxSize = maxSize,
                LowLatency = LowLatency,
                AudioEnabled = AudioEnabled
            };

            LiveBitrateText = FormatBitrate(options.BitRate);
            LiveFpsText = "--";
            LiveResolutionText = "--";
            LiveOverlayText = "";

            var hwnd = await _scrcpyManager.StartAsync(options);
            if (hwnd == IntPtr.Zero)
            {
                _logService.Error("Controller", "scrcpy window could not be found. Check that scrcpy is installed and the device is authorized.");
                return;
            }

            IsScrcpyActive = true;
            IsStreaming = true;
            ScrcpyWindowReady?.Invoke(this, hwnd);
            _logService.Information("Controller", $"scrcpy started for {_currentSession.Serial}");
        }
        catch (Exception ex)
        {
            _logService.Error("Controller", $"Failed to start scrcpy: {ex.Message}", ex);
            await StopScrcpyAsync();
        }
    }

    private async Task StopScrcpyAsync()
    {
        if (_scrcpyManager is null) return;

        try
        {
            await _scrcpyManager.StopAsync();
        }
        finally
        {
            IsScrcpyActive = false;
            IsStreaming = false;
            ScrcpyStopped?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task HandleTapAsync(double relativeX, double relativeY)
    {
        if (_currentSession is null) return;
        if (!IsStreaming) return;

        var x = (int)(relativeX * StreamWidth);
        var y = (int)(relativeY * StreamHeight);

        try
        {
            await _currentSession.InputController.SendTapAsync(x, y);
        }
        catch (Exception ex)
        {
            _logService.Error("Controller", $"Tap failed: {ex.Message}", ex);
        }
    }

    public void HandleTap(double relativeX, double relativeY)
    {
        _ = HandleTapAsync(relativeX, relativeY);
    }

    public async Task HandleMouseMoveAsync(double relativeX, double relativeY)
    {
        if (_currentSession is null) return;
        if (!IsStreaming) return;

        var x = (int)(relativeX * StreamWidth);
        var y = (int)(relativeY * StreamHeight);

        try
        {
            await _currentSession.InputController.SendMouseAsync(x, y);
        }
        catch (Exception ex)
        {
            _logService.Error("Controller", $"MouseMove failed: {ex.Message}", ex);
        }
    }

    public void HandleMouseMove(double relativeX, double relativeY)
    {
        _ = HandleMouseMoveAsync(relativeX, relativeY);
    }

    public async Task HandleRightClickAsync(double relativeX, double relativeY)
    {
        if (_currentSession is null) return;
        if (!IsStreaming) return;

        try
        {
            await _currentSession.InputController.PressBackAsync();
        }
        catch (Exception ex)
        {
            _logService.Error("Controller", $"Right click failed: {ex.Message}", ex);
        }
    }

    public void HandleRightClick(double relativeX, double relativeY)
    {
        _ = HandleRightClickAsync(relativeX, relativeY);
    }

    public async Task HandleDoubleClickAsync(double relativeX, double relativeY)
    {
        if (_currentSession is null) return;
        if (!IsStreaming) return;

        var x = (int)(relativeX * StreamWidth);
        var y = (int)(relativeY * StreamHeight);

        try
        {
            await _currentSession.InputController.SendDoubleTapAsync(x, y);
        }
        catch (Exception ex)
        {
            _logService.Error("Controller", $"Double tap failed: {ex.Message}", ex);
        }
    }

    public void HandleDoubleClick(double relativeX, double relativeY)
    {
        _ = HandleDoubleClickAsync(relativeX, relativeY);
    }

    public async Task HandleScrollWheelAsync(double relativeX, double relativeY, int delta)
    {
        if (_currentSession is null) return;
        if (!IsStreaming) return;

        var x = (int)(relativeX * StreamWidth);
        var y = (int)(relativeY * StreamHeight);
        var scrollAmount = delta > 0 ? -1 : 1;

        try
        {
            await _currentSession.InputController.SendScrollAsync(x, y, scrollAmount);
        }
        catch (Exception ex)
        {
            _logService.Error("Controller", $"Scroll failed: {ex.Message}", ex);
        }
    }

    public void HandleScrollWheel(double relativeX, double relativeY, int delta)
    {
        _ = HandleScrollWheelAsync(relativeX, relativeY, delta);
    }

    private void OnRecordingStateChanged(object? sender, RecordingStateChangedEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            IsRecording = e.IsRecording;
            if (!e.IsRecording) RecordingDuration = TimeSpan.Zero;
        });
    }

    private void OnScrcpyStatsUpdated(object? sender, ScrcpyStats stats)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (stats.Fps is int fps)
            {
                CurrentFps = fps;
                LiveFpsText = fps.ToString();
            }

            if (stats.Width is int width && stats.Height is int height)
            {
                StreamWidth = width;
                StreamHeight = height;
                LiveResolutionText = $"{width}×{height}";
            }

            LiveOverlayText = $"{LiveFpsText} fps · {LiveBitrateText} · {LiveResolutionText}";
        });
    }

    private void OnScrcpyWindowClosed(object? sender, EventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (_scrcpyManager is { IsRunning: true }) return;
            LiveFpsText = "--";
            LiveResolutionText = "--";
            LiveOverlayText = "";
        });
    }

    private static string FormatBitrate(int bitrate)
    {
        if (bitrate >= 1_000_000)
        {
            return $"{bitrate / 1_000_000.0:F1} Mbps";
        }

        return bitrate > 0 ? $"{bitrate / 1_000} kbps" : "--";
    }

    private void OnDeviceConnected(object? sender, DeviceConnectedEventArgs e)
    {
        if (_currentSession is null)
        {
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => SetSession(e.Session));
        }
    }

    private void OnDeviceDisconnected(object? sender, DeviceDisconnectedEventArgs e)
    {
        if (_currentSession?.Serial == e.Serial)
        {
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (IsScrcpyActive)
                {
                    await StopScrcpyAsync();
                }
                IsStreaming = false;
                IsRecording = false;
                SelectedDevice = null;
                _currentSession = null;
            });
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _deviceManager.DeviceConnected -= OnDeviceConnected;
        _deviceManager.DeviceDisconnected -= OnDeviceDisconnected;

        if (_currentSession is not null)
        {
            _currentSession.ScreenRecorder.RecordingStateChanged -= OnRecordingStateChanged;
        }

        if (_durationTimer is not null)
            await _durationTimer.DisposeAsync();

        if (_scrcpyManager is not null)
        {
            _scrcpyManager.StatsUpdated -= OnScrcpyStatsUpdated;
            _scrcpyManager.WindowClosed -= OnScrcpyWindowClosed;
            _scrcpyManager.Dispose();
            _scrcpyManager = null;
        }

        GC.SuppressFinalize(this);
    }
}
