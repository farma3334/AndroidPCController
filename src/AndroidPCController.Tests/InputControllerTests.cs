using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using AndroidPCController.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AndroidPCController.Tests;

public sealed class InputControllerTests
{
    private readonly FakeAdbClient _fakeAdb = new();
    private readonly ILogger<InputController> _logger = NullLogger<InputController>.Instance;
    private const string TestSerial = "TEST123";

    private InputController CreateSut() => new(_fakeAdb, TestSerial, _logger);

    [Fact]
    public async Task PressHomeAsync_SendsKeyEventWithCode3()
    {
        await using var sut = CreateSut();

        await sut.PressHomeAsync();

        Assert.Single(_fakeAdb.SendKeyEvents);
        Assert.Equal(3, _fakeAdb.SendKeyEvents[0].keyCode);
    }

    [Fact]
    public async Task PressBackAsync_SendsKeyEventWithCode4()
    {
        await using var sut = CreateSut();

        await sut.PressBackAsync();

        Assert.Single(_fakeAdb.SendKeyEvents);
        Assert.Equal(4, _fakeAdb.SendKeyEvents[0].keyCode);
    }

    [Fact]
    public async Task PressRecentAppsAsync_SendsKeyEventWithCode187()
    {
        await using var sut = CreateSut();

        await sut.PressRecentAppsAsync();

        Assert.Single(_fakeAdb.SendKeyEvents);
        Assert.Equal(187, _fakeAdb.SendKeyEvents[0].keyCode);
    }

    [Fact]
    public async Task SendKeyEventAsync_Down_SendsKeyViaSendKeyEvent()
    {
        await using var sut = CreateSut();

        await sut.SendKeyEventAsync(26, isDown: true);

        Assert.Single(_fakeAdb.SendKeyEvents);
        Assert.Equal(26, _fakeAdb.SendKeyEvents[0].keyCode);
    }

    [Fact]
    public async Task SendKeyEventAsync_Up_SendsLongpress()
    {
        await using var sut = CreateSut();

        await sut.SendKeyEventAsync(26, isDown: false);

        Assert.Single(_fakeAdb.Commands);
        Assert.Contains("26", _fakeAdb.Commands[0]);
        Assert.Contains("longpress", _fakeAdb.Commands[0]);
    }

    [Fact]
    public async Task SendTapAsync_FormatsCorrectCommand()
    {
        await using var sut = CreateSut();

        await sut.SendTapAsync(100, 200);

        Assert.Single(_fakeAdb.Commands);
        Assert.Equal("input tap 100 200", _fakeAdb.Commands[0]);
    }

    [Fact]
    public async Task SendDoubleTapAsync_FormatsCorrectCommand()
    {
        await using var sut = CreateSut();

        await sut.SendDoubleTapAsync(50, 75);

        Assert.Single(_fakeAdb.Commands);
        Assert.Equal("input doubletap 50 75", _fakeAdb.Commands[0]);
    }

    [Fact]
    public async Task SendLongPressAsync_FormatsCorrectSwipeCommand()
    {
        await using var sut = CreateSut();

        await sut.SendLongPressAsync(100, 200, durationMs: 1000);

        Assert.Single(_fakeAdb.Commands);
        Assert.Equal("input swipe 100 200 100 200 1000", _fakeAdb.Commands[0]);
    }

    [Fact]
    public async Task SendSwipeAsync_FormatsCorrectCommand()
    {
        await using var sut = CreateSut();

        await sut.SendSwipeAsync(10, 20, 300, 400, durationMs: 500);

        Assert.Single(_fakeAdb.Commands);
        Assert.Equal("input swipe 10 20 300 400 500", _fakeAdb.Commands[0]);
    }

    [Fact]
    public async Task SendTextAsync_EscapesSpaces()
    {
        await using var sut = CreateSut();

        await sut.SendTextAsync("hello world");

        Assert.Single(_fakeAdb.Texts);
        Assert.Equal("hello%sworld", _fakeAdb.Texts[0]);
    }

    [Fact]
    public async Task SendTextAsync_EscapesSpecialChars()
    {
        await using var sut = CreateSut();

        await sut.SendTextAsync("a&b<c>d'e\"f");

        Assert.Single(_fakeAdb.Texts);
        Assert.Equal("a\\&b\\<c\\>d\\'e\\\"f", _fakeAdb.Texts[0]);
    }

    [Fact]
    public async Task SendTextAsync_EmptyText_DoesNotSend()
    {
        await using var sut = CreateSut();

        await sut.SendTextAsync("");

        Assert.Empty(_fakeAdb.Texts);
    }

    [Fact]
    public async Task SendMouseAsync_FormatsCorrectCommand()
    {
        await using var sut = CreateSut();

        await sut.SendMouseAsync(500, 300);

        Assert.Single(_fakeAdb.Commands);
        Assert.Equal("input mouse 500 300", _fakeAdb.Commands[0]);
    }

    [Fact]
    public async Task RotateScreenAsync_Portrait_SetsRotation0()
    {
        await using var sut = CreateSut();

        await sut.RotateScreenAsync(DeviceOrientation.Portrait);

        Assert.Contains(_fakeAdb.Commands, c => c == "settings put system user_rotation 0");
        Assert.Contains(_fakeAdb.Commands, c => c == "settings put system accelerometer_rotation 0");
    }

    [Fact]
    public async Task RotateScreenAsync_Landscape_SetsRotation1()
    {
        await using var sut = CreateSut();

        await sut.RotateScreenAsync(DeviceOrientation.Landscape);

        Assert.Contains(_fakeAdb.Commands, c => c == "settings put system user_rotation 1");
    }

    [Fact]
    public async Task RotateScreenAsync_ReversePortrait_SetsRotation2()
    {
        await using var sut = CreateSut();

        await sut.RotateScreenAsync(DeviceOrientation.ReversePortrait);

        Assert.Contains(_fakeAdb.Commands, c => c == "settings put system user_rotation 2");
    }

    [Fact]
    public async Task RotateScreenAsync_ReverseLandscape_SetsRotation3()
    {
        await using var sut = CreateSut();

        await sut.RotateScreenAsync(DeviceOrientation.ReverseLandscape);

        Assert.Contains(_fakeAdb.Commands, c => c == "settings put system user_rotation 3");
    }

    [Fact]
    public void IsConnected_True_WhenNotDisposed()
    {
        var sut = CreateSut();
        Assert.True(sut.IsConnected);
    }

    [Fact]
    public async Task IsConnected_False_WhenDisposed()
    {
        var sut = CreateSut();
        await sut.DisposeAsync();
        Assert.False(sut.IsConnected);
    }

    [Fact]
    public async Task SendScrollAsync_PositiveAmount_SendsDownSwipes()
    {
        await using var sut = CreateSut();

        await sut.SendScrollAsync(500, 500, scrollAmount: 2);

        Assert.Equal(2, _fakeAdb.Commands.Count);
        Assert.All(_fakeAdb.Commands, c => Assert.Contains("500 500 500 400", c));
    }

    [Fact]
    public async Task SendScrollAsync_NegativeAmount_SendsUpSwipes()
    {
        await using var sut = CreateSut();

        await sut.SendScrollAsync(500, 500, scrollAmount: -1);

        Assert.Single(_fakeAdb.Commands);
        Assert.Contains("500 500 500 400", _fakeAdb.Commands[0]);
    }

    [Fact]
    public async Task SendPinchAsync_ComputesSwipeCoordinates()
    {
        await using var sut = CreateSut();

        await sut.SendPinchAsync(500, 500, 2.0f);

        Assert.Contains("&", _fakeAdb.Commands[0]);
    }

    private sealed class FakeAdbClient : IAdbClient
    {
        public List<string> Commands { get; } = new();
        public List<string> Texts { get; } = new();
        public List<(string serial, int keyCode)> SendKeyEvents { get; } = new();
        public List<(string serial, int x, int y)> TouchEvents { get; } = new();
        public List<(string serial, string text)> SendTexts { get; } = new();

        public Task<string> GetVersionAsync(CancellationToken ct = default) => Task.FromResult("1.0");
        public Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DeviceInfo>>(Array.Empty<DeviceInfo>());
        public Task<DeviceInfo?> GetDeviceInfoAsync(string serial, CancellationToken ct = default) => Task.FromResult<DeviceInfo?>(null);
        public Task<bool> IsServerRunningAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task StartServerAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task StopServerAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<DeviceCapabilities> GetCapabilitiesAsync(string serial, CancellationToken ct = default) => Task.FromResult(new DeviceCapabilities());
        public Task<string> ExecuteCommandAsync(string serial, string command, CancellationToken ct = default) { Commands.Add(command); return Task.FromResult("OK"); }
        public Task<byte[]> ExecuteCommandBytesAsync(string serial, string command, CancellationToken ct = default) => Task.FromResult(Array.Empty<byte>());
        public Task<byte[]> PullFileAsync(string serial, string remotePath, CancellationToken ct = default) => Task.FromResult(Array.Empty<byte>());
        public Task PushFileAsync(string serial, string localPath, string remotePath, IProgress<TransferProgress>? progress = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<AndroidAppInfo>> GetInstalledAppsAsync(string serial, bool includeSystem = false, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<AndroidAppInfo>>(Array.Empty<AndroidAppInfo>());
        public Task<string?> GetClipboardAsync(string serial, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task SetClipboardAsync(string serial, string text, CancellationToken ct = default) => Task.CompletedTask;
        public Task<byte[]> TakeScreenshotAsync(string serial, CancellationToken ct = default) => Task.FromResult(Array.Empty<byte>());
        public Task<string> GetBatteryInfoAsync(string serial, CancellationToken ct = default) => Task.FromResult("100");
        public Task<string> GetScreenSizeAsync(string serial, CancellationToken ct = default) => Task.FromResult("1080x2400");
        public Task ConnectWirelessAsync(string host, int port, CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectWirelessAsync(string host, int port, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> PairWirelessAsync(string host, int port, string code, CancellationToken ct = default) => Task.FromResult(0);
        public Task InstallApkAsync(string serial, string apkPath, IProgress<TransferProgress>? progress = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task UninstallAppAsync(string serial, string packageName, CancellationToken ct = default) => Task.CompletedTask;
        public Task LaunchAppAsync(string serial, string packageName, CancellationToken ct = default) => Task.CompletedTask;
        public Task ForceStopAppAsync(string serial, string packageName, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearAppDataAsync(string serial, string packageName, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string> GetLogcatAsync(string serial, int lineCount = 500, CancellationToken ct = default) => Task.FromResult("");
        public Task SendKeyEventAsync(string serial, int keyCode, CancellationToken ct = default) { SendKeyEvents.Add((serial, keyCode)); return Task.CompletedTask; }
        public Task SendTouchEventAsync(string serial, int x, int y, InputEventType type, CancellationToken ct = default) { TouchEvents.Add((serial, x, y)); return Task.CompletedTask; }
        public Task SendSwipeEventAsync(string serial, int x1, int y1, int x2, int y2, int durationMs, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendTextAsync(string serial, string text, CancellationToken ct = default) { SendTexts.Add((serial, text)); Texts.Add(text); return Task.CompletedTask; }
        public event EventHandler<DeviceChangedEventArgs>? DeviceChanged;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
