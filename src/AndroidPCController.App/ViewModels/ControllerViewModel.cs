using System.IO;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndroidPCController.App.ViewModels;

[ObservableObject]
public partial class ControllerViewModel : IAsyncDisposable
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
    private bool _hardwareAcceleration = true;

    public ControllerViewModel(IDeviceManager deviceManager, ISettingsService settingsService, ILogService logService)
    {
        _deviceManager = deviceManager;
        _settingsService = settingsService;
        _logService = logService;

        LoadSettings();

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;
    }

    private void LoadSettings()
    {
        StreamFps = _settingsService.Get(SettingKeys.DefaultFps, 60);
        StreamBitrate = _settingsService.Get(SettingKeys.DefaultBitrate, 8_000_000);
        StreamCodec = _settingsService.Get(SettingKeys.DefaultCodec, "H264");
        HardwareAcceleration = _settingsService.Get(SettingKeys.HardwareAcceleration, true);

        var resolution = _settingsService.Get(SettingKeys.DefaultResolution, "Native");
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

        try
        {
            var settings = new StreamSettings
            {
                Fps = StreamFps,
                MaxWidth = StreamMaxWidth,
                MaxHeight = StreamMaxHeight,
                Bitrate = StreamBitrate,
                Codec = StreamCodec,
                HardwareAcceleration = HardwareAcceleration,
                LowLatency = true
            };

            _currentSession.ScreenStreamer.StreamStarted += OnStreamStarted;
            _currentSession.ScreenStreamer.StreamStopped += OnStreamStopped;
            _currentSession.ScreenStreamer.FrameReceived += OnFrameReceived;

            await _currentSession.ScreenStreamer.StartAsync(settings);
            _logService.Information("Controller", $"Stream started: {StreamCodec} {StreamMaxWidth}x{StreamMaxHeight}@{StreamFps}fps");
        }
        catch (Exception ex)
        {
            _logService.Error("Controller", $"Failed to start stream: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task StopStreamAsync()
    {
        if (_currentSession is null || !IsStreaming) return;

        try
        {
            await _currentSession.ScreenStreamer.StopAsync();
            _logService.Information("Controller", "Stream stopped");
        }
        catch (Exception ex)
        {
            _logService.Error("Controller", $"Failed to stop stream: {ex.Message}", ex);
        }
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
                RecordAudio = false,
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
    private async Task OpenKeyboardAsync()
    {
        _logService.Information("Controller", "Keyboard toggle requested");
        await Task.CompletedTask;
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
            var content = await _currentSession.Clipboard.GetClipboardTextAsync();
            _logService.Information("Controller", $"Clipboard content: {content ?? "(empty)"}");
        }
        catch (Exception ex) { _logService.Error("Controller", $"Clipboard failed: {ex.Message}", ex); }
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

    private void OnStreamStarted(object? sender, EventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => IsStreaming = true);
    }

    private void OnStreamStopped(object? sender, EventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            IsStreaming = false;
            CurrentFps = 0;
            CurrentBitrate = 0;
            StreamWidth = 0;
            StreamHeight = 0;
        });
    }

    private void OnFrameReceived(object? sender, FrameReceivedEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            StreamWidth = e.Width;
            StreamHeight = e.Height;

            if (_currentSession?.ScreenStreamer is { } streamer)
            {
                CurrentFps = streamer.CurrentFps;
                CurrentBitrate = streamer.CurrentBitrate;
            }
        });
    }

    private void OnRecordingStateChanged(object? sender, RecordingStateChangedEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            IsRecording = e.IsRecording;
            if (!e.IsRecording) RecordingDuration = TimeSpan.Zero;
        });
    }

    private void OnDeviceConnected(object? sender, DeviceConnectedEventArgs e)
    {
        if (_currentSession is null)
        {
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => SelectedDevice = e.Device);
        }
    }

    private void OnDeviceDisconnected(object? sender, DeviceDisconnectedEventArgs e)
    {
        if (_currentSession?.Serial == e.Serial)
        {
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
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
            _currentSession.ScreenStreamer.StreamStarted -= OnStreamStarted;
            _currentSession.ScreenStreamer.StreamStopped -= OnStreamStopped;
            _currentSession.ScreenStreamer.FrameReceived -= OnFrameReceived;
            _currentSession.ScreenRecorder.RecordingStateChanged -= OnRecordingStateChanged;
        }

        if (_durationTimer is not null)
            await _durationTimer.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
