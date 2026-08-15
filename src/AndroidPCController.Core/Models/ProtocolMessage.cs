namespace AndroidPCController.Core.Models;

public sealed class ProtocolMessage
{
    public int Version { get; init; } = 1;
    public required string MessageType { get; init; }
    public string? RequestId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public object? Payload { get; init; }
}

public static class MessageTypes
{
    public const string Hello = "HELLO";
    public const string DeviceInfo = "DEVICE_INFO";
    public const string Capabilities = "CAPABILITIES";
    public const string StartStream = "START_STREAM";
    public const string StopStream = "STOP_STREAM";
    public const string InputEvent = "INPUT_EVENT";
    public const string ClipboardUpdate = "CLIPBOARD_UPDATE";
    public const string FileRequest = "FILE_REQUEST";
    public const string FileResponse = "FILE_RESPONSE";
    public const string Ping = "PING";
    public const string Pong = "PONG";
    public const string Error = "ERROR";
    public const string Goodbye = "GOODBYE";
    public const string Screenshot = "SCREENSHOT";
    public const string ScreenRecording = "SCREEN_RECORDING";
    public const string KeyEvent = "KEY_EVENT";
    public const string TouchEvent = "TOUCH_EVENT";
    public const string TextEvent = "TEXT_EVENT";
    public const string AppList = "APP_LIST";
    public const string InstallApk = "INSTALL_APK";
    public const string UninstallApp = "UNINSTALL_APP";
    public const string LaunchApp = "LAUNCH_APP";
    public const string ShellCommand = "SHELL_COMMAND";
    public const string Logcat = "LOGCAT";
}
