namespace AndroidPCController.Core.Models;

public sealed class DeviceCapabilities
{
    public bool ScreenStreaming { get; set; }
    public bool AudioStreaming { get; set; }
    public bool Clipboard { get; set; }
    public bool FileTransfer { get; set; }
    public bool RemoteInput { get; set; }
    public bool Notifications { get; set; }
    public bool ScreenRecording { get; set; }
    public bool Screenshot { get; set; }
    public bool ShellAccess { get; set; }
    public bool InputInjection { get; set; }
    public bool MediaProjection { get; set; }
    public bool AccessibilityService { get; set; }
    public bool H264Support { get; set; }
    public bool H265Support { get; set; }
    public int MaxFps { get; set; } = 60;
    public int MaxResolution { get; set; } = 1920;
    public int ApiLevel { get; set; }
    public string? AndroidVersion { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
}
