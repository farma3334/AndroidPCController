using System.Collections.Concurrent;
using AndroidPCController.Core.Interfaces;

namespace AndroidPCController.Infrastructure;

public sealed class LogService : ILogService
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private int _nextId;
    private const int MaxEntries = 10_000;

    private static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AndroidPCController", "logs", "app.log");

    public event EventHandler<LogEntryEventArgs>? LogAdded;

    public void Log(LogLevel level, string category, string message, Exception? exception = null)
    {
        var entry = new LogEntry
        {
            Id = Interlocked.Increment(ref _nextId),
            Level = level,
            Category = category,
            Message = message,
            Exception = exception?.ToString(),
            Timestamp = DateTime.UtcNow
        };

        _entries.Enqueue(entry);

        while (_entries.Count > MaxEntries && _entries.TryDequeue(out _)) { }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
            File.AppendAllText(LogFilePath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {category}: {message}" +
                (exception is null ? string.Empty : $" | {exception}") + Environment.NewLine);
        }
        catch
        {
        }

        LogAdded?.Invoke(this, new LogEntryEventArgs { Entry = entry });
    }

    public void Trace(string category, string message) => Log(LogLevel.Trace, category, message);
    public void Debug(string category, string message) => Log(LogLevel.Debug, category, message);
    public void Information(string category, string message) => Log(LogLevel.Information, category, message);
    public void Warning(string category, string message) => Log(LogLevel.Warning, category, message);
    public void Error(string category, string message, Exception? exception = null) => Log(LogLevel.Error, category, message, exception);
    public void Critical(string category, string message, Exception? exception = null) => Log(LogLevel.Critical, category, message, exception);

    public IReadOnlyList<LogEntry> GetLogs(int count = 1000, LogLevel? minLevel = null, string? category = null)
    {
        var query = _entries.AsEnumerable();

        if (minLevel.HasValue)
            query = query.Where(e => e.Level >= minLevel.Value);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(e => e.Category == category);

        return query.OrderByDescending(e => e.Timestamp).Take(count).ToList();
    }

    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }
}
