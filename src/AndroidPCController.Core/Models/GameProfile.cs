namespace AndroidPCController.Core.Models;

public sealed class GameProfile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? PackageName { get; init; }
    public required IReadOnlyList<KeyMapping> KeyMappings { get; init; }
    public required IReadOnlyList<MouseMapping> MouseMappings { get; init; }
    public float Sensitivity { get; init; } = 1.0f;
    public int DeadZone { get; init; } = 10;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastModified { get; init; } = DateTime.UtcNow;
}

public sealed class KeyMapping
{
    public required int KeyCode { get; init; }
    public required string KeyName { get; init; }
    public required int TouchX { get; init; }
    public required int TouchY { get; init; }
    public int DurationMs { get; init; }
    public bool IsToggle { get; init; }
}

public sealed class MouseMapping
{
    public required string Action { get; init; }
    public required int TouchX { get; init; }
    public required int TouchY { get; init; }
    public int EndX { get; init; }
    public int EndY { get; init; }
    public string? Description { get; init; }
}
