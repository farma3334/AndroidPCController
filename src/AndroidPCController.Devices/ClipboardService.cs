using AndroidPCController.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Timers;

namespace AndroidPCController.Devices;

public sealed class ClipboardService : IClipboardService
{
    private readonly IAdbClient _adbClient;
    private readonly string _serial;
    private readonly ILogger<ClipboardService> _logger;
    private readonly System.Timers.Timer _pollTimer;
    private readonly object _lock = new();
    private string? _currentContent;
    private bool _isSyncEnabled;
    private bool _disposed;

    public ClipboardService(IAdbClient adbClient, string serial, ILogger<ClipboardService> logger)
    {
        _adbClient = adbClient ?? throw new ArgumentNullException(nameof(adbClient));
        _serial = serial ?? throw new ArgumentNullException(nameof(serial));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pollTimer = new System.Timers.Timer(2000);
        _pollTimer.Elapsed += OnPollTimerElapsed;
    }

    public string? CurrentContent
    {
        get
        {
            lock (_lock) return _currentContent;
        }
        private set
        {
            lock (_lock) _currentContent = value;
        }
    }

    public bool IsSyncEnabled
    {
        get => _isSyncEnabled;
        set
        {
            if (_isSyncEnabled == value) return;
            _isSyncEnabled = value;
            if (value)
                _pollTimer.Start();
            else
                _pollTimer.Stop();
            _logger.LogInformation("Clipboard sync {State}", value ? "enabled" : "disabled");
        }
    }

    public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

    public async Task<string?> GetClipboardTextAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        try
        {
            string? text = await _adbClient.GetClipboardAsync(_serial, ct).ConfigureAwait(false);
            CurrentContent = text;
            _logger.LogDebug("Clipboard content retrieved: {Length} chars", text?.Length ?? 0);
            return text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get clipboard text");
            return null;
        }
    }

    public async Task SetClipboardTextAsync(string text, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Setting clipboard text: {Length} chars", text.Length);
        await _adbClient.SetClipboardAsync(_serial, text, ct).ConfigureAwait(false);
        CurrentContent = text;
    }

    private async void OnPollTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_disposed || !_isSyncEnabled) return;

        try
        {
            string? current = await GetClipboardTextAsync().ConfigureAwait(false);

            string? previous;
            lock (_lock)
            {
                previous = _currentContent;
            }

            if (current is not null && current != previous)
            {
                CurrentContent = current;
                ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs
                {
                    Text = current,
                    Source = "device"
                });
                _logger.LogDebug("Clipboard changed on device: {Preview}", current.Length > 50 ? current[..50] + "..." : current);
            }
        }
        catch (ObjectDisposedException)
        {
            // Expected during disposal
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Clipboard poll failed");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _pollTimer.Stop();
        _pollTimer.Dispose();
        _logger.LogInformation("ClipboardService disposed for device {Serial}", _serial);
        return ValueTask.CompletedTask;
    }
}
