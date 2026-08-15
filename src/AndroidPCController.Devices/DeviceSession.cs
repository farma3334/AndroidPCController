using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using Microsoft.Extensions.Logging;

namespace AndroidPCController.Devices;

public sealed class DeviceSession : IDeviceSession
{
    private readonly IAdbClient _adbClient;
    private readonly ILogger _logger;
    private ConnectionState _state = ConnectionState.Disconnected;
    private bool _disposed;
    private bool _isStreaming;

    public string Serial => DeviceInfo.Serial;
    public DeviceInfo DeviceInfo { get; private set; }
    public DeviceCapabilities Capabilities { get; private set; }
    public ConnectionState State
    {
        get => _state;
        private set
        {
            var old = _state;
            _state = value;
            if (old != value)
            {
                StateChanged?.Invoke(this, new DeviceStateChangedEventArgs
                {
                    Serial = Serial,
                    OldState = old,
                    NewState = value,
                    Message = $"State transitioned from {old} to {value}."
                });
            }
        }
    }

    public bool IsStreaming => _isStreaming;

    public IScreenStreamer ScreenStreamer { get; }
    public IInputController InputController { get; }
    public IFileTransferService FileTransfer { get; }
    public IApplicationManager AppManager { get; }
    public IClipboardService Clipboard { get; }
    public IScreenshotService Screenshot { get; }
    public IScreenRecorder ScreenRecorder { get; }
    public IDiagnosticsService Diagnostics { get; }

    public event EventHandler<DeviceStateChangedEventArgs>? StateChanged;

    internal DeviceSession(
        IAdbClient adbClient,
        ILogger logger,
        DeviceInfo deviceInfo,
        DeviceCapabilities capabilities,
        ConnectionType connectionType)
    {
        _adbClient = adbClient ?? throw new ArgumentNullException(nameof(adbClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        DeviceInfo = deviceInfo ?? throw new ArgumentNullException(nameof(deviceInfo));
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));

        InputController = new AdbInputController(_adbClient, logger, Serial);
        Clipboard = new AdbClipboardService(_adbClient, logger, Serial);
        Screenshot = new AdbScreenshotService(_adbClient, logger, Serial);
        Diagnostics = new AdbDiagnosticsService(_adbClient, logger);
        FileTransfer = new StubFileTransferService(logger, Serial);
        AppManager = new StubApplicationManager(_adbClient, logger, Serial);
        ScreenStreamer = new StubScreenStreamer(logger, Serial);
        ScreenRecorder = new StubScreenRecorder(logger, Serial);
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (State == ConnectionState.Connected || State == ConnectionState.Authorized)
        {
            _logger.LogDebug("Device {Serial} is already in state {State}.", Serial, State);
            return;
        }

        State = ConnectionState.Connecting;
        _logger.LogInformation("Establishing connection to device {Serial}.", Serial);

        try
        {
            var info = await _adbClient.GetDeviceInfoAsync(Serial, ct).ConfigureAwait(false);
            if (info is not null)
            {
                DeviceInfo = info;
            }

            Capabilities = await _adbClient.GetCapabilitiesAsync(Serial, ct).ConfigureAwait(false);

            State = ConnectionState.Connected;
            _logger.LogInformation("Device {Serial} connected. Model: {Model}, Android: {Version}.",
                Serial, DeviceInfo.Model, DeviceInfo.AndroidVersion);
        }
        catch (Exception ex)
        {
            State = ConnectionState.Error;
            _logger.LogError(ex, "Failed to connect to device {Serial}.", Serial);
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (State == ConnectionState.Disconnected)
        {
            _logger.LogDebug("Device {Serial} is already disconnected.", Serial);
            return;
        }

        _logger.LogInformation("Disconnecting device {Serial}.", Serial);

        if (_isStreaming)
        {
            try
            {
                await ScreenStreamer.StopAsync(ct).ConfigureAwait(false);
                _isStreaming = false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping screen stream during disconnect for device {Serial}.", Serial);
            }
        }

        State = ConnectionState.Disconnected;
        _logger.LogInformation("Device {Serial} disconnected.", Serial);
    }

    public async Task RefreshDeviceInfoAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        _logger.LogDebug("Refreshing device info for {Serial}.", Serial);

        var info = await _adbClient.GetDeviceInfoAsync(Serial, ct).ConfigureAwait(false);
        if (info is not null)
        {
            DeviceInfo = info;
        }

        Capabilities = await _adbClient.GetCapabilitiesAsync(Serial, ct).ConfigureAwait(false);

        _logger.LogDebug("Device info refreshed for {Serial}. Model: {Model}.", Serial, DeviceInfo.Model);
    }

    public async Task<string> ExecuteShellCommandAsync(string command, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        _logger.LogDebug("Executing shell command on device {Serial}: {Command}.", Serial, command);
        return await _adbClient.ExecuteCommandAsync(Serial, command, ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _logger.LogDebug("Disposing session for device {Serial}.", Serial);

        var disposables = new IAsyncDisposable[]
        {
            ScreenStreamer,
            InputController,
            FileTransfer,
            AppManager,
            Clipboard,
            Screenshot,
            ScreenRecorder,
        };

        foreach (var disposable in disposables)
        {
            try
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing {ServiceType} for device {Serial}.",
                    disposable.GetType().Name, Serial);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    #region Service Implementations

    private sealed class AdbInputController : IInputController
    {
        private readonly IAdbClient _adbClient;
        private readonly ILogger _logger;
        private readonly string _serial;
        private bool _disposed;

        public bool IsConnected => !_disposed;

        public AdbInputController(IAdbClient adbClient, ILogger logger, string serial)
        {
            _adbClient = adbClient;
            _logger = logger;
            _serial = serial;
        }

        public async Task SendTapAsync(int x, int y, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await _adbClient.SendTouchEventAsync(_serial, x, y, InputEventType.Tap, ct).ConfigureAwait(false);
        }

        public async Task SendDoubleTapAsync(int x, int y, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await _adbClient.SendTouchEventAsync(_serial, x, y, InputEventType.DoubleTap, ct).ConfigureAwait(false);
        }

        public async Task SendLongPressAsync(int x, int y, int durationMs = 500, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await _adbClient.SendTouchEventAsync(_serial, x, y, InputEventType.LongPress, ct).ConfigureAwait(false);
        }

        public async Task SendSwipeAsync(int x1, int y1, int x2, int y2, int durationMs = 300, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await _adbClient.SendSwipeEventAsync(_serial, x1, y1, x2, y2, durationMs, ct).ConfigureAwait(false);
        }

        public async Task SendPinchAsync(int x, int y, float scale, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _logger.LogDebug("Pinch gesture at ({X},{Y}) with scale {Scale} on {Serial}.", x, y, scale, _serial);
            var distance = (int)(100 * scale);
            await _adbClient.SendSwipeEventAsync(_serial, x - distance, y, x + distance, y, 200, ct).ConfigureAwait(false);
        }

        public async Task SendKeyEventAsync(int keyCode, bool isDown = true, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await _adbClient.SendKeyEventAsync(_serial, keyCode, ct).ConfigureAwait(false);
        }

        public async Task SendTextAsync(string text, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await _adbClient.SendTextAsync(_serial, text, ct).ConfigureAwait(false);
        }

        public async Task SendMouseAsync(int x, int y, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _logger.LogDebug("Mouse move to ({X},{Y}) on {Serial}.", x, y, _serial);
            await _adbClient.SendTouchEventAsync(_serial, x, y, InputEventType.MouseMove, ct).ConfigureAwait(false);
        }

        public async Task SendScrollAsync(int x, int y, int scrollAmount, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _logger.LogDebug("Scroll at ({X},{Y}) amount {Amount} on {Serial}.", x, y, scrollAmount, _serial);
            var direction = scrollAmount > 0 ? -1 : 1;
            var targetY = y + (direction * Math.Abs(scrollAmount) * 50);
            await _adbClient.SendSwipeEventAsync(_serial, x, y, x, targetY, 300, ct).ConfigureAwait(false);
        }

        public async Task PressHomeAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await _adbClient.SendKeyEventAsync(_serial, 3, ct).ConfigureAwait(false);
        }

        public async Task PressBackAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await _adbClient.SendKeyEventAsync(_serial, 4, ct).ConfigureAwait(false);
        }

        public async Task PressRecentAppsAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await _adbClient.SendKeyEventAsync(_serial, 187, ct).ConfigureAwait(false);
        }

        public async Task RotateScreenAsync(DeviceOrientation orientation, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            var orientationValue = orientation switch
            {
                DeviceOrientation.Portrait => 0,
                DeviceOrientation.Landscape => 1,
                DeviceOrientation.ReversePortrait => 2,
                DeviceOrientation.ReverseLandscape => 3,
                _ => 0
            };
            _logger.LogDebug("Rotating screen to {Orientation} on {Serial}.", orientation, _serial);
            await _adbClient.ExecuteCommandAsync(_serial, $"settings put system user_rotation {orientationValue}", ct).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }

    private sealed class AdbClipboardService : IClipboardService
    {
        private readonly IAdbClient _adbClient;
        private readonly ILogger _logger;
        private readonly string _serial;
        private string? _currentContent;
        private bool _disposed;

        public string? CurrentContent => _currentContent;
        public bool IsSyncEnabled { get; set; }

        public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

        public AdbClipboardService(IAdbClient adbClient, ILogger logger, string serial)
        {
            _adbClient = adbClient;
            _logger = logger;
            _serial = serial;
        }

        public async Task<string?> GetClipboardTextAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _currentContent = await _adbClient.GetClipboardAsync(_serial, ct).ConfigureAwait(false);
            return _currentContent;
        }

        public async Task SetClipboardTextAsync(string text, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(text);

            await _adbClient.SetClipboardAsync(_serial, text, ct).ConfigureAwait(false);
            _currentContent = text;

            ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs
            {
                Text = text,
                Source = "Local"
            });

            _logger.LogDebug("Clipboard set on device {Serial}: {Length} characters.", _serial, text.Length);
        }

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }

    private sealed class AdbScreenshotService : IScreenshotService
    {
        private readonly IAdbClient _adbClient;
        private readonly ILogger _logger;
        private readonly string _serial;
        private bool _disposed;

        public AdbScreenshotService(IAdbClient adbClient, ILogger logger, string serial)
        {
            _adbClient = adbClient;
            _logger = logger;
            _serial = serial;
        }

        public async Task<byte[]> CaptureAsync(string? format = "png", CancellationToken ct = default)
        {
            ThrowIfDisposed();

            _logger.LogDebug("Capturing screenshot on device {Serial} in {Format} format.", _serial, format);
            var data = await _adbClient.TakeScreenshotAsync(_serial, ct).ConfigureAwait(false);
            _logger.LogInformation("Screenshot captured on {Serial}: {Size} bytes.", _serial, data.Length);

            return data;
        }

        public async Task<string> CaptureAndSaveAsync(string directory, string? filename = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);

            var data = await CaptureAsync("png", ct).ConfigureAwait(false);
            var name = filename ?? $"screenshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png";
            var localPath = Path.Combine(directory, name);

            await File.WriteAllBytesAsync(localPath, data, ct).ConfigureAwait(false);
            _logger.LogInformation("Screenshot saved to {Path} on device {Serial}.", localPath, _serial);

            return localPath;
        }

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }

    private sealed class AdbDiagnosticsService : IDiagnosticsService
    {
        private readonly IAdbClient _adbClient;
        private readonly ILogger _logger;

        public AdbDiagnosticsService(IAdbClient adbClient, ILogger logger)
        {
            _adbClient = adbClient;
            _logger = logger;
        }

        public async Task<DiagnosticResult> RunDiagnosticsAsync(string serial, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serial);

            _logger.LogInformation("Running diagnostics on device {Serial}.", serial);
            var checks = new List<DiagnosticCheck>();

            var sw = System.Diagnostics.Stopwatch.StartNew();

            sw.Restart();
            var deviceInfo = await _adbClient.GetDeviceInfoAsync(serial, ct).ConfigureAwait(false);
            checks.Add(new DiagnosticCheck
            {
                Name = "Device Detection",
                Passed = deviceInfo is not null,
                Message = deviceInfo is not null ? $"Found: {deviceInfo.Model}" : "Device not found.",
                Duration = sw.Elapsed
            });

            sw.Restart();
            var canGetVersion = false;
            string? versionMessage = null;
            try
            {
                await _adbClient.GetVersionAsync(ct).ConfigureAwait(false);
                canGetVersion = true;
                versionMessage = "ADB server responsive.";
            }
            catch (Exception ex)
            {
                versionMessage = $"ADB server not responsive: {ex.Message}";
            }
            checks.Add(new DiagnosticCheck
            {
                Name = "ADB Server",
                Passed = canGetVersion,
                Message = versionMessage,
                Duration = sw.Elapsed
            });

            sw.Restart();
            var canExecute = false;
            string? shellMessage = null;
            try
            {
                await _adbClient.ExecuteCommandAsync(serial, "echo ok", ct).ConfigureAwait(false);
                canExecute = true;
                shellMessage = "Shell access available.";
            }
            catch (Exception ex)
            {
                shellMessage = $"Shell access failed: {ex.Message}";
            }
            checks.Add(new DiagnosticCheck
            {
                Name = "Shell Access",
                Passed = canExecute,
                Message = shellMessage,
                Duration = sw.Elapsed
            });

            sw.Restart();
            var batteryInfo = "Unknown";
            try
            {
                batteryInfo = await _adbClient.GetBatteryInfoAsync(serial, ct).ConfigureAwait(false);
            }
            catch { }
            checks.Add(new DiagnosticCheck
            {
                Name = "Battery Info",
                Passed = !string.IsNullOrEmpty(batteryInfo) && batteryInfo != "Unknown",
                Message = $"Battery: {batteryInfo}",
                Duration = sw.Elapsed
            });

            sw.Stop();

            return new DiagnosticResult
            {
                Serial = serial,
                Checks = checks.AsReadOnly(),
                AllPassed = checks.All(c => c.Passed)
            };
        }

        public async Task<PerformanceMetrics> GetPerformanceMetricsAsync(string serial, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serial);

            _logger.LogDebug("Retrieving performance metrics for device {Serial}.", serial);

            var batteryInfo = "Unknown";
            try
            {
                batteryInfo = await _adbClient.GetBatteryInfoAsync(serial, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get battery info for {Serial}.", serial);
            }

            int batteryLevel = 0;
            string? batteryState = null;
            if (!string.IsNullOrEmpty(batteryInfo))
            {
                var lines = batteryInfo.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains("level:", StringComparison.OrdinalIgnoreCase))
                    {
                        var levelStr = line.Replace("level:", "").Trim().TrimEnd('%');
                        int.TryParse(levelStr, out batteryLevel);
                    }
                    if (line.Contains("status:", StringComparison.OrdinalIgnoreCase))
                    {
                        batteryState = line.Replace("status:", "").Trim();
                    }
                }
            }

            var cpuUsage = 0.0;
            try
            {
                var cpuResult = await _adbClient.ExecuteCommandAsync(serial, "top -bn1 | head -5", ct).ConfigureAwait(false);
                var cpuLine = cpuResult.Split('\n').FirstOrDefault(l => l.Contains("%cpu", StringComparison.OrdinalIgnoreCase));
                if (cpuLine is not null)
                {
                    var parts = cpuLine.Split('%');
                    if (parts.Length > 0)
                    {
                        double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out cpuUsage);
                    }
                }
            }
            catch { }

            return new PerformanceMetrics
            {
                CpuUsage = cpuUsage,
                BatteryLevel = batteryLevel,
                BatteryState = batteryState,
                Timestamp = DateTime.UtcNow
            };
        }

        public async Task<ConnectionDiagnostic> TestConnectionAsync(string serial, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serial);

            _logger.LogDebug("Testing connection diagnostics for device {Serial}.", serial);

            var serverRunning = false;
            try
            {
                serverRunning = await _adbClient.IsServerRunningAsync(ct).ConfigureAwait(false);
            }
            catch { }

            var usbDetected = false;
            var deviceDetected = false;
            var debuggingEnabled = false;
            var authorized = false;
            var transportInit = false;
            var screenAvailable = false;
            string? errorMessage = null;

            try
            {
                var deviceInfo = await _adbClient.GetDeviceInfoAsync(serial, ct).ConfigureAwait(false);
                if (deviceInfo is not null)
                {
                    usbDetected = deviceInfo.ConnectionType == ConnectionType.Usb;
                    deviceDetected = true;
                    debuggingEnabled = !string.IsNullOrEmpty(deviceInfo.ConnectionState);
                    authorized = deviceInfo.ConnectionState.Contains("device", StringComparison.OrdinalIgnoreCase)
                                 && !deviceInfo.ConnectionState.Contains("unauthorized", StringComparison.OrdinalIgnoreCase);
                    transportInit = authorized;
                    screenAvailable = deviceInfo.ScreenWidth > 0 && deviceInfo.ScreenHeight > 0;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }

            return new ConnectionDiagnostic
            {
                UsbDetected = usbDetected,
                AdbInstalled = serverRunning,
                DeviceDetected = deviceDetected,
                UsbDebuggingEnabled = debuggingEnabled,
                DeviceAuthorized = authorized,
                TransportInitialized = transportInit,
                ScreenStreamAvailable = screenAvailable,
                ErrorMessage = errorMessage,
                Solution = errorMessage is not null ? "Ensure USB debugging is enabled and the device is authorized." : null
            };
        }
    }

    private sealed class StubFileTransferService : IFileTransferService
    {
        private readonly ILogger _logger;
        private readonly string _serial;
        private readonly List<TransferProgress> _activeTransfers = new();
        private bool _disposed;

        public IReadOnlyList<TransferProgress> ActiveTransfers => _activeTransfers.AsReadOnly();

        public event EventHandler<TransferProgressEventArgs>? TransferProgressChanged;

        public StubFileTransferService(ILogger logger, string serial)
        {
            _logger = logger;
            _serial = serial;
        }

        public Task<IReadOnlyList<AndroidFileInfo>> ListDirectoryAsync(string remotePath, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _logger.LogDebug("Listing directory {Path} on device {Serial} (stub).", remotePath, _serial);
            return Task.FromResult<IReadOnlyList<AndroidFileInfo>>(Array.Empty<AndroidFileInfo>());
        }

        public Task<byte[]> DownloadFileAsync(string remotePath, IProgress<TransferProgress>? progress = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _logger.LogDebug("Downloading file {Path} from device {Serial} (stub).", remotePath, _serial);
            throw new NotImplementedException("File download is not yet implemented.");
        }

        public Task UploadFileAsync(string localPath, string remotePath, IProgress<TransferProgress>? progress = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _logger.LogDebug("Uploading file {LocalPath} to {RemotePath} on device {Serial} (stub).", localPath, remotePath, _serial);
            throw new NotImplementedException("File upload is not yet implemented.");
        }

        public Task DeleteFileAsync(string remotePath, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _logger.LogDebug("Deleting file {Path} on device {Serial} (stub).", remotePath, _serial);
            throw new NotImplementedException("File deletion is not yet implemented.");
        }

        public Task CreateDirectoryAsync(string remotePath, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _logger.LogDebug("Creating directory {Path} on device {Serial} (stub).", remotePath, _serial);
            throw new NotImplementedException("Directory creation is not yet implemented.");
        }

        public Task RenameAsync(string oldPath, string newPath, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _logger.LogDebug("Renaming {OldPath} to {NewPath} on device {Serial} (stub).", oldPath, newPath, _serial);
            throw new NotImplementedException("Rename is not yet implemented.");
        }

        public Task<AndroidFileInfo> GetFileInfoAsync(string remotePath, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _logger.LogDebug("Getting file info for {Path} on device {Serial} (stub).", remotePath, _serial);
            throw new NotImplementedException("File info retrieval is not yet implemented.");
        }

        public Task CancelTransferAsync(string transferId, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _logger.LogDebug("Cancelling transfer {TransferId} on device {Serial} (stub).", transferId, _serial);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }

    private sealed class StubApplicationManager : IApplicationManager
    {
        private readonly IAdbClient _adbClient;
        private readonly ILogger _logger;
        private readonly string _serial;
        private bool _disposed;

        public StubApplicationManager(IAdbClient adbClient, ILogger logger, string serial)
        {
            _adbClient = adbClient;
            _logger = logger;
            _serial = serial;
        }

        public async Task<InstalledAppsResult> GetInstalledAppsAsync(bool includeSystem = false, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            _logger.LogDebug("Getting installed apps on device {Serial} (system={IncludeSystem}).", _serial, includeSystem);
            var apps = await _adbClient.GetInstalledAppsAsync(_serial, includeSystem, ct).ConfigureAwait(false);

            var userApps = apps.Where(a => !a.IsSystemApp).ToList().AsReadOnly();
            var systemApps = apps.Where(a => a.IsSystemApp).ToList().AsReadOnly();

            return new InstalledAppsResult
            {
                UserApps = userApps,
                SystemApps = systemApps
            };
        }

        public async Task LaunchAppAsync(string packageName, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await _adbClient.LaunchAppAsync(_serial, packageName, ct).ConfigureAwait(false);
        }

        public async Task ForceStopAppAsync(string packageName, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await _adbClient.ForceStopAppAsync(_serial, packageName, ct).ConfigureAwait(false);
        }

        public async Task UninstallAppAsync(string packageName, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await _adbClient.UninstallAppAsync(_serial, packageName, ct).ConfigureAwait(false);
        }

        public async Task ClearAppDataAsync(string packageName, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await _adbClient.ClearAppDataAsync(_serial, packageName, ct).ConfigureAwait(false);
        }

        public async Task InstallApkAsync(string apkPath, IProgress<TransferProgress>? progress = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await _adbClient.InstallApkAsync(_serial, apkPath, progress, ct).ConfigureAwait(false);
        }

        public async Task EnableAppAsync(string packageName, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _logger.LogDebug("Enabling app {PackageName} on device {Serial}.", packageName, _serial);
            await _adbClient.ExecuteCommandAsync(_serial, $"pm enable {packageName}", ct).ConfigureAwait(false);
        }

        public async Task DisableAppAsync(string packageName, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _logger.LogDebug("Disabling app {PackageName} on device {Serial}.", packageName, _serial);
            await _adbClient.ExecuteCommandAsync(_serial, $"pm disable-user --user 0 {packageName}", ct).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }

    private sealed class StubScreenStreamer : IScreenStreamer
    {
        private readonly ILogger _logger;
        private readonly string _serial;
        private bool _disposed;
        private bool _isStreaming;

        public bool IsStreaming => _isStreaming;
        public int CurrentFps { get; private set; }
        public int CurrentBitrate { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public event EventHandler<FrameReceivedEventArgs>? FrameReceived;
        public event EventHandler<StreamErrorEventArgs>? StreamError;
        public event EventHandler? StreamStarted;
        public event EventHandler? StreamStopped;

        public StubScreenStreamer(ILogger logger, string serial)
        {
            _logger = logger;
            _serial = serial;
        }

        public Task StartAsync(StreamSettings settings, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (_isStreaming)
            {
                _logger.LogDebug("Screen stream already active on device {Serial}.", _serial);
                return Task.CompletedTask;
            }

            _logger.LogInformation("Starting screen stream on device {Serial} (stub). FPS={Fps}, Resolution={Width}x{Height}.",
                _serial, settings.Fps, settings.MaxWidth, settings.MaxHeight);
            _isStreaming = true;
            CurrentFps = settings.Fps;
            CurrentBitrate = settings.Bitrate;
            Width = settings.MaxWidth;
            Height = settings.MaxHeight;
            StreamStarted?.Invoke(this, EventArgs.Empty);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (!_isStreaming)
            {
                return Task.CompletedTask;
            }

            _logger.LogInformation("Stopping screen stream on device {Serial} (stub).", _serial);
            _isStreaming = false;
            CurrentFps = 0;
            CurrentBitrate = 0;
            StreamStopped?.Invoke(this, EventArgs.Empty);

            return Task.CompletedTask;
        }

        public Task UpdateSettingsAsync(StreamSettings settings, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _logger.LogDebug("Updating stream settings on device {Serial} (stub).", _serial);
            CurrentFps = settings.Fps;
            CurrentBitrate = settings.Bitrate;
            Width = settings.MaxWidth;
            Height = settings.MaxHeight;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _isStreaming = false;
                _disposed = true;
            }
            return ValueTask.CompletedTask;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }

    private sealed class StubScreenRecorder : IScreenRecorder
    {
        private readonly ILogger _logger;
        private readonly string _serial;
        private bool _disposed;
        private bool _isRecording;
        private bool _isPaused;
        private DateTime _startTime;
        private string? _currentFilePath;

        public bool IsRecording => _isRecording;
        public TimeSpan CurrentDuration => _isRecording ? DateTime.UtcNow - _startTime : TimeSpan.Zero;
        public string? CurrentFilePath => _currentFilePath;

        public event EventHandler<RecordingStateChangedEventArgs>? RecordingStateChanged;

        public StubScreenRecorder(ILogger logger, string serial)
        {
            _logger = logger;
            _serial = serial;
        }

        public Task StartAsync(RecordingSettings settings, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (_isRecording)
            {
                _logger.LogDebug("Screen recording already active on device {Serial}.", _serial);
                return Task.CompletedTask;
            }

            _logger.LogInformation("Starting screen recording on device {Serial} (stub). FPS={Fps}, Bitrate={Bitrate}.",
                _serial, settings.Fps, settings.Bitrate);
            _isRecording = true;
            _isPaused = false;
            _startTime = DateTime.UtcNow;
            _currentFilePath = settings.OutputFilename ?? $"recording_{DateTime.UtcNow:yyyyMMdd_HHmmss}.mp4";

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
                return Task.CompletedTask;
            }

            _logger.LogInformation("Stopping screen recording on device {Serial} (stub). Duration={Duration}.", _serial, CurrentDuration);
            _isRecording = false;
            _isPaused = false;
            var filePath = _currentFilePath;
            _currentFilePath = null;

            RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
            {
                IsRecording = false,
                IsPaused = false,
                FilePath = filePath
            });

            return Task.CompletedTask;
        }

        public Task PauseAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (!_isRecording || _isPaused)
            {
                return Task.CompletedTask;
            }

            _logger.LogDebug("Pausing screen recording on device {Serial} (stub).", _serial);
            _isPaused = true;

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
            {
                return Task.CompletedTask;
            }

            _logger.LogDebug("Resuming screen recording on device {Serial} (stub).", _serial);
            _isPaused = false;

            RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
            {
                IsRecording = true,
                IsPaused = false,
                FilePath = _currentFilePath
            });

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _isRecording = false;
                _isPaused = false;
                _disposed = true;
            }
            return ValueTask.CompletedTask;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }

    #endregion
}
