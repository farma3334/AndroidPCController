using AndroidPCController.Core.Models;

namespace AndroidPCController.Core.Interfaces;

public interface IDeviceSession : IAsyncDisposable
{
    string Serial { get; }
    DeviceInfo DeviceInfo { get; }
    DeviceCapabilities Capabilities { get; }
    ConnectionState State { get; }
    bool IsStreaming { get; }
    IScreenStreamer ScreenStreamer { get; }
    IInputController InputController { get; }
    IFileTransferService FileTransfer { get; }
    IApplicationManager AppManager { get; }
    IClipboardService Clipboard { get; }
    IScreenshotService Screenshot { get; }
    IScreenRecorder ScreenRecorder { get; }
    IDiagnosticsService Diagnostics { get; }
    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task RefreshDeviceInfoAsync(CancellationToken ct = default);
    Task<string> ExecuteShellCommandAsync(string command, CancellationToken ct = default);
    event EventHandler<DeviceStateChangedEventArgs>? StateChanged;
}

public sealed class DeviceStateChangedEventArgs : EventArgs
{
    public required string Serial { get; init; }
    public required ConnectionState OldState { get; init; }
    public required ConnectionState NewState { get; init; }
    public string? Message { get; init; }
}
