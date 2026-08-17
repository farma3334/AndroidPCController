using System.Collections.ObjectModel;
using System.IO;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndroidPCController.App.ViewModels;

public partial class ScreenRecorderViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IDeviceManager _deviceManager;
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;
    private IDeviceSession? _currentSession;
    private bool _disposed;
    private Timer? _durationTimer;
    private DateTime _recordingStartTime;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private TimeSpan _recordingDuration;

    [ObservableProperty]
    private int _currentBitrate;

    [ObservableProperty]
    private int _currentFps;

    [ObservableProperty]
    private DeviceInfo? _selectedDevice;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _lastRecordingPath = string.Empty;

    [ObservableProperty]
    private int _recordFps = 60;

    [ObservableProperty]
    private int _recordBitrate = 8_000_000;

    [ObservableProperty]
    private string _recordCodec = "H264";

    [ObservableProperty]
    private bool _recordAudio;

    [ObservableProperty]
    private string _recordResolution = "Native";

    [ObservableProperty]
    private int _recordingBitrate = 8;

    [ObservableProperty]
    private ObservableCollection<RecordingItem> _recentRecordings = [];

    public IReadOnlyList<string> AvailableResolutions { get; } = ["Native", "1920x1080", "1280x720", "800x480"];

    public IReadOnlyList<int> AvailableFpsValues { get; } = [15, 30, 60, 120];

    public IReadOnlyList<int> AvailableBitrateValues { get; } = [2, 4, 8, 16, 32, 50];

    public IReadOnlyList<string> AvailableCodecs { get; } = ["H264", "H265"];

    public ScreenRecorderViewModel(IDeviceManager deviceManager, ISettingsService settingsService, ILogService logService)
    {
        _deviceManager = deviceManager;
        _settingsService = settingsService;
        _logService = logService;

        LoadSettings();
        LoadRecentRecordings();

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;

        SetSession(_deviceManager.ActiveSessions.FirstOrDefault());
    }

    partial void OnRecordingBitrateChanged(int value) => RecordBitrate = value * 1_000_000;

    private void LoadRecentRecordings()
    {
        try
        {
            var downloadDir = _settingsService.Get(SettingKeys.DownloadDirectory, string.Empty);
            if (string.IsNullOrEmpty(downloadDir))
                downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "AndroidPCController");

            if (!Directory.Exists(downloadDir)) return;

            var recordings = Directory.GetFiles(downloadDir, "*.mp4")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .Take(10)
                .Select(f => new RecordingItem
                {
                    FileName = f.Name,
                    FilePath = f.FullName,
                    SizeDisplay = FormatSize(f.Length),
                    CreatedDate = f.LastWriteTime
                })
                .ToList();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                RecentRecordings.Clear();
                foreach (var recording in recordings)
                {
                    RecentRecordings.Add(recording);
                }
            });
        }
        catch
        {
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.#} {sizes[order]}";
    }

    private void LoadSettings()
    {
        RecordFps = _settingsService.Get(SettingKeys.DefaultFps, 60);
        RecordBitrate = _settingsService.Get(SettingKeys.DefaultBitrate, 8_000_000);
        RecordCodec = _settingsService.Get(SettingKeys.DefaultCodec, "H264");
        RecordResolution = _settingsService.Get(SettingKeys.DefaultResolution, "Native");
        RecordingBitrate = RecordBitrate / 1_000_000;
    }

    public void SetSession(IDeviceSession? session)
    {
        _currentSession = session;
        SelectedDevice = session?.DeviceInfo;
        IsConnected = session is not null;

        if (session is not null && session.ScreenRecorder.IsRecording)
        {
            IsRecording = true;
            StartDurationTimer();
        }
    }

    [RelayCommand]
    private async Task StartRecordingAsync()
    {
        if (_currentSession is null)
        {
            StatusText = "No device connected.";
            return;
        }

        if (IsRecording) return;

        try
        {
            var downloadDir = _settingsService.Get(SettingKeys.DownloadDirectory, string.Empty);
            if (string.IsNullOrEmpty(downloadDir))
                downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "AndroidPCController");

            var settings = new RecordingSettings
            {
                Fps = RecordFps,
                Bitrate = RecordBitrate,
                Codec = RecordCodec,
                RecordAudio = RecordAudio,
                OutputDirectory = downloadDir
            };

            _currentSession.ScreenRecorder.RecordingStateChanged += OnRecordingStateChanged;
            await _currentSession.ScreenRecorder.StartAsync(settings);

            _recordingStartTime = DateTime.Now;
            StartDurationTimer();

            StatusText = "Recording...";
            _logService.Information("ScreenRecorder", $"Recording started: {RecordCodec} {RecordFps}fps {RecordBitrate}bps");
        }
        catch (Exception ex)
        {
            StatusText = $"Start failed: {ex.Message}";
            _logService.Error("ScreenRecorder", $"Start failed: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task StopRecordingAsync()
    {
        if (_currentSession is null || !IsRecording) return;

        try
        {
            StopDurationTimer();
            await _currentSession.ScreenRecorder.StopAsync();

            LastRecordingPath = _currentSession.ScreenRecorder.CurrentFilePath ?? string.Empty;
            StatusText = $"Recording saved: {Path.GetFileName(LastRecordingPath)}";
            _logService.Information("ScreenRecorder", $"Recording stopped. Duration: {RecordingDuration:hh\\:mm\\:ss}");
            LoadRecentRecordings();
        }
        catch (Exception ex)
        {
            StatusText = $"Stop failed: {ex.Message}";
            _logService.Error("ScreenRecorder", $"Stop failed: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task PauseRecordingAsync()
    {
        if (_currentSession is null || !IsRecording || IsPaused) return;

        try
        {
            await _currentSession.ScreenRecorder.PauseAsync();
            StopDurationTimer();
            IsPaused = true;
            StatusText = "Recording paused.";
            _logService.Information("ScreenRecorder", "Recording paused");
        }
        catch (Exception ex)
        {
            StatusText = $"Pause failed: {ex.Message}";
            _logService.Error("ScreenRecorder", $"Pause failed: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task ResumeRecordingAsync()
    {
        if (_currentSession is null || !IsRecording || !IsPaused) return;

        try
        {
            await _currentSession.ScreenRecorder.ResumeAsync();
            StartDurationTimer();
            IsPaused = false;
            StatusText = "Recording resumed.";
            _logService.Information("ScreenRecorder", "Recording resumed");
        }
        catch (Exception ex)
        {
            StatusText = $"Resume failed: {ex.Message}";
            _logService.Error("ScreenRecorder", $"Resume failed: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task PauseResumeAsync()
    {
        if (_currentSession is null || !IsRecording) return;
        if (IsPaused)
            await ResumeRecordingAsync();
        else
            await PauseRecordingAsync();
    }

    [RelayCommand]
    private void OpenRecordingFolder()
    {
        try
        {
            var downloadDir = _settingsService.Get(SettingKeys.DownloadDirectory, string.Empty);
            if (string.IsNullOrEmpty(downloadDir))
                downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "AndroidPCController");

            Directory.CreateDirectory(downloadDir);
            System.Diagnostics.Process.Start("explorer.exe", downloadDir);
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to open folder: {ex.Message}";
            _logService.Error("ScreenRecorder", $"Open folder failed: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private void OpenLastRecording()
    {
        if (string.IsNullOrEmpty(LastRecordingPath) || !File.Exists(LastRecordingPath)) return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = LastRecordingPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to open recording: {ex.Message}";
            _logService.Error("ScreenRecorder", $"Open recording failed: {ex.Message}", ex);
        }
    }

    private void StartDurationTimer()
    {
        StopDurationTimer();
        _durationTimer = new Timer(_ =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                RecordingDuration = DateTime.Now - _recordingStartTime;
                if (_currentSession?.ScreenRecorder is { IsRecording: true })
                {
                    CurrentFps = RecordFps;
                    CurrentBitrate = RecordBitrate;
                }
            });
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    private void StopDurationTimer()
    {
        if (_durationTimer is not null)
        {
            _durationTimer.Dispose();
            _durationTimer = null;
        }
    }

    private void OnRecordingStateChanged(object? sender, RecordingStateChangedEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            IsRecording = e.IsRecording;
            IsPaused = e.IsPaused;

            if (!e.IsRecording)
            {
                StopDurationTimer();
                RecordingDuration = TimeSpan.Zero;
                CurrentFps = 0;
                CurrentBitrate = 0;

                if (e.Error is not null)
                {
                    StatusText = $"Recording error: {e.Error}";
                    _logService.Error("ScreenRecorder", $"Recording error: {e.Error}");
                }
                else
                {
                    LastRecordingPath = e.FilePath ?? string.Empty;
                }
            }
        });
    }

    private void OnDeviceConnected(object? sender, DeviceConnectedEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => SetSession(e.Session));
    }

    private void OnDeviceDisconnected(object? sender, DeviceDisconnectedEventArgs e)
    {
        if (_currentSession?.Serial == e.Serial)
        {
            if (IsRecording) StopDurationTimer();
            _currentSession = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                SelectedDevice = null;
                IsConnected = false;
                IsRecording = false;
                IsPaused = false;
                RecordingDuration = TimeSpan.Zero;
                StatusText = "Device disconnected.";
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
            _currentSession.ScreenRecorder.RecordingStateChanged -= OnRecordingStateChanged;

        StopDurationTimer();

        GC.SuppressFinalize(this);
    }
}

public sealed class RecordingItem
{
    public string FileName { get; init; } = "";
    public string FilePath { get; init; } = "";
    public string SizeDisplay { get; init; } = "";
    public string DurationDisplay { get; init; } = "";
    public DateTime CreatedDate { get; init; }
}
