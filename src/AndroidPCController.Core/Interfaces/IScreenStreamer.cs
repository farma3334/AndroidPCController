namespace AndroidPCController.Core.Interfaces;

public interface IScreenStreamer : IAsyncDisposable
{
    bool IsStreaming { get; }
    int CurrentFps { get; }
    int CurrentBitrate { get; }
    int Width { get; }
    int Height { get; }
    event EventHandler<FrameReceivedEventArgs>? FrameReceived;
    event EventHandler<StreamErrorEventArgs>? StreamError;
    event EventHandler? StreamStarted;
    event EventHandler? StreamStopped;
    Task StartAsync(StreamSettings settings, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task UpdateSettingsAsync(StreamSettings settings, CancellationToken ct = default);
}

public sealed class StreamSettings
{
    public int Fps { get; init; } = 60;
    public int MaxWidth { get; init; } = 1920;
    public int MaxHeight { get; init; } = 1080;
    public int Bitrate { get; init; } = 8_000_000;
    public string Codec { get; init; } = "H264";
    public bool HardwareAcceleration { get; init; } = true;
    public bool AudioEnabled { get; init; }
    public bool LowLatency { get; init; } = true;
}

public sealed class FrameReceivedEventArgs : EventArgs
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required byte[] Data { get; init; }
    public required long TimestampMs { get; init; }
    public required bool IsKeyFrame { get; init; }
}

public sealed class StreamErrorEventArgs : EventArgs
{
    public required string Message { get; init; }
    public Exception? Exception { get; init; }
}
