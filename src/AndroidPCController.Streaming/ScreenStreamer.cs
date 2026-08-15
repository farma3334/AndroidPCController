using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using Microsoft.Extensions.Logging;

namespace AndroidPCController.Streaming;

public sealed class ScreenStreamer : IScreenStreamer
{
    private readonly IAdbClient _adbClient;
    private readonly string _serial;
    private readonly ILogger<ScreenStreamer> _logger;
    private CancellationTokenSource? _cts;
    private Task? _streamTask;
    private StreamSettings _settings = new();
    private int _frameCount;
    private DateTime _fpsCounterStart = DateTime.UtcNow;
    private bool _disposed;

    public bool IsStreaming => _streamTask is { IsCompleted: false };
    public int CurrentFps { get; private set; }
    public int CurrentBitrate => _settings.Bitrate;
    public int Width { get; private set; }
    public int Height { get; private set; }

    public event EventHandler<FrameReceivedEventArgs>? FrameReceived;
    public event EventHandler<StreamErrorEventArgs>? StreamError;
    public event EventHandler? StreamStarted;
    public event EventHandler? StreamStopped;

    public ScreenStreamer(IAdbClient adbClient, string serial, ILogger<ScreenStreamer> logger)
    {
        _adbClient = adbClient;
        _serial = serial;
        _logger = logger;
    }

    public Task StartAsync(StreamSettings settings, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (IsStreaming)
            throw new InvalidOperationException("Stream is already active.");

        _settings = settings;
        _cts = new CancellationTokenSource();
        _streamTask = StreamLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_cts is null || _streamTask is null) return;

        await _cts.CancelAsync();
        try
        {
            await _streamTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping stream");
        }

        StreamStopped?.Invoke(this, EventArgs.Empty);
    }

    public Task UpdateSettingsAsync(StreamSettings settings, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _settings = settings;
        return Task.CompletedTask;
    }

    private async Task StreamLoopAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting screen stream for device {Serial}", _serial);
        StreamStarted?.Invoke(this, EventArgs.Empty);

        var targetDelay = _settings.Fps > 0 ? 1000 / _settings.Fps : 33;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    var frameData = await _adbClient.TakeScreenshotAsync(_serial, ct).ConfigureAwait(false);

                    if (frameData.Length > 0)
                    {
                        var (w, h) = ParsePngDimensions(frameData);
                        if (w > 0 && h > 0)
                        {
                            Width = w;
                            Height = h;
                        }

                        Interlocked.Increment(ref _frameCount);
                        UpdateFps();

                        FrameReceived?.Invoke(this, new FrameReceivedEventArgs
                        {
                            Width = Width,
                            Height = Height,
                            Data = frameData,
                            TimestampMs = sw.ElapsedMilliseconds,
                            IsKeyFrame = true
                        });
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Frame capture failed");
                    StreamError?.Invoke(this, new StreamErrorEventArgs
                    {
                        Message = $"Frame capture failed: {ex.Message}",
                        Exception = ex
                    });
                }

                sw.Stop();
                var remaining = targetDelay - (int)sw.ElapsedMilliseconds;
                if (remaining > 0)
                {
                    await Task.Delay(remaining, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stream stopped for device {Serial}", _serial);
        }
    }

    private void UpdateFps()
    {
        var elapsed = (DateTime.UtcNow - _fpsCounterStart).TotalSeconds;
        if (elapsed >= 1.0)
        {
            CurrentFps = (int)(Interlocked.Exchange(ref _frameCount, 0) / elapsed);
            _fpsCounterStart = DateTime.UtcNow;
        }
    }

    private static (int Width, int Height) ParsePngDimensions(byte[] pngData)
    {
        if (pngData.Length < 24 || pngData[0] != 0x89 || pngData[1] != 0x50)
            return (0, 0);

        var width = (pngData[16] << 24) | (pngData[17] << 16) | (pngData[18] << 8) | pngData[19];
        var height = (pngData[20] << 24) | (pngData[21] << 16) | (pngData[22] << 8) | pngData[23];
        return (width, height);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            _cts.Dispose();
        }

        if (_streamTask is not null)
        {
            try { await _streamTask.ConfigureAwait(false); }
            catch { }
        }
    }
}
