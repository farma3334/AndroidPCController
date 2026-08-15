using AndroidPCController.Core.Models;

namespace AndroidPCController.Core.Interfaces;

public interface IAdbClient : IAsyncDisposable
{
    Task<string> GetVersionAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default);
    Task<DeviceInfo?> GetDeviceInfoAsync(string serial, CancellationToken ct = default);
    Task<bool> IsServerRunningAsync(CancellationToken ct = default);
    Task StartServerAsync(CancellationToken ct = default);
    Task StopServerAsync(CancellationToken ct = default);
    Task<DeviceCapabilities> GetCapabilitiesAsync(string serial, CancellationToken ct = default);
    Task<string> ExecuteCommandAsync(string serial, string command, CancellationToken ct = default);
    Task<byte[]> ExecuteCommandBytesAsync(string serial, string command, CancellationToken ct = default);
    Task<byte[]> PullFileAsync(string serial, string remotePath, CancellationToken ct = default);
    Task PushFileAsync(string serial, string localPath, string remotePath, IProgress<TransferProgress>? progress = null, CancellationToken ct = default);
    Task<IReadOnlyList<AndroidAppInfo>> GetInstalledAppsAsync(string serial, bool includeSystem = false, CancellationToken ct = default);
    Task<string?> GetClipboardAsync(string serial, CancellationToken ct = default);
    Task SetClipboardAsync(string serial, string text, CancellationToken ct = default);
    Task<byte[]> TakeScreenshotAsync(string serial, CancellationToken ct = default);
    Task<string> GetBatteryInfoAsync(string serial, CancellationToken ct = default);
    Task<string> GetScreenSizeAsync(string serial, CancellationToken ct = default);
    Task ConnectWirelessAsync(string host, int port, CancellationToken ct = default);
    Task DisconnectWirelessAsync(string host, int port, CancellationToken ct = default);
    Task<int> PairWirelessAsync(string host, int port, string code, CancellationToken ct = default);
    Task InstallApkAsync(string serial, string apkPath, IProgress<TransferProgress>? progress = null, CancellationToken ct = default);
    Task UninstallAppAsync(string serial, string packageName, CancellationToken ct = default);
    Task LaunchAppAsync(string serial, string packageName, CancellationToken ct = default);
    Task ForceStopAppAsync(string serial, string packageName, CancellationToken ct = default);
    Task ClearAppDataAsync(string serial, string packageName, CancellationToken ct = default);
    Task<string> GetLogcatAsync(string serial, int lineCount = 500, CancellationToken ct = default);
    Task SendKeyEventAsync(string serial, int keyCode, CancellationToken ct = default);
    Task SendTouchEventAsync(string serial, int x, int y, InputEventType type, CancellationToken ct = default);
    Task SendSwipeEventAsync(string serial, int x1, int y1, int x2, int y2, int durationMs, CancellationToken ct = default);
    Task SendTextAsync(string serial, string text, CancellationToken ct = default);
    event EventHandler<DeviceChangedEventArgs>? DeviceChanged;
}
