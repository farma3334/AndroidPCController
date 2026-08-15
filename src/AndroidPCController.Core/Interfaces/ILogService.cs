namespace AndroidPCController.Core.Interfaces;

public interface ILogService
{
    void Log(LogLevel level, string category, string message, Exception? exception = null);
    void Trace(string category, string message);
    void Debug(string category, string message);
    void Information(string category, string message);
    void Warning(string category, string message);
    void Error(string category, string message, Exception? exception = null);
    void Critical(string category, string message, Exception? exception = null);
    IReadOnlyList<LogEntry> GetLogs(int count = 1000, LogLevel? minLevel = null, string? category = null);
    void Clear();
    event EventHandler<LogEntryEventArgs>? LogAdded;
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

public sealed class LogEntry
{
    public required int Id { get; init; }
    public required LogLevel Level { get; init; }
    public required string Category { get; init; }
    public required string Message { get; init; }
    public string? Exception { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public sealed class LogEntryEventArgs : EventArgs
{
    public required LogEntry Entry { get; init; }
}
