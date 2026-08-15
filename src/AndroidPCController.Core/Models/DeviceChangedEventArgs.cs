using AndroidPCController.Core.Models;

namespace AndroidPCController.Core.Interfaces;

public sealed class DeviceChangedEventArgs : EventArgs
{
    public required string Serial { get; init; }
    public required string ChangeType { get; init; }
    public DeviceInfo? DeviceInfo { get; init; }
}
