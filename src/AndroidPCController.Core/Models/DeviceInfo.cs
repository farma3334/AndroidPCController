namespace AndroidPCController.Core.Models;

public sealed class DeviceInfo
{
    public required string Serial { get; init; }
    public required string Model { get; init; }
    public required string Manufacturer { get; init; }
    public required string AndroidVersion { get; init; }
    public required int ApiLevel { get; init; }
    public required string ProductName { get; init; }
    public required string DeviceName { get; init; }
    public required int ScreenWidth { get; init; }
    public required int ScreenHeight { get; init; }
    public required int ScreenDensity { get; init; }
    public required string ConnectionState { get; init; }
    public required ConnectionType ConnectionType { get; init; }
    public string? IpAddress { get; init; }
    public int? BatteryLevel { get; init; }
    public string? BatteryState { get; init; }
    public string? BuildNumber { get; init; }
    public string? SecurityPatch { get; init; }
    public string? KernelVersion { get; init; }
    public string? Chipset { get; init; }
    public string? CpuInfo { get; init; }
    public string? GpuInfo { get; init; }
    public long? TotalRam { get; init; }
    public long? TotalStorage { get; init; }
    public string? BluetoothAddress { get; init; }
    public string? UsbProductId { get; init; }
    public string? UsbVendorId { get; init; }
    public DateTime LastSeen { get; init; } = DateTime.UtcNow;
}
