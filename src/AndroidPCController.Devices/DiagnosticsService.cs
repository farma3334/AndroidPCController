using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AndroidPCController.Devices;

public sealed class DiagnosticsService : IDiagnosticsService
{
    private readonly IAdbClient _adbClient;
    private readonly string _serial;
    private readonly ILogger<DiagnosticsService> _logger;

    public DiagnosticsService(IAdbClient adbClient, string serial, ILogger<DiagnosticsService> logger)
    {
        _adbClient = adbClient ?? throw new ArgumentNullException(nameof(adbClient));
        _serial = serial ?? throw new ArgumentNullException(nameof(serial));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DiagnosticResult> RunDiagnosticsAsync(string serial, CancellationToken ct = default)
    {
        _logger.LogInformation("Running diagnostics for device {Serial}", serial);
        var checks = new List<DiagnosticCheck>();

        checks.Add(await CheckDeviceReachableAsync(serial, ct).ConfigureAwait(false));
        checks.Add(await CheckAdbConnectionAsync(serial, ct).ConfigureAwait(false));
        checks.Add(await CheckShellAccessAsync(serial, ct).ConfigureAwait(false));
        checks.Add(await CheckScreenCaptureAsync(serial, ct).ConfigureAwait(false));
        checks.Add(await CheckInputInjectionAsync(serial, ct).ConfigureAwait(false));
        checks.Add(await CheckFileTransferAsync(serial, ct).ConfigureAwait(false));
        checks.Add(await CheckClipboardAccessAsync(serial, ct).ConfigureAwait(false));
        checks.Add(await CheckBatteryStatusAsync(serial, ct).ConfigureAwait(false));
        checks.Add(await CheckStorageSpaceAsync(serial, ct).ConfigureAwait(false));

        bool allPassed = checks.All(c => c.Passed);
        _logger.LogInformation("Diagnostics complete: {Passed}/{Total} checks passed",
            checks.Count(c => c.Passed), checks.Count);

        return new DiagnosticResult
        {
            Serial = serial,
            Checks = checks.AsReadOnly(),
            AllPassed = allPassed,
            Timestamp = DateTime.UtcNow
        };
    }

    public async Task<PerformanceMetrics> GetPerformanceMetricsAsync(string serial, CancellationToken ct = default)
    {
        _logger.LogDebug("Getting performance metrics for {Serial}", serial);

        double cpuUsage = await GetCpuUsageAsync(serial, ct).ConfigureAwait(false);
        (long ramUsed, long ramTotal) = await GetMemoryInfoAsync(serial, ct).ConfigureAwait(false);
        (int batteryLevel, string? batteryState) = await GetBatteryInfoAsync(serial, ct).ConfigureAwait(false);
        double temperature = await GetTemperatureAsync(serial, ct).ConfigureAwait(false);
        double latency = await MeasureLatencyAsync(ct).ConfigureAwait(false);

        return new PerformanceMetrics
        {
            CpuUsage = cpuUsage,
            RamUsedBytes = ramUsed,
            RamTotalBytes = ramTotal,
            BatteryLevel = batteryLevel,
            BatteryState = batteryState,
            Temperature = temperature,
            Latency = TimeSpan.FromMilliseconds(latency),
            Timestamp = DateTime.UtcNow
        };
    }

    public async Task<ConnectionDiagnostic> TestConnectionAsync(string serial, CancellationToken ct = default)
    {
        _logger.LogInformation("Testing connection for device {Serial}", serial);

        bool usbDetected = false;
        bool adbInstalled = false;
        bool deviceDetected = false;
        bool usbDebuggingEnabled = false;
        bool deviceAuthorized = false;
        bool transportInitialized = false;
        bool screenStreamAvailable = false;
        string? errorMessage = null;

        try
        {
            adbInstalled = await CheckAdbInstalledAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            errorMessage = $"ADB check failed: {ex.Message}";
        }

        try
        {
            var devices = await _adbClient.GetDevicesAsync(ct).ConfigureAwait(false);
            deviceDetected = devices.Any(d => d.Serial == serial);
            if (deviceDetected)
            {
                var device = devices.First(d => d.Serial == serial);
                deviceAuthorized = !device.ConnectionState.Contains("unauthorized", StringComparison.OrdinalIgnoreCase);
                transportInitialized = !device.ConnectionState.Contains("offline", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Device detection failed: {ex.Message}";
        }

        try
        {
            await _adbClient.ExecuteCommandAsync(serial, "echo test", ct).ConfigureAwait(false);
            usbDebuggingEnabled = true;
        }
        catch
        {
            usbDebuggingEnabled = false;
        }

        try
        {
            string screenInfo = await _adbClient.GetScreenSizeAsync(serial, ct).ConfigureAwait(false);
            screenStreamAvailable = !string.IsNullOrWhiteSpace(screenInfo) && screenInfo.Contains("x");
        }
        catch
        {
            screenStreamAvailable = false;
        }

        return new ConnectionDiagnostic
        {
            UsbDetected = usbDetected,
            AdbInstalled = adbInstalled,
            DeviceDetected = deviceDetected,
            UsbDebuggingEnabled = usbDebuggingEnabled,
            DeviceAuthorized = deviceAuthorized,
            TransportInitialized = transportInitialized,
            ScreenStreamAvailable = screenStreamAvailable,
            ErrorMessage = errorMessage,
            Solution = !adbInstalled ? "Install ADB and add to PATH" :
                       !deviceDetected ? "Check USB connection and enable USB debugging" :
                       !deviceAuthorized ? "Accept USB debugging authorization on device" :
                       !transportInitialized ? "Restart ADB server: adb kill-server && adb start-server" :
                       null
        };
    }

    private async Task<DiagnosticCheck> CheckDeviceReachableAsync(string serial, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var devices = await _adbClient.GetDevicesAsync(ct).ConfigureAwait(false);
            bool found = devices.Any(d => d.Serial == serial);
            sw.Stop();
            return new DiagnosticCheck
            {
                Name = "Device Reachable",
                Passed = found,
                Message = found ? "Device found" : "Device not found in ADB devices",
                Solution = found ? null : "Check USB connection and USB debugging",
                Duration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DiagnosticCheck
            {
                Name = "Device Reachable",
                Passed = false,
                Message = ex.Message,
                Solution = "Check ADB installation and USB connection",
                Duration = sw.Elapsed
            };
        }
    }

    private async Task<DiagnosticCheck> CheckAdbConnectionAsync(string serial, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _adbClient.ExecuteCommandAsync(serial, "echo ping", ct).ConfigureAwait(false);
            sw.Stop();
            return new DiagnosticCheck
            {
                Name = "ADB Connection",
                Passed = true,
                Message = "Shell command executed successfully",
                Duration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DiagnosticCheck
            {
                Name = "ADB Connection",
                Passed = false,
                Message = ex.Message,
                Solution = "Restart ADB server or reconnect device",
                Duration = sw.Elapsed
            };
        }
    }

    private async Task<DiagnosticCheck> CheckShellAccessAsync(string serial, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            string output = await _adbClient.ExecuteCommandAsync(serial, "whoami", ct).ConfigureAwait(false);
            sw.Stop();
            return new DiagnosticCheck
            {
                Name = "Shell Access",
                Passed = !string.IsNullOrWhiteSpace(output),
                Message = $"Shell user: {output.Trim()}",
                Duration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DiagnosticCheck
            {
                Name = "Shell Access",
                Passed = false,
                Message = ex.Message,
                Solution = "Ensure USB debugging is enabled and device is authorized",
                Duration = sw.Elapsed
            };
        }
    }

    private async Task<DiagnosticCheck> CheckScreenCaptureAsync(string serial, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            byte[] screenshot = await _adbClient.TakeScreenshotAsync(serial, ct).ConfigureAwait(false);
            sw.Stop();
            return new DiagnosticCheck
            {
                Name = "Screen Capture",
                Passed = screenshot.Length > 0,
                Message = $"Screenshot captured: {screenshot.Length} bytes",
                Duration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DiagnosticCheck
            {
                Name = "Screen Capture",
                Passed = false,
                Message = ex.Message,
                Solution = "Check if screen recording is blocking screencap",
                Duration = sw.Elapsed
            };
        }
    }

    private async Task<DiagnosticCheck> CheckInputInjectionAsync(string serial, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _adbClient.ExecuteCommandAsync(serial, "input keyevent --longpress 0", ct).ConfigureAwait(false);
            sw.Stop();
            return new DiagnosticCheck
            {
                Name = "Input Injection",
                Passed = true,
                Message = "Input injection available",
                Duration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DiagnosticCheck
            {
                Name = "Input Injection",
                Passed = false,
                Message = ex.Message,
                Solution = "Input injection may be blocked by device policy",
                Duration = sw.Elapsed
            };
        }
    }

    private async Task<DiagnosticCheck> CheckFileTransferAsync(string serial, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tempFile, "test", ct).ConfigureAwait(false);
                await _adbClient.PushFileAsync(serial, tempFile, "/data/local/tmp/test_write.txt", null, ct).ConfigureAwait(false);
                byte[] pulled = await _adbClient.PullFileAsync(serial, "/data/local/tmp/test_write.txt", ct).ConfigureAwait(false);
                await _adbClient.ExecuteCommandAsync(serial, "rm /data/local/tmp/test_write.txt", ct).ConfigureAwait(false);
                sw.Stop();
                return new DiagnosticCheck
                {
                    Name = "File Transfer",
                    Passed = pulled.Length > 0,
                    Message = "Push/pull file transfer working",
                    Duration = sw.Elapsed
                };
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DiagnosticCheck
            {
                Name = "File Transfer",
                Passed = false,
                Message = ex.Message,
                Solution = "Check storage permissions and available space",
                Duration = sw.Elapsed
            };
        }
    }

    private async Task<DiagnosticCheck> CheckClipboardAccessAsync(string serial, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _adbClient.GetClipboardAsync(serial, ct).ConfigureAwait(false);
            sw.Stop();
            return new DiagnosticCheck
            {
                Name = "Clipboard Access",
                Passed = true,
                Message = "Clipboard read/write available",
                Duration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DiagnosticCheck
            {
                Name = "Clipboard Access",
                Passed = false,
                Message = ex.Message,
                Solution = "Clipboard access may require specific Android version",
                Duration = sw.Elapsed
            };
        }
    }

    private async Task<DiagnosticCheck> CheckBatteryStatusAsync(string serial, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            string batteryInfo = await _adbClient.GetBatteryInfoAsync(serial, ct).ConfigureAwait(false);
            sw.Stop();
            return new DiagnosticCheck
            {
                Name = "Battery Status",
                Passed = !string.IsNullOrWhiteSpace(batteryInfo),
                Message = batteryInfo.Trim(),
                Duration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DiagnosticCheck
            {
                Name = "Battery Status",
                Passed = false,
                Message = ex.Message,
                Duration = sw.Elapsed
            };
        }
    }

    private async Task<DiagnosticCheck> CheckStorageSpaceAsync(string serial, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            string output = await _adbClient.ExecuteCommandAsync(serial, "df /data | tail -1", ct).ConfigureAwait(false);
            sw.Stop();
            bool hasSpace = !output.Contains("0\t0\t0");
            return new DiagnosticCheck
            {
                Name = "Storage Space",
                Passed = hasSpace,
                Message = output.Trim(),
                Solution = hasSpace ? null : "Device storage is full",
                Duration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DiagnosticCheck
            {
                Name = "Storage Space",
                Passed = false,
                Message = ex.Message,
                Duration = sw.Elapsed
            };
        }
    }

    private async Task<bool> CheckAdbInstalledAsync(CancellationToken ct)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "adb",
                Arguments = "version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null) return false;

            string output = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return output.Contains("Android Debug Bridge", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task<double> GetCpuUsageAsync(string serial, CancellationToken ct)
    {
        try
        {
            string output = await _adbClient.ExecuteCommandAsync(
                serial,
                "top -bn1 | grep 'CPU:' | head -1",
                ct).ConfigureAwait(false);

            var match = Regex.Match(output, @"(\d+)%");
            if (match.Success && double.TryParse(match.Groups[1].Value, out double usage))
                return usage;
        }
        catch { }
        return 0;
    }

    private async Task<(long used, long total)> GetMemoryInfoAsync(string serial, CancellationToken ct)
    {
        try
        {
            string output = await _adbClient.ExecuteCommandAsync(
                serial,
                "cat /proc/meminfo | head -2",
                ct).ConfigureAwait(false);

            long total = 0, available = 0;
            foreach (string line in output.Split('\n'))
            {
                if (line.StartsWith("MemTotal:"))
                    total = ParseMemoryValue(line);
                else if (line.StartsWith("MemAvailable:"))
                    available = ParseMemoryValue(line);
            }

            return (total - available, total);
        }
        catch { }
        return (0, 0);
    }

    private async Task<(int level, string? state)> GetBatteryInfoAsync(string serial, CancellationToken ct)
    {
        try
        {
            string output = await _adbClient.GetBatteryInfoAsync(serial, ct).ConfigureAwait(false);
            int level = 0;
            string? state = null;

            var levelMatch = Regex.Match(output, @"level:\s*(\d+)");
            if (levelMatch.Success) int.TryParse(levelMatch.Groups[1].Value, out level);

            var stateMatch = Regex.Match(output, @"status:\s*(\w+)");
            if (stateMatch.Success) state = stateMatch.Groups[1].Value;

            return (level, state);
        }
        catch { }
        return (0, null);
    }

    private async Task<double> GetTemperatureAsync(string serial, CancellationToken ct)
    {
        try
        {
            string output = await _adbClient.ExecuteCommandAsync(
                serial,
                "cat /sys/class/thermal/thermal_zone0/temp",
                ct).ConfigureAwait(false);

            if (double.TryParse(output.Trim(), out double temp))
                return temp / 1000.0;
        }
        catch { }
        return 0;
    }

    private async Task<double> MeasureLatencyAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _adbClient.ExecuteCommandAsync(_serial, "echo ping", ct).ConfigureAwait(false);
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds;
        }
        catch
        {
            sw.Stop();
            return -1;
        }
    }

    private static long ParseMemoryValue(string line)
    {
        var match = Regex.Match(line, @"(\d+)");
        if (match.Success && long.TryParse(match.Groups[1].Value, out long value))
            return value * 1024;
        return 0;
    }
}
