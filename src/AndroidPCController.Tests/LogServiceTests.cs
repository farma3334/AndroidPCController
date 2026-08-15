using AndroidPCController.Core.Interfaces;
using AndroidPCController.Infrastructure;

namespace AndroidPCController.Tests;

public sealed class LogServiceTests
{
    private readonly LogService _sut = new();

    [Fact]
    public void Log_AddsEntry_EntryCanBeRetrieved()
    {
        _sut.Log(LogLevel.Information, "Test", "Test message");

        var logs = _sut.GetLogs();

        Assert.Single(logs);
        Assert.Equal("Test message", logs[0].Message);
    }

    [Fact]
    public void Log_EntryHasIncrementingIds()
    {
        _sut.Log(LogLevel.Information, "Test", "First");
        _sut.Log(LogLevel.Information, "Test", "Second");
        _sut.Log(LogLevel.Information, "Test", "Third");

        var logs = _sut.GetLogs(count: 3);

        Assert.Equal(1, logs[2].Id);
        Assert.Equal(2, logs[1].Id);
        Assert.Equal(3, logs[0].Id);
    }

    [Fact]
    public void GetLogs_ReturnsInReverseChronologicalOrder()
    {
        _sut.Log(LogLevel.Information, "Test", "Oldest");
        Thread.Sleep(10);
        _sut.Log(LogLevel.Information, "Test", "Middle");
        Thread.Sleep(10);
        _sut.Log(LogLevel.Information, "Test", "Newest");

        var logs = _sut.GetLogs(count: 3);

        Assert.Equal("Newest", logs[0].Message);
        Assert.Equal("Middle", logs[1].Message);
        Assert.Equal("Oldest", logs[2].Message);
    }

    [Fact]
    public void GetLogs_WithMinLevelFilter_ReturnsOnlyMatchingEntries()
    {
        _sut.Log(LogLevel.Trace, "Test", "Trace message");
        _sut.Log(LogLevel.Debug, "Test", "Debug message");
        _sut.Log(LogLevel.Information, "Test", "Info message");
        _sut.Log(LogLevel.Warning, "Test", "Warn message");
        _sut.Log(LogLevel.Error, "Test", "Error message");

        var logs = _sut.GetLogs(minLevel: LogLevel.Warning);

        Assert.Equal(2, logs.Count);
        Assert.All(logs, e => Assert.True(e.Level >= LogLevel.Warning));
    }

    [Fact]
    public void GetLogs_WithCategoryFilter_ReturnsOnlyMatchingCategory()
    {
        _sut.Log(LogLevel.Information, "A", "A message");
        _sut.Log(LogLevel.Information, "B", "B message");
        _sut.Log(LogLevel.Information, "A", "A message 2");

        var logs = _sut.GetLogs(category: "A");

        Assert.Equal(2, logs.Count);
        Assert.All(logs, e => Assert.Equal("A", e.Category));
    }

    [Fact]
    public void GetLogs_WithBothFilters_ReturnsIntersection()
    {
        _sut.Log(LogLevel.Trace, "A", "Trace A");
        _sut.Log(LogLevel.Warning, "A", "Warn A");
        _sut.Log(LogLevel.Warning, "B", "Warn B");
        _sut.Log(LogLevel.Error, "A", "Error A");

        var logs = _sut.GetLogs(minLevel: LogLevel.Warning, category: "A");

        Assert.Equal(2, logs.Count);
        Assert.All(logs, e =>
        {
            Assert.Equal("A", e.Category);
            Assert.True(e.Level >= LogLevel.Warning);
        });
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        _sut.Log(LogLevel.Information, "Test", "Message 1");
        _sut.Log(LogLevel.Information, "Test", "Message 2");

        _sut.Clear();

        var logs = _sut.GetLogs();
        Assert.Empty(logs);
    }

    [Fact]
    public void MaxEntries_Limit_OldestRemovedWhenExceeded()
    {
        for (int i = 0; i < 10_050; i++)
        {
            _sut.Log(LogLevel.Information, "Test", $"Message {i}");
        }

        var logs = _sut.GetLogs(count: 10_000);

        Assert.Equal(10_000, logs.Count);
        Assert.Contains(logs, e => e.Message == "Message 10049");
        Assert.DoesNotContain(logs, e => e.Message == "Message 0");
    }

    [Fact]
    public void LogAdded_Event_FiresWhenLogIsAdded()
    {
        LogEntryEventArgs? eventArgs = null;
        _sut.LogAdded += (_, e) => eventArgs = e;

        _sut.Log(LogLevel.Information, "Test", "Event test");

        Assert.NotNull(eventArgs);
        Assert.Equal("Event test", eventArgs!.Entry.Message);
    }

    [Fact]
    public void LogAdded_Event_IncludesCorrectEntry()
    {
        LogEntryEventArgs? eventArgs = null;
        _sut.LogAdded += (_, e) => eventArgs = e;

        _sut.Log(LogLevel.Warning, "TestCategory", "Warning message");

        Assert.Equal(LogLevel.Warning, eventArgs!.Entry.Level);
        Assert.Equal("TestCategory", eventArgs.Entry.Category);
    }

    [Fact]
    public void Trace_LogsTraceLevel()
    {
        _sut.Trace("Cat", "trace msg");

        var logs = _sut.GetLogs();
        Assert.Single(logs);
        Assert.Equal(LogLevel.Trace, logs[0].Level);
    }

    [Fact]
    public void Debug_LogsDebugLevel()
    {
        _sut.Debug("Cat", "debug msg");

        var logs = _sut.GetLogs();
        Assert.Equal(LogLevel.Debug, logs[0].Level);
    }

    [Fact]
    public void Information_LogsInformationLevel()
    {
        _sut.Information("Cat", "info msg");

        var logs = _sut.GetLogs();
        Assert.Equal(LogLevel.Information, logs[0].Level);
    }

    [Fact]
    public void Warning_LogsWarningLevel()
    {
        _sut.Warning("Cat", "warn msg");

        var logs = _sut.GetLogs();
        Assert.Equal(LogLevel.Warning, logs[0].Level);
    }

    [Fact]
    public void Error_LogsErrorLevel()
    {
        _sut.Error("Cat", "error msg");

        var logs = _sut.GetLogs();
        Assert.Equal(LogLevel.Error, logs[0].Level);
    }

    [Fact]
    public void Critical_LogsCriticalLevel()
    {
        _sut.Critical("Cat", "critical msg");

        var logs = _sut.GetLogs();
        Assert.Equal(LogLevel.Critical, logs[0].Level);
    }

    [Fact]
    public void Error_WithException_RecordsExceptionString()
    {
        var ex = new InvalidOperationException("test exception");

        _sut.Error("Cat", "Error with ex", ex);

        var logs = _sut.GetLogs();
        Assert.Contains("test exception", logs[0].Exception!);
        Assert.Contains("InvalidOperationException", logs[0].Exception!);
    }

    [Fact]
    public void Log_EntryHasTimestamp()
    {
        var before = DateTime.UtcNow;

        _sut.Log(LogLevel.Information, "Test", "timestamped");

        var logs = _sut.GetLogs();
        var after = DateTime.UtcNow;

        Assert.InRange(logs[0].Timestamp, before, after);
    }

    [Fact]
    public void GetLogs_CountLimitsResults()
    {
        for (int i = 0; i < 100; i++)
        {
            _sut.Log(LogLevel.Information, "Test", $"msg {i}");
        }

        var logs = _sut.GetLogs(count: 10);

        Assert.Equal(10, logs.Count);
    }

    [Fact]
    public void GetLogs_ReturnsEmptyWhenNoEntries()
    {
        var logs = _sut.GetLogs();

        Assert.Empty(logs);
    }

    [Fact]
    public void Clear_EventListeners_StillWorkAfterClear()
    {
        LogEntryEventArgs? eventArgs = null;
        _sut.LogAdded += (_, e) => eventArgs = e;

        _sut.Clear();
        _sut.Log(LogLevel.Information, "Test", "After clear");

        Assert.NotNull(eventArgs);
        Assert.Equal("After clear", eventArgs!.Entry.Message);
    }
}
