using AndroidPCController.Core.Models;

namespace AndroidPCController.Core.Interfaces;

public interface IInputController : IAsyncDisposable
{
    bool IsConnected { get; }
    Task SendTapAsync(int x, int y, CancellationToken ct = default);
    Task SendDoubleTapAsync(int x, int y, CancellationToken ct = default);
    Task SendLongPressAsync(int x, int y, int durationMs = 500, CancellationToken ct = default);
    Task SendSwipeAsync(int x1, int y1, int x2, int y2, int durationMs = 300, CancellationToken ct = default);
    Task SendPinchAsync(int x, int y, float scale, CancellationToken ct = default);
    Task SendKeyEventAsync(int keyCode, bool isDown = true, CancellationToken ct = default);
    Task SendTextAsync(string text, CancellationToken ct = default);
    Task SendMouseAsync(int x, int y, CancellationToken ct = default);
    Task SendScrollAsync(int x, int y, int scrollAmount, CancellationToken ct = default);
    Task PressHomeAsync(CancellationToken ct = default);
    Task PressBackAsync(CancellationToken ct = default);
    Task PressRecentAppsAsync(CancellationToken ct = default);
    Task RotateScreenAsync(DeviceOrientation orientation, CancellationToken ct = default);
}
