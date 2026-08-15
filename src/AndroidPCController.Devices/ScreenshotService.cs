using AndroidPCController.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AndroidPCController.Devices;

public sealed class ScreenshotService : IScreenshotService
{
    private readonly IAdbClient _adbClient;
    private readonly string _serial;
    private readonly ILogger<ScreenshotService> _logger;
    private bool _disposed;

    public ScreenshotService(IAdbClient adbClient, string serial, ILogger<ScreenshotService> logger)
    {
        _adbClient = adbClient ?? throw new ArgumentNullException(nameof(adbClient));
        _serial = serial ?? throw new ArgumentNullException(nameof(serial));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<byte[]> CaptureAsync(string? format = "png", CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Capturing screenshot (format={Format})", format ?? "png");

        byte[] screenshotData = await _adbClient.TakeScreenshotAsync(_serial, ct).ConfigureAwait(false);
        _logger.LogInformation("Screenshot captured: {Size} bytes", screenshotData.Length);
        return screenshotData;
    }

    public async Task<string> CaptureAndSaveAsync(string directory, string? filename = null, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Directory cannot be null or empty", nameof(directory));

        Directory.CreateDirectory(directory);

        string fileName = filename ?? $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        string localPath = Path.Combine(directory, fileName);

        _logger.LogDebug("Capturing and saving screenshot to {Path}", localPath);

        byte[] data = await CaptureAsync("png", ct).ConfigureAwait(false);
        await File.WriteAllBytesAsync(localPath, data, ct).ConfigureAwait(false);

        _logger.LogInformation("Screenshot saved to {Path} ({Size} bytes)", localPath, data.Length);
        return localPath;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _logger.LogInformation("ScreenshotService disposed for device {Serial}", _serial);
        return ValueTask.CompletedTask;
    }
}
