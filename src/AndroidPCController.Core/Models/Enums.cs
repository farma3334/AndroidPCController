namespace AndroidPCController.Core.Models;

public enum ConnectionType
{
    Usb,
    Wireless,
    Unknown
}

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Authorized,
    Unauthorized,
    Offline,
    Error
}

public enum DeviceOrientation
{
    Portrait,
    Landscape,
    ReversePortrait,
    ReverseLandscape
}

public enum InputEventType
{
    Tap,
    LongPress,
    DoubleTap,
    Swipe,
    Pinch,
    KeyPress,
    KeyDown,
    KeyUp,
    MouseMove,
    MouseDown,
    MouseUp,
    Scroll,
    Text
}

public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}

public enum TransferState
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

public enum StreamQuality
{
    Low,
    Medium,
    High,
    Ultra,
    Custom
}

public enum AndroidKeyType
{
    Home,
    Back,
    RecentApps,
    Power,
    VolumeUp,
    VolumeDown,
    MediaPlayPause,
    MediaNext,
    MediaPrevious,
    Camera,
    Unknown
}
