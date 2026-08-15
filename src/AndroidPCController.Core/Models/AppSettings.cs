namespace AndroidPCController.Core.Models;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public string Language { get; set; } = "en";
    public bool StartMinimized { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool AutoReconnect { get; set; } = true;
    public int ConnectionTimeoutMs { get; set; } = 10000;
    public int DefaultFps { get; set; } = 60;
    public int DefaultBitrate { get; set; } = 8_000_000;
    public string DefaultResolution { get; set; } = "Native";
    public string DefaultCodec { get; set; } = "H264";
    public bool HardwareAcceleration { get; set; } = true;
    public bool ClipboardSync { get; set; } = true;
    public bool NotificationSync { get; set; }
    public bool UsageAnalytics { get; set; }
    public bool CrashReports { get; set; } = true;
    public bool DeviceHistory { get; set; } = true;
    public string DownloadDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "AndroidPCController");
    public string? AdbPath { get; set; }
    public bool DebugLogging { get; set; }
}
