using System.Diagnostics;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using Microsoft.Extensions.Logging;

namespace AndroidPCController.Devices;

public sealed class AdbScreenRecorder : IScreenRecorder
{
    private readonly IAdbClient _adbClient;
    private readonly string _serial;
    private readonly ILogger<AdbScreenRecorder> _logger;
    private readonly object _lock = new();
    private bool _disposed;
    private bool _isRecording;
    private bool _isPaused;
    private int? _pid;
    private string? _remotePath;
    private string? _currentFilePath;
    private DateTime _startTime;
    private DateTime _pausedSince;
    private TimeSpan _pausedDuration;

    private static readonly TimeSpan ProcessPollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(15);

    public AdbScreenRecorder(IAdbClient adbClient, string serial, ILogger<AdbScreenRecorder> logger)
    {
        _adbClient = adbClient ?? throw new ArgumentNullException(nameof(adbClient));
        _serial = serial ?? throw new ArgumentNullException(nameof(serial));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsRecording
    {
        get { lock (_lock) { return _isRecording; } }
    }

    public TimeSpan CurrentDuration
    {
        get
        {
            lock (_lock)
            {
                if (!_isRecording) return TimeSpan.Zero;
                return DateTime.UtcNow - _startTime - _pausedDuration;
            }
        }
    }

    public string? CurrentFilePath
    {
        get { lock (_lock) { return _currentFilePath; } }
    }

    public event EventHandler<RecordingStateChangedEventArgs>? RecordingStateChanged;

    public async Task StartAsync(RecordingSettings settings, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (_isRecording)
            {
                _logger.LogWarning("Recording already in progress on device {Serial}", _serial);
                return;
            }
        }

        string outputDir = settings.OutputDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Videos",
            "AndroidPCController");
        Directory.CreateDirectory(outputDir);

        string fileName = settings.OutputFilename ?? $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
        string localPath = Path.Combine(outputDir, fileName);

        string remoteDir = "/sdcard/AndroidPCController";
        string remotePath = $"{remoteDir}/{fileName}";

        _logger.LogInformation("Starting screen recording on device {Serial}: {Path} (FPS={Fps}, Bitrate={Bitrate})",
            _serial, remotePath, settings.Fps, settings.Bitrate);

        try
        {
            var availability = await _adbClient.ExecuteCommandAsync(_serial, "which screenrecord", ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(availability))
                throw new InvalidOperationException("screenrecord is not available on the device.");

            await _adbClient.ExecuteCommandAsync(_serial, $"mkdir -p {remoteDir}", ct).ConfigureAwait(false);

            var bitrate = Math.Clamp(settings.Bitrate, 1_000_000, 100_000_000);
            var command =
                $"nohup screenrecord --time-limit 180 --bit-rate {bitrate} {remotePath} & echo $!";
            var output = await _adbClient.ExecuteCommandAsync(_serial, command, ct).ConfigureAwait(false);

            if (!int.TryParse(output.Trim(), out var pid) || pid <= 0)
                throw new InvalidOperationException($"Failed to start screenrecord on device {_serial}: {output.Trim()}");

            lock (_lock)
            {
                _isRecording = true;
                _isPaused = false;
                _pid = pid;
                _remotePath = remotePath;
                _currentFilePath = localPath;
                _startTime = DateTime.UtcNow;
                _pausedDuration = TimeSpan.Zero;
            }

            RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
            {
                IsRecording = true,
                IsPaused = false,
                FilePath = localPath
            });

            _logger.LogInformation("Screen recording started on device {Serial} (PID={Pid})", _serial, pid);
        }
        catch
        {
            try { await _adbClient.ExecuteCommandAsync(_serial, $"rm -f {remotePath}", ct).ConfigureAwait(false); }
            catch { }
            throw;
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        int? pid;
        string? remotePath;
        string? localPath;
        lock (_lock)
        {
            if (!_isRecording)
            {
                _logger.LogWarning("No recording in progress on device {Serial}", _serial);
                return;
            }

            pid = _pid;
            remotePath = _remotePath;
            localPath = _currentFilePath;
        }

        var duration = CurrentDuration;
        _logger.LogInformation("Stopping screen recording on device {Serial} (PID={Pid}, Duration={Duration})",
            _serial, pid, duration);

        string? errorMessage = null;
        try
        {
            if (pid is > 0)
            {
                await _adbClient.ExecuteCommandAsync(_serial, $"kill -INT {pid}", CancellationToken.None).ConfigureAwait(false);

                var stopwatch = Stopwatch.StartNew();
                while (await IsProcessAliveAsync(pid.Value, ct).ConfigureAwait(false))
                {
                    if (stopwatch.Elapsed > StopTimeout)
                    {
                        await _adbClient.ExecuteCommandAsync(_serial, $"kill -9 {pid}", CancellationToken.None).ConfigureAwait(false);
                        break;
                    }

                    await Task.Delay(ProcessPollInterval, ct).ConfigureAwait(false);
                }
            }

            if (remotePath is not null && localPath is not null)
            {
                var data = await _adbClient.PullFileAsync(_serial, remotePath, ct).ConfigureAwait(false);
                await File.WriteAllBytesAsync(localPath, data, ct).ConfigureAwait(false);
            }

            if (remotePath is not null)
            {
                await _adbClient.ExecuteCommandAsync(_serial, $"rm -f {remotePath}", CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            _logger.LogError(ex, "Failed to finalize screen recording on device {Serial}", _serial);
        }

        lock (_lock)
        {
            _isRecording = false;
            _isPaused = false;
            _pid = null;
            _remotePath = null;
        }

        var finalPath = errorMessage is null ? localPath : null;
        if (errorMessage is null)
        {
            _logger.LogInformation("Screen recording saved: {Path}", finalPath);
        }

        RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
        {
            IsRecording = false,
            IsPaused = false,
            FilePath = finalPath,
            Error = errorMessage
        });

        lock (_lock)
        {
            if (errorMessage is null)
            {
                _currentFilePath = null;
            }
        }
    }

    public async Task PauseAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        int? pid;
        lock (_lock)
        {
            if (!_isRecording || _isPaused)
                return;

            pid = _pid;
        }

        if (pid is > 0)
        {
            await _adbClient.ExecuteCommandAsync(_serial, $"kill -STOP {pid}", ct).ConfigureAwait(false);
        }

        lock (_lock)
        {
            _isPaused = true;
            _pausedSince = DateTime.UtcNow;
        }

        _logger.LogInformation("Screen recording paused on device {Serial}", _serial);
        RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
        {
            IsRecording = true,
            IsPaused = true,
            FilePath = _currentFilePath
        });
    }

    public async Task ResumeAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        int? pid;
        lock (_lock)
        {
            if (!_isRecording || !_isPaused)
                return;

            pid = _pid;
        }

        if (pid is > 0)
        {
            await _adbClient.ExecuteCommandAsync(_serial, $"kill -CONT {pid}", ct).ConfigureAwait(false);
        }

        lock (_lock)
        {
            _pausedDuration += DateTime.UtcNow - _pausedSince;
            _isPaused = false;
        }

        _logger.LogInformation("Screen recording resumed on device {Serial}", _serial);
        RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
        {
            IsRecording = true,
            IsPaused = false,
            FilePath = _currentFilePath
        });
    }

    private async Task<bool> IsProcessAliveAsync(int pid, CancellationToken ct)
    {
        try
        {
            var output = await _adbClient.ExecuteCommandAsync(_serial, $"kill -0 {pid}; echo $?", ct).ConfigureAwait(false);
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return lines.Length > 0 && lines[^1].Trim() == "0";
        }
        catch
        {
            return false;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        bool wasRecording;
        lock (_lock)
        {
            wasRecording = _isRecording;
        }

        if (wasRecording)
        {
            try
            {
                await StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop recording during dispose for device {Serial}", _serial);
            }
        }

        _logger.LogInformation("AdbScreenRecorder disposed for device {Serial}", _serial);
    }
}
