namespace AndroidPCController.Core.Models;

public sealed class InputEvent
{
    public required InputEventType Type { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public int EndX { get; init; }
    public int EndY { get; init; }
    public int KeyCode { get; init; }
    public string? Text { get; init; }
    public int DurationMs { get; init; }
    public int PointerId { get; init; }
    public float Pressure { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public sealed class KeyEvent
{
    public required int KeyCode { get; init; }
    public required bool IsDown { get; init; }
    public int MetaState { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public sealed class TextEvent
{
    public required string Text { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
