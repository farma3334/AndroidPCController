using AndroidPCController.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AndroidPCController.Devices;

public sealed class ScreenRecorderStub : IScreenRecorder
{
    private readonly ILogger<ScreenRecorderStub> _logger;
    private bool _disposed;
    private bool _isRecording;
    private bool _isPaused;
    private DateTime _startTime;
    private string? _currentFilePath;
    private System.Timers.Timer? _durationTimer;

    public ScreenRecorderStub(ILogger<ScreenRecorderStub> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsRecording => _isRecording;
    public TimeSpan CurrentDuration => _isRecording ? DateTime.UtcNow - _startTime : TimeSpan.Zero;
    public string? CurrentFilePath => _currentFilePath;

    public event EventHandler<RecordingStateChangedEventArgs>? RecordingStateChanged;

    public Task StartAsync(RecordingSettings settings, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (_isRecording)
        {
            _logger.LogWarning("Recording already in progress");
            return Task.CompletedTask;
        }

        _isRecording = true;
        _isPaused = false;
        _startTime = DateTime.UtcNow;

        string outputDir = settings.OutputDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Videos",
            "AndroidPCController");
        Directory.CreateDirectory(outputDir);

        string fileName = settings.OutputFilename ?? $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
        _currentFilePath = Path.Combine(outputDir, fileName);

        _logger.LogInformation("Recording started: {Path} (FPS={Fps}, Bitrate={Bitrate})",
            _currentFilePath, settings.Fps, settings.Bitrate);

        _durationTimer = new System.Timers.Timer(1000);
        _durationTimer.Elapsed += (_, _) =>
        {
            if (_isRecording && !_isPaused)
            {
                RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
                {
                    IsRecording = true,
                    IsPaused = false,
                    FilePath = _currentFilePath
                });
            }
        };
        _durationTimer.Start();

        RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
        {
            IsRecording = true,
            IsPaused = false,
            FilePath = _currentFilePath
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (!_isRecording)
        {
            _logger.LogWarning("No recording in progress");
            return Task.CompletedTask;
        }

        _isRecording = false;
        _isPaused = false;
        _durationTimer?.Stop();
        _durationTimer?.Dispose();
        _durationTimer = null;

        _logger.LogInformation("Recording stopped: {Path} (Duration={Duration})",
            _currentFilePath, CurrentDuration);

        RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
        {
            IsRecording = false,
            IsPaused = false,
            FilePath = _currentFilePath
        });

        _currentFilePath = null;
        return Task.CompletedTask;
    }

    public Task PauseAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (!_isRecording || _isPaused)
            return Task.CompletedTask;

        _isPaused = true;
        _logger.LogInformation("Recording paused");

        RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
        {
            IsRecording = true,
            IsPaused = true,
            FilePath = _currentFilePath
        });

        return Task.CompletedTask;
    }

    public Task ResumeAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (!_isRecording || !_isPaused)
            return Task.CompletedTask;

        _isPaused = false;
        _logger.LogInformation("Recording resumed");

        RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
        {
            IsRecording = true,
            IsPaused = false,
            FilePath = _currentFilePath
        });

        return Task.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        if (_isRecording)
        {
            _isRecording = false;
            _isPaused = false;
        }

        _durationTimer?.Dispose();
        _durationTimer = null;

        _logger.LogInformation("ScreenRecorderStub disposed");
        return ValueTask.CompletedTask;
    }
}
