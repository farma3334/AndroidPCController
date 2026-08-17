using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using AndroidPCController.Core.Notifications;
using Microsoft.Extensions.Logging;

namespace AndroidPCController.Adb;

public sealed class AdbClient : IAdbClient
{
    private readonly ILogger<AdbClient> _logger;
    private readonly string _adbPath;
    private readonly Dictionary<string, DeviceInfo> _knownDevices = new();
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private Timer? _devicePollTimer;
    private bool _disposed;
    private bool _serverStartedByUs;

    private static readonly TimeSpan DevicePollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LongCommandTimeout = TimeSpan.FromMinutes(5);

    private static readonly Regex DeviceLineRegex = new(
        @"^(\S+)\s+(device|offline|unauthorized|bootloader|recovery|sideload|no permissions)\s*(.*)$",
        RegexOptions.Compiled);

    private static readonly Regex PropertiesRegex = new(
        @"\[(.+?)\]:\s*\[(.+?)\]",
        RegexOptions.Compiled);

    public event EventHandler<DeviceChangedEventArgs>? DeviceChanged;

    public AdbClient(ILogger<AdbClient> logger)
    {
        _logger = logger;
        _adbPath = FindAdbExecutable();
        _logger.LogInformation("ADB client initialized with path: {AdbPath}", _adbPath);
    }

    public async Task<string> GetVersionAsync(CancellationToken ct = default)
    {
        var output = await RunAdbCommandAsync("version", ct: ct);
        var firstLine = output.Split('\n', 2)[0].Trim();
        _logger.LogDebug("ADB version: {Version}", firstLine);
        return firstLine;
    }

    public async Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default)
    {
        var output = await RunAdbCommandAsync("devices -l", ct: ct);
        return ParseDeviceList(output);
    }

    public async Task<DeviceInfo?> GetDeviceInfoAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            var props = await GetDevicePropertiesAsync(serial, ct);

            var model = props.GetValueOrDefault("ro.product.model", "Unknown") ?? "Unknown";
            var manufacturer = props.GetValueOrDefault("ro.product.manufacturer", "Unknown") ?? "Unknown";
            var androidVersion = props.GetValueOrDefault("ro.build.version.release", "Unknown") ?? "Unknown";
            var apiLevelStr = props.GetValueOrDefault("ro.build.version.sdk", "0") ?? "0";
            int.TryParse(apiLevelStr, out int apiLevel);
            var productName = props.GetValueOrDefault("ro.product.name", "Unknown") ?? "Unknown";
            var deviceName = props.GetValueOrDefault("ro.product.device", "Unknown") ?? "Unknown";

            var screenSize = await GetScreenSizeInternalAsync(serial, ct);
            var screenDensityStr = props.GetValueOrDefault("ro.sf.lcd_density", "0");
            int.TryParse(screenDensityStr, out int screenDensity);

            var connectionType = serial.Contains(':') ? ConnectionType.Wireless : ConnectionType.Usb;

            string? ipAddress = null;
            if (connectionType == ConnectionType.Wireless && serial.Contains(':'))
            {
                ipAddress = serial.Split(':')[0];
            }

            var batteryInfo = await GetBatteryInfoInternalAsync(serial, ct);
            int? batteryLevel = null;
            string? batteryState = null;
            if (batteryInfo != null)
            {
                batteryLevel = batteryInfo.Value.Level;
                batteryState = batteryInfo.Value.State;
            }

            var buildNumber = props.GetValueOrDefault("ro.build.display.id", null);
            var securityPatch = props.GetValueOrDefault("ro.build.version.security_patch", null);
            var kernelVersion = await ExecuteCommandAsync(serial, "uname -r", ct);
            kernelVersion = kernelVersion.Trim();
            var chipset = props.GetValueOrDefault("ro.hardware.chipname", props.GetValueOrDefault("ro.board.platform", null));
            var cpuInfo = await ExecuteCommandAsync(serial, "cat /proc/cpuinfo | head -5", ct);
            cpuInfo = cpuInfo.Trim();
            var totalRamStr = props.GetValueOrDefault("ro.product.ram", "0");
            long.TryParse(totalRamStr, out long totalRam);

            var bluetoothAddress = props.GetValueOrDefault("ro.ril.oem.bluetooth", null);
            var usbProductId = props.GetValueOrDefault("ro.product.usb.product_id", null);
            var usbVendorId = props.GetValueOrDefault("ro.product.usb.vendor_id", null);

            return new DeviceInfo
            {
                Serial = serial,
                Model = model,
                Manufacturer = manufacturer,
                AndroidVersion = androidVersion,
                ApiLevel = apiLevel,
                ProductName = productName,
                DeviceName = deviceName,
                ScreenWidth = screenSize.Width,
                ScreenHeight = screenSize.Height,
                ScreenDensity = screenDensity,
                ConnectionState = "device",
                ConnectionType = connectionType,
                IpAddress = ipAddress,
                BatteryLevel = batteryLevel,
                BatteryState = batteryState,
                BuildNumber = buildNumber,
                SecurityPatch = securityPatch,
                KernelVersion = kernelVersion,
                Chipset = chipset,
                CpuInfo = cpuInfo,
                TotalRam = totalRam,
                BluetoothAddress = bluetoothAddress,
                UsbProductId = usbProductId,
                UsbVendorId = usbVendorId,
                LastSeen = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get device info for {Serial}", serial);
            return null;
        }
    }

    public async Task<bool> IsServerRunningAsync(CancellationToken ct = default)
    {
        try
        {
            var output = await RunAdbCommandAsync("start-server", ct: ct, timeout: TimeSpan.FromSeconds(5));
            return !output.Contains("error") && !output.Contains("failed");
        }
        catch
        {
            return false;
        }
    }

    public async Task StartServerAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting ADB server");
        var output = await RunAdbCommandAsync("start-server", ct: ct);
        _logger.LogDebug("ADB server start output: {Output}", output);
        _serverStartedByUs = true;
    }

    public async Task StopServerAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Stopping ADB server");
        var output = await RunAdbCommandAsync("kill-server", ct: ct);
        _logger.LogDebug("ADB server stop output: {Output}", output);
        _serverStartedByUs = false;
    }

    public async Task<DeviceCapabilities> GetCapabilitiesAsync(string serial, CancellationToken ct = default)
    {
        var props = await GetDevicePropertiesAsync(serial, ct);
        var apiLevelStr = props.GetValueOrDefault("ro.build.version.sdk", "0");
        int.TryParse(apiLevelStr, out int apiLevel);
        var androidVersion = props.GetValueOrDefault("ro.build.version.release", "Unknown");
        var manufacturer = props.GetValueOrDefault("ro.product.manufacturer", "Unknown");
        var model = props.GetValueOrDefault("ro.product.model", "Unknown");

        var screenSize = await GetScreenSizeInternalAsync(serial, ct);
        int maxResolution = Math.Max(screenSize.Width, screenSize.Height);

        bool supportsScrcpy = await CheckPackageInstalledAsync(serial, "com.genymotion.scrcpy", ct) ||
                              await CheckCommandAvailableAsync(serial, "scrcpy", ct);

        return new DeviceCapabilities
        {
            ScreenStreaming = apiLevel >= 19,
            AudioStreaming = apiLevel >= 30,
            Clipboard = apiLevel >= 11,
            FileTransfer = true,
            RemoteInput = apiLevel >= 19,
            Notifications = apiLevel >= 19,
            ScreenRecording = apiLevel >= 19,
            Screenshot = apiLevel >= 19,
            ShellAccess = true,
            InputInjection = apiLevel >= 19,
            MediaProjection = apiLevel >= 21,
            AccessibilityService = apiLevel >= 16,
            H264Support = apiLevel >= 19,
            H265Support = apiLevel >= 24,
            MaxFps = 60,
            MaxResolution = maxResolution,
            ApiLevel = apiLevel,
            AndroidVersion = androidVersion,
            Manufacturer = manufacturer,
            Model = model
        };
    }

    public async Task<string> ExecuteCommandAsync(string serial, string command, CancellationToken ct = default)
    {
        var output = await RunAdbCommandAsync($"-s {serial} shell {command}", ct: ct, timeout: DefaultCommandTimeout);
        return output;
    }

    public async Task<byte[]> ExecuteCommandBytesAsync(string serial, string command, CancellationToken ct = default)
    {
        var result = await RunAdbCommandRawAsync($"-s {serial} shell {command}", ct: ct, timeout: DefaultCommandTimeout);
        return Encoding.UTF8.GetBytes(result.Output);
    }

    public async Task<byte[]> PullFileAsync(string serial, string remotePath, CancellationToken ct = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"adb_pull_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempPath);
            var localFile = Path.Combine(tempPath, Path.GetFileName(remotePath));
            await RunAdbCommandAsync($"-s {serial} pull \"{remotePath}\" \"{localFile}\"",
                ct: ct, timeout: LongCommandTimeout);
            return await File.ReadAllBytesAsync(localFile, ct);
        }
        finally
        {
            try { Directory.Delete(tempPath, true); } catch { }
        }
    }

    public async Task PullBugReportAsync(string serial, string localPath, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await RunAdbCommandAsync($"-s {serial} bugreport \"{localPath}\"",
            ct: ct, timeout: LongCommandTimeout);
    }

    public async Task PushFileAsync(string serial, string localPath, string remotePath,
        IProgress<TransferProgress>? progress = null, CancellationToken ct = default)
    {
        if (!File.Exists(localPath))
            throw new FileNotFoundException($"Local file not found: {localPath}");

        var fileInfo = new FileInfo(localPath);
        var transferId = Guid.NewGuid().ToString("N");
        var transferProgress = new TransferProgress
        {
            TransferId = transferId,
            FileName = Path.GetFileName(localPath),
            SourcePath = localPath,
            DestinationPath = remotePath,
            State = TransferState.InProgress,
            TotalBytes = fileInfo.Length,
            TransferredBytes = 0,
            StartTime = DateTime.UtcNow
        };

        try
        {
            progress?.Report(transferProgress);
            await RunAdbCommandAsync($"-s {serial} push \"{localPath}\" \"{remotePath}\"",
                ct: ct, timeout: LongCommandTimeout);

            transferProgress.State = TransferState.Completed;
            transferProgress.TransferredBytes = fileInfo.Length;
            progress?.Report(transferProgress);
        }
        catch (Exception ex)
        {
            transferProgress.State = TransferState.Failed;
            transferProgress.ErrorMessage = ex.Message;
            progress?.Report(transferProgress);
            throw;
        }
    }

    public async Task<IReadOnlyList<AndroidAppInfo>> GetInstalledAppsAsync(string serial,
        bool includeSystem = false, CancellationToken ct = default)
    {
        var flag = includeSystem ? "-s" : "-3";
        var output = await ExecuteCommandAsync(serial, $"pm list packages {flag}", ct);
        var packages = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Replace("package:", "").Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        var apps = new List<AndroidAppInfo>();
        foreach (var package in packages)
        {
            try
            {
                var versionName = (await ExecuteCommandAsync(serial, $"dumpsys package {package} | grep versionName", ct)).Trim();
                versionName = versionName.Replace("versionName=", "").Trim();

                var versionCodeStr = (await ExecuteCommandAsync(serial, $"dumpsys package {package} | grep versionCode", ct)).Trim();
                versionCodeStr = versionCodeStr.Replace("versionCode=", "").Trim();
                int.TryParse(versionCodeStr.Split(' ')[0], out int versionCode);

                var appName = (await ExecuteCommandAsync(serial, $"pm dump {package} | grep -i \"applicationLabel\" | head -1", ct)).Trim();
                appName = appName.Replace("applicationLabel=", "").Trim();
                if (string.IsNullOrEmpty(appName)) appName = package;

                var isSystem = includeSystem && await IsSystemAppAsync(serial, package, ct);
                var sourceDir = (await ExecuteCommandAsync(serial, $"pm path {package}", ct)).Trim();
                sourceDir = sourceDir.Replace("package:", "").Trim();

                apps.Add(new AndroidAppInfo
                {
                    PackageName = package,
                    AppName = appName,
                    VersionName = string.IsNullOrEmpty(versionName) ? "Unknown" : versionName,
                    VersionCode = versionCode,
                    IsSystemApp = isSystem,
                    SourceDir = sourceDir
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get details for package {Package}", package);
            }
        }

        return apps;
    }

    public async Task<string?> GetClipboardAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            var output = await ExecuteCommandAsync(serial, "service call clipboard 2 i32 1 s16 get | grep -o \"'[^']*'\" | tr -d \"'\"", ct);
            var trimmed = output.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
        catch
        {
            return null;
        }
    }

    public async Task SetClipboardAsync(string serial, string text, CancellationToken ct = default)
    {
        var escaped = text.Replace("'", "'\\''");
        await ExecuteCommandAsync(serial, $"service call clipboard 2 i32 1 s16 '{escaped}'", ct);
    }

    public async Task<IReadOnlyList<NotificationInfo>> GetNotificationsAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            var output = await ExecuteCommandAsync(serial, "dumpsys notification --noredact", ct);
            return NotificationInfoParser.Parse(output);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to read notifications from {serial}: {ex.Message}", ex);
        }
    }

    public async Task<byte[]> TakeScreenshotAsync(string serial, CancellationToken ct = default)
    {
        var remotePath = "/sdcard/screenshot_tmp.png";
        try
        {
            await ExecuteCommandAsync(serial, $"screencap -p {remotePath}", ct);
            var data = await PullFileAsync(serial, remotePath, ct);
            await ExecuteCommandAsync(serial, $"rm {remotePath}", ct);
            return data;
        }
        catch
        {
            try { await ExecuteCommandAsync(serial, $"rm {remotePath}", ct); } catch { }
            throw;
        }
    }

    public async Task<string> GetBatteryInfoAsync(string serial, CancellationToken ct = default)
    {
        return await ExecuteCommandAsync(serial, "dumpsys battery", ct);
    }

    public async Task<string> GetScreenSizeAsync(string serial, CancellationToken ct = default)
    {
        var size = await GetScreenSizeInternalAsync(serial, ct);
        return $"{size.Width}x{size.Height}";
    }

    public async Task ConnectWirelessAsync(string host, int port, CancellationToken ct = default)
    {
        var output = await RunAdbCommandAsync($"connect {host}:{port}", ct: ct);
        _logger.LogInformation("Wireless connect to {Host}:{Port}: {Output}", host, port, output);
        if (output.Contains("cannot connect") || output.Contains("failed"))
            throw new InvalidOperationException($"Failed to connect to {host}:{port}: {output.Trim()}");
    }

    public async Task DisconnectWirelessAsync(string host, int port, CancellationToken ct = default)
    {
        var output = await RunAdbCommandAsync($"disconnect {host}:{port}", ct: ct);
        _logger.LogInformation("Wireless disconnect from {Host}:{Port}: {Output}", host, port, output);
    }

    public async Task<int> PairWirelessAsync(string host, int port, string code, CancellationToken ct = default)
    {
        var output = await RunAdbCommandAsync($"pair {host}:{port} {code}", ct: ct);
        _logger.LogInformation("Wireless pair to {Host}:{Port}: {Output}", host, port, output);

        if (output.Contains("Successfully"))
        {
            var portMatch = Regex.Match(output, @"port\s+(\d+)");
            if (portMatch.Success && int.TryParse(portMatch.Groups[1].Value, out int assignedPort))
                return assignedPort;
        }

        if (output.Contains("failed") || output.Contains("error"))
            throw new InvalidOperationException($"Pairing failed: {output.Trim()}");

        return port;
    }

    public async Task InstallApkAsync(string serial, string apkPath,
        IProgress<TransferProgress>? progress = null, CancellationToken ct = default)
    {
        if (!File.Exists(apkPath))
            throw new FileNotFoundException($"APK file not found: {apkPath}");

        var fileInfo = new FileInfo(apkPath);
        var transferId = Guid.NewGuid().ToString("N");
        var transferProgress = new TransferProgress
        {
            TransferId = transferId,
            FileName = Path.GetFileName(apkPath),
            SourcePath = apkPath,
            DestinationPath = $"/data/local/tmp/{Path.GetFileName(apkPath)}",
            State = TransferState.InProgress,
            TotalBytes = fileInfo.Length,
            TransferredBytes = 0,
            StartTime = DateTime.UtcNow
        };

        try
        {
            progress?.Report(transferProgress);

            var pushOutput = await RunAdbCommandAsync(
                $"-s {serial} push \"{apkPath}\" /data/local/tmp/", ct: ct, timeout: LongCommandTimeout);
            transferProgress.TransferredBytes = fileInfo.Length;
            progress?.Report(transferProgress);

            var installOutput = await ExecuteCommandAsync(serial,
                $"pm install -r /data/local/tmp/{Path.GetFileName(apkPath)}", ct);
            await ExecuteCommandAsync(serial, $"rm /data/local/tmp/{Path.GetFileName(apkPath)}", ct);

            if (installOutput.Contains("Failure") || installOutput.Contains("Error"))
                throw new InvalidOperationException($"APK installation failed: {installOutput.Trim()}");

            transferProgress.State = TransferState.Completed;
            progress?.Report(transferProgress);
        }
        catch (Exception ex)
        {
            transferProgress.State = TransferState.Failed;
            transferProgress.ErrorMessage = ex.Message;
            progress?.Report(transferProgress);
            throw;
        }
    }

    public async Task UninstallAppAsync(string serial, string packageName, CancellationToken ct = default)
    {
        var output = await ExecuteCommandAsync(serial, $"pm uninstall {packageName}", ct);
        _logger.LogInformation("Uninstalled {Package}: {Output}", packageName, output.Trim());
    }

    public async Task LaunchAppAsync(string serial, string packageName, CancellationToken ct = default)
    {
        var activity = await GetLauncherActivityAsync(serial, packageName, ct);
        await ExecuteCommandAsync(serial, $"am start -n {packageName}/{activity}", ct);
    }

    public async Task ForceStopAppAsync(string serial, string packageName, CancellationToken ct = default)
    {
        await ExecuteCommandAsync(serial, $"am force-stop {packageName}", ct);
    }

    public async Task ClearAppDataAsync(string serial, string packageName, CancellationToken ct = default)
    {
        await ExecuteCommandAsync(serial, $"pm clear {packageName}", ct);
    }

    public async Task<string> GetLogcatAsync(string serial, int lineCount = 500, CancellationToken ct = default)
    {
        return await ExecuteCommandAsync(serial, $"logcat -d -t {lineCount}", ct);
    }

    public async Task SendKeyEventAsync(string serial, int keyCode, CancellationToken ct = default)
    {
        await ExecuteCommandAsync(serial, $"input keyevent {keyCode}", ct);
    }

    public async Task SendTouchEventAsync(string serial, int x, int y, InputEventType type, CancellationToken ct = default)
    {
        var cmd = type switch
        {
            InputEventType.Tap => $"input tap {x} {y}",
            InputEventType.LongPress => $"input swipe {x} {y} {x} {y} 1000",
            _ => $"input tap {x} {y}"
        };
        await ExecuteCommandAsync(serial, cmd, ct);
    }

    public async Task SendSwipeEventAsync(string serial, int x1, int y1, int x2, int y2,
        int durationMs, CancellationToken ct = default)
    {
        await ExecuteCommandAsync(serial, $"input swipe {x1} {y1} {x2} {y2} {durationMs}", ct);
    }

    public async Task SendTextAsync(string serial, string text, CancellationToken ct = default)
    {
        var escaped = text.Replace(" ", "%s")
            .Replace("&", "\\&")
            .Replace("<", "\\<")
            .Replace(">", "\\>")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"");
        await ExecuteCommandAsync(serial, $"input text '{escaped}'", ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopDevicePolling();

        if (_serverStartedByUs)
        {
            try
            {
                await RunAdbCommandAsync("kill-server", ct: CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop ADB server on dispose");
            }
        }

        _commandLock.Dispose();
    }

    private void StartDevicePolling()
    {
        _devicePollTimer = new Timer(async _ => await PollDevicesAsync(),
            null, DevicePollInterval, DevicePollInterval);
    }

    private async Task StopDevicePolling()
    {
        if (_devicePollTimer != null)
        {
            await _devicePollTimer.DisposeAsync();
            _devicePollTimer = null;
        }
    }

    private async Task PollDevicesAsync()
    {
        try
        {
            var currentDevices = await GetDevicesAsync();
            var currentSerials = new HashSet<string>(currentDevices.Select(d => d.Serial));

            foreach (var serial in _knownDevices.Keys.ToList())
            {
                if (!currentSerials.Contains(serial))
                {
                    _knownDevices.Remove(serial);
                    DeviceChanged?.Invoke(this, new DeviceChangedEventArgs
                    {
                        Serial = serial,
                        ChangeType = "Disconnected"
                    });
                }
            }

            foreach (var device in currentDevices)
            {
                if (_knownDevices.TryGetValue(device.Serial, out var existing))
                {
                    if (existing.ConnectionState != device.ConnectionState)
                    {
                        _knownDevices[device.Serial] = device;
                        DeviceChanged?.Invoke(this, new DeviceChangedEventArgs
                        {
                            Serial = device.Serial,
                            ChangeType = "StateChanged",
                            DeviceInfo = device
                        });
                    }
                }
                else
                {
                    var fullInfo = await GetDeviceInfoAsync(device.Serial);
                    if (fullInfo != null)
                        _knownDevices[device.Serial] = fullInfo;

                    DeviceChanged?.Invoke(this, new DeviceChangedEventArgs
                    {
                        Serial = device.Serial,
                        ChangeType = "Connected",
                        DeviceInfo = fullInfo
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Device polling failed");
        }
    }

    private IReadOnlyList<DeviceInfo> ParseDeviceList(string output)
    {
        var devices = new List<DeviceInfo>();
        var lines = output.Split('\n');

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("List of") || line.StartsWith("*"))
                continue;

            var match = DeviceLineRegex.Match(line.Trim());
            if (!match.Success) continue;

            var serial = match.Groups[1].Value;
            var state = match.Groups[2].Value;
            var extra = match.Groups[3].Value;

            var connectionType = serial.Contains(':') ? ConnectionType.Wireless : ConnectionType.Usb;
            string? ipAddress = connectionType == ConnectionType.Wireless ? serial.Split(':')[0] : null;

            var device = new DeviceInfo
            {
                Serial = serial,
                Model = ExtractProperty(extra, "model"),
                Manufacturer = "Unknown",
                AndroidVersion = "Unknown",
                ApiLevel = 0,
                ProductName = ExtractProperty(extra, "product"),
                DeviceName = ExtractProperty(extra, "device"),
                ScreenWidth = 0,
                ScreenHeight = 0,
                ScreenDensity = 0,
                ConnectionState = state,
                ConnectionType = connectionType,
                IpAddress = ipAddress,
                LastSeen = DateTime.UtcNow
            };

            devices.Add(device);
        }

        return devices;
    }

    private static string ExtractProperty(string line, string propertyName)
    {
        var match = Regex.Match(line, $"{propertyName}:([\\w-]+)");
        return match.Success ? match.Groups[1].Value : "Unknown";
    }

    private async Task<Dictionary<string, string?>> GetDevicePropertiesAsync(string serial, CancellationToken ct)
    {
        var output = await ExecuteCommandAsync(serial, "getprop", ct);
        var props = new Dictionary<string, string?>();

        foreach (Match match in PropertiesRegex.Matches(output))
        {
            props[match.Groups[1].Value] = match.Groups[2].Value;
        }

        return props;
    }

    private async Task<(int Width, int Height)> GetScreenSizeInternalAsync(string serial, CancellationToken ct)
    {
        try
        {
            var output = await ExecuteCommandAsync(serial, "wm size", ct);
            var match = Regex.Match(output, @"(\d+)x(\d+)");
            if (match.Success)
            {
                int.TryParse(match.Groups[1].Value, out int width);
                int.TryParse(match.Groups[2].Value, out int height);
                return (width, height);
            }
        }
        catch { }
        return (0, 0);
    }

    private async Task<(int Level, string State)?> GetBatteryInfoInternalAsync(string serial, CancellationToken ct)
    {
        try
        {
            var output = await ExecuteCommandAsync(serial, "dumpsys battery", ct);
            int level = 0;
            string state = "Unknown";

            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("level:"))
                    int.TryParse(trimmed.Split(':')[1].Trim(), out level);
                else if (trimmed.StartsWith("status:"))
                {
                    var statusStr = trimmed.Split(':')[1].Trim();
                    state = statusStr switch
                    {
                        "2" => "Charging",
                        "3" => "Discharging",
                        "4" => "Not charging",
                        "5" => "Full",
                        _ => "Unknown"
                    };
                }
            }

            return (level, state);
        }
        catch
        {
            return null;
        }
    }

    private async Task<bool> IsSystemAppAsync(string serial, string packageName, CancellationToken ct)
    {
        try
        {
            var output = await ExecuteCommandAsync(serial, $"pm path {packageName}", ct);
            return output.Contains("/system/");
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckPackageInstalledAsync(string serial, string packageName, CancellationToken ct)
    {
        try
        {
            var output = await ExecuteCommandAsync(serial, $"pm list packages {packageName}", ct);
            return output.Contains(packageName);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckCommandAvailableAsync(string serial, string command, CancellationToken ct)
    {
        try
        {
            var output = await ExecuteCommandAsync(serial, $"which {command}", ct);
            return !string.IsNullOrWhiteSpace(output.Trim());
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> GetLauncherActivityAsync(string serial, string packageName, CancellationToken ct)
    {
        try
        {
            var output = await ExecuteCommandAsync(serial,
                $"cmd package resolve-activity --brief {packageName}", ct);
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 1)
            {
                var activityLine = lines[^1].Trim();
                if (activityLine.Contains('/'))
                    return activityLine;
            }
        }
        catch { }

        try
        {
            var output = await ExecuteCommandAsync(serial,
                $"dumpsys package {packageName} | grep -A 5 \"android.intent.action.MAIN\" | grep -o \"{packageName}/[^\"]+\" | head -1", ct);
            var trimmed = output.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                return trimmed;
        }
        catch { }

        return "MainActivity";
    }

    private string FindAdbExecutable()
    {
        var exeName = OperatingSystem.IsWindows() ? "adb.exe" : "adb";

        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "tools", "platform-tools", exeName),
            Path.Combine(baseDir, "platform-tools", exeName),
            Path.Combine(baseDir, "adb", exeName),
            Path.Combine(baseDir, "..", "tools", "platform-tools", exeName),
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "..", "tools", "platform-tools", exeName),
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "tools", "platform-tools", exeName),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                _logger.LogDebug("Found ADB at: {Path}", fullPath);
                return fullPath;
            }
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(dir.Trim(), exeName);
            if (File.Exists(fullPath))
            {
                _logger.LogDebug("Found ADB in PATH: {Path}", fullPath);
                return fullPath;
            }
        }

        _logger.LogWarning("ADB executable not found, defaulting to 'adb'");
        return exeName;
    }

    private async Task<string> RunAdbCommandAsync(string arguments, CancellationToken ct = default,
        TimeSpan? timeout = null)
    {
        var result = await RunAdbCommandRawAsync(arguments, ct, timeout);
        return result.Output;
    }

    private async Task<(string Output, int ExitCode)> RunAdbCommandRawAsync(
        string arguments, CancellationToken ct = default, TimeSpan? timeout = null)
    {
        await _commandLock.WaitAsync(ct);
        try
        {
            var effectiveTimeout = timeout ?? DefaultCommandTimeout;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(effectiveTimeout);

            _logger.LogDebug("Executing: {AdbPath} {Arguments}", _adbPath, arguments);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _adbPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                },
                EnableRaisingEvents = true
            };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) stdout.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) stderr.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                try { process.Kill(true); } catch { }
                throw new TimeoutException(
                    $"ADB command timed out after {effectiveTimeout.TotalSeconds}s: {_adbPath} {arguments}");
            }

            var outputStr = stdout.ToString().Trim();
            var errorStr = stderr.ToString().Trim();

            if (!string.IsNullOrEmpty(errorStr))
                _logger.LogDebug("ADB stderr: {Error}", errorStr);

            if (process.ExitCode != 0 && !string.IsNullOrEmpty(errorStr))
                _logger.LogWarning("ADB command failed (exit {ExitCode}): {Error}", process.ExitCode, errorStr);

            return (outputStr, process.ExitCode);
        }
        finally
        {
            _commandLock.Release();
        }
    }
}
