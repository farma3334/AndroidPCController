using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using Microsoft.Extensions.Logging;

namespace AndroidPCController.Input;

public sealed class InputController : IInputController
{
    private readonly IAdbClient _adbClient;
    private readonly string _serial;
    private readonly ILogger<InputController> _logger;
    private bool _disposed;

    public InputController(IAdbClient adbClient, string serial, ILogger<InputController> logger)
    {
        _adbClient = adbClient ?? throw new ArgumentNullException(nameof(adbClient));
        _serial = serial ?? throw new ArgumentNullException(nameof(serial));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsConnected => !_disposed;

    public async Task SendTapAsync(int x, int y, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Tap at ({X}, {Y})", x, y);
        await _adbClient.ExecuteCommandAsync(_serial, $"input tap {x} {y}", ct).ConfigureAwait(false);
    }

    public async Task SendDoubleTapAsync(int x, int y, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Double tap at ({X}, {Y})", x, y);
        await _adbClient.ExecuteCommandAsync(_serial, $"input doubletap {x} {y}", ct).ConfigureAwait(false);
    }

    public async Task SendLongPressAsync(int x, int y, int durationMs = 500, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Long press at ({X}, {Y}) for {Duration}ms", x, y, durationMs);
        await _adbClient.ExecuteCommandAsync(_serial, $"input swipe {x} {y} {x} {y} {durationMs}", ct).ConfigureAwait(false);
    }

    public async Task SendSwipeAsync(int x1, int y1, int x2, int y2, int durationMs = 300, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Swipe from ({X1}, {Y1}) to ({X2}, {Y2}) over {Duration}ms", x1, y1, x2, y2, durationMs);
        await _adbClient.ExecuteCommandAsync(_serial, $"input swipe {x1} {y1} {x2} {y2} {durationMs}", ct).ConfigureAwait(false);
    }

    public async Task SendPinchAsync(int x, int y, float scale, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Pinch at ({X}, {Y}) with scale {Scale}", x, y, scale);
        const int baseDistance = 200;
        int offset = (int)(baseDistance * (scale - 1.0f) / 2.0f);

        int x1 = x - offset;
        int y1 = y - offset;
        int x2 = x + offset;
        int y2 = y + offset;

        await _adbClient.ExecuteCommandAsync(
            _serial,
            $"input swipe {x1} {y1} {x2} {y2} 200 & input swipe {x2} {y2} {x1} {y1} 200",
            ct).ConfigureAwait(false);
    }

    public async Task SendKeyEventAsync(int keyCode, bool isDown = true, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Key event: keyCode={KeyCode}, isDown={IsDown}", keyCode, isDown);
        if (isDown)
        {
            await _adbClient.SendKeyEventAsync(_serial, keyCode, ct).ConfigureAwait(false);
        }
        else
        {
            string command = $"input keyevent --longpress {keyCode}";
            await _adbClient.ExecuteCommandAsync(_serial, command, ct).ConfigureAwait(false);
        }
    }

    public async Task SendTextAsync(string text, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrEmpty(text))
        {
            _logger.LogWarning("Attempted to send empty text");
            return;
        }
        _logger.LogDebug("Sending text: {TextLength} characters", text.Length);
        string escaped = text.Replace(" ", "%s").Replace("&", "\\&").Replace("<", "\\<").Replace(">", "\\>").Replace("'", "\\'").Replace("\"", "\\\"");
        await _adbClient.SendTextAsync(_serial, escaped, ct).ConfigureAwait(false);
    }

    public async Task SendMouseAsync(int x, int y, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Mouse move to ({X}, {Y})", x, y);
        await _adbClient.ExecuteCommandAsync(_serial, $"input mouse {x} {y}", ct).ConfigureAwait(false);
    }

    public async Task SendScrollAsync(int x, int y, int scrollAmount, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Scroll at ({X}, {Y}) amount={Amount}", x, y, scrollAmount);
        string direction = scrollAmount > 0 ? "down" : "up";
        int count = Math.Abs(scrollAmount);
        for (int i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            await _adbClient.ExecuteCommandAsync(_serial, $"input swipe {x} {y} {x} {y - 100} 100", ct).ConfigureAwait(false);
        }
    }

    public async Task PressHomeAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Pressing Home");
        await _adbClient.SendKeyEventAsync(_serial, 3, ct).ConfigureAwait(false);
    }

    public async Task PressBackAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Pressing Back");
        await _adbClient.SendKeyEventAsync(_serial, 4, ct).ConfigureAwait(false);
    }

    public async Task PressRecentAppsAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Pressing Recent Apps");
        await _adbClient.SendKeyEventAsync(_serial, 187, ct).ConfigureAwait(false);
    }

    public async Task RotateScreenAsync(DeviceOrientation orientation, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Rotating screen to {Orientation}", orientation);

        int rotation = orientation switch
        {
            DeviceOrientation.Portrait => 0,
            DeviceOrientation.Landscape => 1,
            DeviceOrientation.ReversePortrait => 2,
            DeviceOrientation.ReverseLandscape => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, "Unsupported orientation")
        };

        await _adbClient.ExecuteCommandAsync(_serial, $"settings put system user_rotation {rotation}", ct).ConfigureAwait(false);
        await _adbClient.ExecuteCommandAsync(_serial, "settings put system accelerometer_rotation 0", ct).ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _logger.LogInformation("InputController disposed for device {Serial}", _serial);
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
