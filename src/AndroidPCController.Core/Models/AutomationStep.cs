namespace AndroidPCController.Core.Models;

public sealed class AutomationStep
{
    public required AutomationAction Action { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int EndX { get; init; }
    public int EndY { get; init; }
    public int KeyCode { get; init; }
    public string? Text { get; init; }
    public int DelayMs { get; init; }
    public int DurationMs { get; init; }
    public string? Description { get; init; }
}

public enum AutomationAction
{
    LaunchApp,
    Wait,
    Tap,
    LongPress,
    Swipe,
    PressKey,
    InputText,
    TakeScreenshot,
    StopRecording,
    Back,
    Home,
    Recent,
    Sleep
}

public sealed class AutomationScript
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<AutomationStep> Steps { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public bool LoopForever { get; init; }
    public int LoopCount { get; init; } = 1;
}
