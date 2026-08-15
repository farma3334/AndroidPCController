namespace AndroidPCController.Core.Interfaces;

public interface IClipboardService : IAsyncDisposable
{
    string? CurrentContent { get; }
    bool IsSyncEnabled { get; set; }
    Task<string?> GetClipboardTextAsync(CancellationToken ct = default);
    Task SetClipboardTextAsync(string text, CancellationToken ct = default);
    event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;
}

public sealed class ClipboardChangedEventArgs : EventArgs
{
    public required string Text { get; init; }
    public required string Source { get; init; }
}
