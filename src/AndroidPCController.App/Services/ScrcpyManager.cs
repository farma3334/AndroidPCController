using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using AndroidPCController.Core.Interfaces;

namespace AndroidPCController.App.Services;

public sealed class ScrcpyOptions
{
    public string Serial { get; init; } = "";
    public int MaxFps { get; init; }
    public int BitRate { get; init; }
    public string Codec { get; init; } = "H264";
    public int MaxSize { get; init; }
    public bool LowLatency { get; init; } = true;
    public bool AudioEnabled { get; init; }
}

public sealed class ScrcpyStats
{
    public int? Fps { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
}

public sealed class ScrcpyManager
{
    private const string WindowTitle = "AndroidPCController-scrcpy";

    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;
    private Process? _process;
    private IntPtr _windowHandle;

    public bool IsRunning => _process is { HasExited: false };
public IntPtr WindowHandle => _windowHandle;

    public IntPtr HostChildWindow { get; private set; }

    public event EventHandler<IntPtr>? WindowReady;

    public event EventHandler? WindowClosed;

    public event EventHandler<ScrcpyStats>? StatsUpdated;

    public ScrcpyManager(ILogService logService, ISettingsService settingsService)
    {
        _logService = logService;
        _settingsService = settingsService;
    }

    public string FindScrcpyExecutable()
    {
        var exeName = "scrcpy.exe";
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "tools", "scrcpy", "scrcpy-win64-v4.1", exeName),
            Path.Combine(baseDir, "tools", "scrcpy", exeName),
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "tools", "scrcpy", "scrcpy-win64-v4.1", exeName),
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "tools", "scrcpy", exeName),
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "scrcpy", "scrcpy-win64-v4.1", exeName),
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "scrcpy", exeName),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        var toolsDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "tools", "scrcpy"));
        if (Directory.Exists(toolsDir))
        {
            var nested = Directory.GetFiles(toolsDir, exeName, SearchOption.AllDirectories)
                .FirstOrDefault();
            if (nested is not null)
            {
                return nested;
            }
        }

        return exeName;
    }

    public async Task<IntPtr> StartAsync(ScrcpyOptions options, CancellationToken ct = default)
    {
        if (IsRunning)
        {
            return _windowHandle;
        }

        var exePath = FindScrcpyExecutable();
        _logService.Information("Scrcpy", $"Starting scrcpy from {exePath}");

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        psi.ArgumentList.Add($"--serial={options.Serial}");
        psi.ArgumentList.Add($"--window-title={WindowTitle}");
        psi.ArgumentList.Add("--window-borderless");
        psi.ArgumentList.Add("--window-x=-10000");
        psi.ArgumentList.Add("--window-y=-10000");
        psi.ArgumentList.Add("--print-fps");
        if (options.MaxFps > 0) psi.ArgumentList.Add($"--max-fps={options.MaxFps}");
        if (options.BitRate > 0) psi.ArgumentList.Add($"--video-bit-rate={options.BitRate}");
        if (!string.IsNullOrEmpty(options.Codec) && !options.Codec.Equals("H264", StringComparison.OrdinalIgnoreCase))
            psi.ArgumentList.Add($"--video-codec={options.Codec.ToLowerInvariant()}");
        if (options.MaxSize > 0) psi.ArgumentList.Add($"--max-size={options.MaxSize}");
        if (options.LowLatency)
        {
            psi.ArgumentList.Add("--video-buffer=0");
            psi.ArgumentList.Add("--audio-buffer=0");
            psi.ArgumentList.Add("--audio-output-buffer=0");
        }
        if (!options.AudioEnabled) psi.ArgumentList.Add("--no-audio");

        try
        {
            _process = Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logService.Error("Scrcpy", $"Failed to start scrcpy: {ex.Message}", ex);
            throw;
        }

        if (_process is null)
        {
            throw new InvalidOperationException("Failed to start scrcpy process.");
        }

        _process.EnableRaisingEvents = true;
        _process.Exited += OnProcessExited;
        BeginReadingOutput();

        _windowHandle = await FindWindowByTitleAsync(WindowTitle, TimeSpan.FromSeconds(15), ct);
        if (_windowHandle == IntPtr.Zero)
        {
            _logService.Warning("Scrcpy", "scrcpy window not found within timeout");
            return IntPtr.Zero;
        }

        _logService.Information("Scrcpy", $"scrcpy window found: 0x{_windowHandle:X}");
        WindowReady?.Invoke(this, _windowHandle);
        return _windowHandle;
    }

    public void FocusWindow()
    {
        if (_windowHandle == IntPtr.Zero) return;
        SetForegroundWindow(_windowHandle);
        SetFocus(_windowHandle);
    }

    public void SetHost(IntPtr hostChildWindow)
    {
        if (_windowHandle == IntPtr.Zero || hostChildWindow == IntPtr.Zero) return;
        SetParent(_windowHandle, hostChildWindow);
        HostChildWindow = hostChildWindow;
    }

    public void ReleaseHost(IntPtr hostChildWindow)
    {
        if (_windowHandle == IntPtr.Zero || HostChildWindow != hostChildWindow) return;
        SetParent(_windowHandle, IntPtr.Zero);
        HostChildWindow = IntPtr.Zero;
    }

    public async Task StopAsync()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            SendMessage(_windowHandle, 0x0010, IntPtr.Zero, IntPtr.Zero);
            _windowHandle = IntPtr.Zero;
        }

        if (_process is null)
        {
            WindowClosed?.Invoke(this, EventArgs.Empty);
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                await Task.WhenAny(_process.WaitForExitAsync(), Task.Delay(TimeSpan.FromSeconds(5)));
            }
            if (!_process.HasExited)
            {
                _logService.Warning("Scrcpy", "scrcpy did not exit gracefully, killing process");
                _process.Kill(true);
            }
        }
        catch (Exception ex)
        {
            _logService.Error("Scrcpy", $"Error stopping scrcpy: {ex.Message}", ex);
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }

        WindowClosed?.Invoke(this, EventArgs.Empty);
        _logService.Information("Scrcpy", "scrcpy stopped");
    }

    public void Dispose()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(true);
        }
        _process?.Dispose();
        _process = null;
        _windowHandle = IntPtr.Zero;
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        _windowHandle = IntPtr.Zero;
        WindowClosed?.Invoke(this, EventArgs.Empty);
    }

    private void BeginReadingOutput()
    {
        _ = Task.Run(() => ReadStreamAsync(_process!.StandardOutput));
        _ = Task.Run(() => ReadStreamAsync(_process!.StandardError));
    }

    private async Task ReadStreamAsync(StreamReader reader)
    {
        try
        {
            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line is null) break;
                ParseStatsLine(line);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
    }

    private void ParseStatsLine(string line)
    {
        int? fps = null;
        int? width = null;
        int? height = null;

        var fpsMatch = Regex.Match(line, @"(\d+)\s+fps");
        if (fpsMatch.Success && int.TryParse(fpsMatch.Groups[1].Value, out var fpsValue))
        {
            fps = fpsValue;
        }

        var textureMatch = Regex.Match(line, @"Texture:\s*(\d+)x(\d+)");
        if (textureMatch.Success &&
            int.TryParse(textureMatch.Groups[1].Value, out var widthValue) &&
            int.TryParse(textureMatch.Groups[2].Value, out var heightValue))
        {
            width = widthValue;
            height = heightValue;
        }

        if (fps is null && width is null) return;

        StatsUpdated?.Invoke(this, new ScrcpyStats { Fps = fps, Width = width, Height = height });
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    private static async Task<IntPtr> FindWindowByTitleAsync(string title, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var hwnd = FindWindowByTitle(title);
            if (hwnd != IntPtr.Zero)
            {
                return hwnd;
            }

            try
            {
                await Task.Delay(200, ct);
            }
            catch (OperationCanceledException)
            {
                return IntPtr.Zero;
            }
        }

        return IntPtr.Zero;
    }

    private static IntPtr FindWindowByTitle(string title)
    {
        IntPtr found = IntPtr.Zero;

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
            {
                return true;
            }

            var sb = new StringBuilder(512);
            GetWindowText(hwnd, sb, sb.Capacity);
            var windowText = sb.ToString();
            if (windowText == title || windowText.StartsWith(title, StringComparison.Ordinal))
            {
                found = hwnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }
}