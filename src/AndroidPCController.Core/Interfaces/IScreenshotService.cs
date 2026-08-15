namespace AndroidPCController.Core.Interfaces;

public interface IScreenshotService : IAsyncDisposable
{
    Task<byte[]> CaptureAsync(string? format = "png", CancellationToken ct = default);
    Task<string> CaptureAndSaveAsync(string directory, string? filename = null, CancellationToken ct = default);
}

public interface IScreenRecorder : IAsyncDisposable
{
    bool IsRecording { get; }
    TimeSpan CurrentDuration { get; }
    string? CurrentFilePath { get; }
    event EventHandler<RecordingStateChangedEventArgs>? RecordingStateChanged;
    Task StartAsync(RecordingSettings settings, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task PauseAsync(CancellationToken ct = default);
    Task ResumeAsync(CancellationToken ct = default);
}

public sealed class RecordingSettings
{
    public int Fps { get; init; } = 60;
    public int Bitrate { get; init; } = 8_000_000;
    public string Codec { get; init; } = "H264";
    public bool RecordAudio { get; init; }
    public string? OutputDirectory { get; init; }
    public string? OutputFilename { get; init; }
}

public sealed class RecordingStateChangedEventArgs : EventArgs
{
    public required bool IsRecording { get; init; }
    public required bool IsPaused { get; init; }
    public string? FilePath { get; init; }
    public string? Error { get; init; }
}
