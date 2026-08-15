using AndroidPCController.Core.Models;

namespace AndroidPCController.Core.Interfaces;

public interface IDiagnosticsService
{
    Task<DiagnosticResult> RunDiagnosticsAsync(string serial, CancellationToken ct = default);
    Task<PerformanceMetrics> GetPerformanceMetricsAsync(string serial, CancellationToken ct = default);
    Task<ConnectionDiagnostic> TestConnectionAsync(string serial, CancellationToken ct = default);
}

public sealed class DiagnosticResult
{
    public required string Serial { get; init; }
    public required IReadOnlyList<DiagnosticCheck> Checks { get; init; }
    public required bool AllPassed { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public sealed class DiagnosticCheck
{
    public required string Name { get; init; }
    public required bool Passed { get; init; }
    public string? Message { get; init; }
    public string? Solution { get; init; }
    public TimeSpan Duration { get; init; }
}

public sealed class PerformanceMetrics
{
    public double CpuUsage { get; init; }
    public long RamUsedBytes { get; init; }
    public long RamTotalBytes { get; init; }
    public int BatteryLevel { get; init; }
    public string? BatteryState { get; init; }
    public double Temperature { get; init; }
    public double CurrentFps { get; init; }
    public double NetworkMbps { get; init; }
    public TimeSpan Latency { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public sealed class ConnectionDiagnostic
{
    public required bool UsbDetected { get; init; }
    public required bool AdbInstalled { get; init; }
    public required bool DeviceDetected { get; init; }
    public required bool UsbDebuggingEnabled { get; init; }
    public required bool DeviceAuthorized { get; init; }
    public required bool TransportInitialized { get; init; }
    public required bool ScreenStreamAvailable { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Solution { get; init; }
}
