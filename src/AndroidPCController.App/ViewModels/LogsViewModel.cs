using System.Collections.ObjectModel;
using System.IO;
using AndroidPCController.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndroidPCController.App.ViewModels;

public partial class LogsViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ILogService _logService;
    private bool _disposed;

    [ObservableProperty]
    private ObservableCollection<LogEntry> _logs = [];

    [ObservableProperty]
    private LogLevel? _filterLevel;

    [ObservableProperty]
    private string _filterCategory = string.Empty;

    [ObservableProperty]
    private bool _isLive = true;

    [ObservableProperty]
    private int _totalLogs;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private bool _filterAll = true;

    [ObservableProperty]
    private bool _filterDebug;

    [ObservableProperty]
    private bool _filterInfo;

    [ObservableProperty]
    private bool _filterWarning;

    [ObservableProperty]
    private bool _filterError;

    [ObservableProperty]
    private ObservableCollection<string> _availableCategories = [];

    [ObservableProperty]
    private string? _selectedCategory;

    public LogsViewModel(ILogService logService)
    {
        _logService = logService;
        _logService.LogAdded += OnLogAdded;
        SelectedCategory = "All";
        _ = LoadInitialLogsAsync();
    }

    partial void OnFilterAllChanged(bool value)
    {
        if (!value) return;
        FilterDebug = FilterInfo = FilterWarning = FilterError = false;
        FilterLevel = null;
        _ = LoadInitialLogsAsync();
    }

    partial void OnFilterDebugChanged(bool value)
    {
        if (!value) return;
        FilterAll = FilterInfo = FilterWarning = FilterError = false;
        FilterLevel = LogLevel.Debug;
        _ = LoadInitialLogsAsync();
    }

    partial void OnFilterInfoChanged(bool value)
    {
        if (!value) return;
        FilterAll = FilterDebug = FilterWarning = FilterError = false;
        FilterLevel = LogLevel.Information;
        _ = LoadInitialLogsAsync();
    }

    partial void OnFilterWarningChanged(bool value)
    {
        if (!value) return;
        FilterAll = FilterDebug = FilterInfo = FilterError = false;
        FilterLevel = LogLevel.Warning;
        _ = LoadInitialLogsAsync();
    }

    partial void OnFilterErrorChanged(bool value)
    {
        if (!value) return;
        FilterAll = FilterDebug = FilterInfo = FilterWarning = false;
        FilterLevel = LogLevel.Error;
        _ = LoadInitialLogsAsync();
    }

    partial void OnSelectedCategoryChanged(string? value)
    {
        FilterCategory = value is null or "All" ? string.Empty : value;
        _ = LoadInitialLogsAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadInitialLogsAsync();
    }

    [RelayCommand]
    private void Clear()
    {
        Logs.Clear();
        _logService.Clear();
        TotalLogs = 0;
        ErrorCount = 0;
        WarningCount = 0;
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export logs",
                Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                var lines = new List<string> { "Timestamp,Level,Category,Message,Exception" };
                foreach (var log in Logs)
                {
                    var escapedMessage = $"\"{log.Message.Replace("\"", "\"\"")}\"";
                    var escapedException = string.IsNullOrEmpty(log.Exception) ? "" : $"\"{log.Exception.Replace("\"", "\"\"")}\"";
                    lines.Add($"{log.Timestamp:O},{log.Level},{log.Category},{escapedMessage},{escapedException}");
                }

                await File.WriteAllLinesAsync(dialog.FileName, lines);
                _logService.Information("Logs", $"Exported {Logs.Count} log entries to {dialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            _logService.Error("Logs", $"Export failed: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private void ApplyFilter()
    {
        _ = LoadInitialLogsAsync();
    }

    private async Task LoadInitialLogsAsync()
    {
        try
        {
            var entries = _logService.GetLogs(1000, FilterLevel, string.IsNullOrWhiteSpace(FilterCategory) ? null : FilterCategory);

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Logs.Clear();
                foreach (var entry in entries) Logs.Add(entry);

                var categories = entries
                    .Select(e => e.Category)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct()
                    .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (AvailableCategories.Count != categories.Count + 1)
                {
                    AvailableCategories.Clear();
                    AvailableCategories.Add("All");
                    foreach (var category in categories) AvailableCategories.Add(category);
                }

                UpdateCounts();
            });
        }
        catch (Exception ex)
        {
            _logService.Error("Logs", $"Failed to load logs: {ex.Message}", ex);
        }

        await Task.CompletedTask;
    }

    private void OnLogAdded(object? sender, LogEntryEventArgs e)
    {
        if (!IsLive) return;

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (FilterLevel.HasValue && e.Entry.Level < FilterLevel.Value) return;
            if (!string.IsNullOrWhiteSpace(FilterCategory) && !e.Entry.Category.Contains(FilterCategory, StringComparison.OrdinalIgnoreCase)) return;

            Logs.Add(e.Entry);
            if (Logs.Count > 2000) Logs.RemoveAt(0);

            TotalLogs = Logs.Count;
            if (e.Entry.Level == LogLevel.Error || e.Entry.Level == LogLevel.Critical) ErrorCount++;
            if (e.Entry.Level == LogLevel.Warning) WarningCount++;
        });
    }

    private void UpdateCounts()
    {
        TotalLogs = Logs.Count;
        ErrorCount = Logs.Count(l => l.Level is LogLevel.Error or LogLevel.Critical);
        WarningCount = Logs.Count(l => l.Level == LogLevel.Warning);
    }

    partial void OnIsLiveChanged(bool value)
    {
        if (value) _ = LoadInitialLogsAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _logService.LogAdded -= OnLogAdded;

        GC.SuppressFinalize(this);
    }
}
