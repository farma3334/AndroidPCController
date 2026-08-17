namespace AndroidPCController.Core.Interfaces;

public interface ISettingsService
{
    T Get<T>(string key, T defaultValue = default!) where T : notnull;
    void Set<T>(string key, T value) where T : notnull;
    void Save();
    void Load();
    event EventHandler<SettingChangedEventArgs>? SettingChanged;
}

public sealed class SettingChangedEventArgs : EventArgs
{
    public required string Key { get; init; }
    public required object? Value { get; init; }
}

public static class SettingKeys
{
    public const string Theme = "App.Theme";
    public const string Language = "App.Language";
    public const string StartMinimized = "App.StartMinimized";
    public const string MinimizeToTray = "App.MinimizeToTray";
    public const string AutoReconnect = "Connection.AutoReconnect";
    public const string ConnectionTimeout = "Connection.Timeout";
    public const string DefaultFps = "Streaming.DefaultFps";
    public const string DefaultBitrate = "Streaming.DefaultBitrate";
    public const string DefaultResolution = "Streaming.DefaultResolution";
    public const string DefaultCodec = "Streaming.DefaultCodec";
    public const string HardwareAcceleration = "Streaming.HardwareAcceleration";
    public const string ClipboardSync = "Privacy.ClipboardSync";
    public const string NotificationSync = "Privacy.NotificationSync";
    public const string UsageAnalytics = "Privacy.UsageAnalytics";
    public const string CrashReports = "Privacy.CrashReports";
    public const string DeviceHistory = "Privacy.DeviceHistory";
    public const string DownloadDirectory = "Files.DownloadDirectory";
    public const string AdbPath = "Advanced.AdbPath";
    public const string DebugLogging = "Advanced.DebugLogging";

    // Input settings
    public const string MouseSensitivity = "Input.MouseSensitivity";
    public const string ScrollSensitivity = "Input.ScrollSensitivity";
    public const string DoubleTapTimeout = "Input.DoubleTapTimeout";
    public const string LongPressDuration = "Input.LongPressDuration";
    public const string ShowTouchFeedback = "Input.ShowTouchFeedback";
    public const string EnableGestures = "Input.EnableGestures";
}
