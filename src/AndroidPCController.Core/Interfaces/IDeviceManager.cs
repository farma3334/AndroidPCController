using AndroidPCController.Core.Models;

namespace AndroidPCController.Core.Interfaces;

public interface IDeviceManager : IAsyncDisposable
{
    IReadOnlyList<IDeviceSession> ActiveSessions { get; }
    Task<IDeviceSession> ConnectAsync(string serial, ConnectionType type = ConnectionType.Usb, CancellationToken ct = default);
    Task<IDeviceSession> ConnectWirelessAsync(string host, int port, string? pairingCode = null, CancellationToken ct = default);
    Task DisconnectAsync(string serial, CancellationToken ct = default);
    Task<IReadOnlyList<DeviceInfo>> GetAvailableDevicesAsync(CancellationToken ct = default);
    Task<DeviceInfo?> GetDeviceInfoAsync(string serial, CancellationToken ct = default);
    IDeviceSession? GetSession(string serial);
    event EventHandler<DeviceConnectedEventArgs>? DeviceConnected;
    event EventHandler<DeviceDisconnectedEventArgs>? DeviceDisconnected;
}

public sealed class DeviceConnectedEventArgs : EventArgs
{
    public required DeviceInfo Device { get; init; }
    public required IDeviceSession Session { get; init; }
}

public sealed class DeviceDisconnectedEventArgs : EventArgs
{
    public required string Serial { get; init; }
    public string? Reason { get; init; }
}
