using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using AndroidPCController.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndroidPCController.App.ViewModels;

[ObservableObject]
public partial class ScreenshotsViewModel : IAsyncDisposable
{
    private readonly IDeviceManager _deviceManager;
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;
    private IDeviceSession? _currentSession;
    private bool _disposed;
    private readonly string _screenshotsDirectory;

    [ObservableProperty]
    private ObservableCollection<ScreenshotItem> _screenshots = [];

    [ObservableProperty]
    private ScreenshotItem? _selectedScreenshot;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private BitmapImage? _previewImage;

    [ObservableProperty]
    private string _statusText = "Ready";

    public ScreenshotsViewModel(IDeviceManager deviceManager, ISettingsService settingsService, ILogService logService)
    {
        _deviceManager = deviceManager;
        _settingsService = settingsService;
        _logService = logService;

        var downloadDir = _settingsService.Get(SettingKeys.DownloadDirectory, string.Empty);
        if (string.IsNullOrEmpty(downloadDir))
            downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "AndroidPCController");

        _screenshotsDirectory = Path.Combine(downloadDir, "Screenshots");
        Directory.CreateDirectory(_screenshotsDirectory);

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;

        LoadExistingScreenshots();
    }

    public void SetSession(IDeviceSession? session)
    {
        _currentSession = session;
    }

    [RelayCommand]
    private async Task CaptureAsync()
    {
        if (_currentSession is null)
        {
            StatusText = "No device connected.";
            return;
        }

        try
        {
            IsLoading = true;
            StatusText = "Capturing screenshot...";

            var filename = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var path = await _currentSession.Screenshot.CaptureAndSaveAsync(_screenshotsDirectory, filename);

            var item = new ScreenshotItem
            {
                FilePath = path,
                FileName = Path.GetFileName(path),
                CapturedAt = DateTime.Now,
                FileSize = new FileInfo(path).Length
            };

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Screenshots.Insert(0, item);
                SelectedScreenshot = item;
                LoadPreview(item);
            });

            StatusText = $"Screenshot saved: {item.FileName}";
            _logService.Information("Screenshots", $"Captured: {path}");
        }
        catch (Exception ex)
        {
            StatusText = $"Capture failed: {ex.Message}";
            _logService.Error("Screenshots", $"Capture failed: {ex.Message}", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedScreenshot))]
    private async Task DeleteAsync()
    {
        if (SelectedScreenshot is null) return;

        try
        {
            if (File.Exists(SelectedScreenshot.FilePath))
                File.Delete(SelectedScreenshot.FilePath);

            var item = SelectedScreenshot;
            Screenshots.Remove(item);

            if (PreviewImage is not null && SelectedScreenshot == item)
                PreviewImage = null;

            StatusText = $"Deleted: {item.FileName}";
            _logService.Information("Screenshots", $"Deleted: {item.FilePath}");
        }
        catch (Exception ex)
        {
            StatusText = $"Delete failed: {ex.Message}";
            _logService.Error("Screenshots", $"Delete failed: {ex.Message}", ex);
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(_screenshotsDirectory);
            System.Diagnostics.Process.Start("explorer.exe", _screenshotsDirectory);
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to open folder: {ex.Message}";
            _logService.Error("Screenshots", $"Open folder failed: {ex.Message}", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedScreenshot))]
    private void CopyToClipboard()
    {
        if (SelectedScreenshot is null) return;

        try
        {
            if (!File.Exists(SelectedScreenshot.FilePath))
            {
                StatusText = "File not found.";
                return;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(SelectedScreenshot.FilePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            System.Windows.Clipboard.SetImage(bitmap);
            StatusText = $"Copied {SelectedScreenshot.FileName} to clipboard.";
            _logService.Information("Screenshots", $"Copied to clipboard: {SelectedScreenshot.FileName}");
        }
        catch (Exception ex)
        {
            StatusText = $"Copy failed: {ex.Message}";
            _logService.Error("Screenshots", $"Copy to clipboard failed: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private void SelectScreenshot(ScreenshotItem? item)
    {
        SelectedScreenshot = item;
        if (item is not null) LoadPreview(item);
    }

    private void LoadPreview(ScreenshotItem item)
    {
        try
        {
            if (!File.Exists(item.FilePath)) return;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(item.FilePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 400;
            bitmap.EndInit();
            bitmap.Freeze();

            PreviewImage = bitmap;
        }
        catch (Exception ex)
        {
            _logService.Error("Screenshots", $"Failed to load preview: {ex.Message}", ex);
        }
    }

    private void LoadExistingScreenshots()
    {
        try
        {
            if (!Directory.Exists(_screenshotsDirectory)) return;

            var files = Directory.GetFiles(_screenshotsDirectory, "*.png")
                .Concat(Directory.GetFiles(_screenshotsDirectory, "*.jpg"))
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToList();

            foreach (var file in files)
            {
                Screenshots.Add(new ScreenshotItem
                {
                    FilePath = file,
                    FileName = Path.GetFileName(file),
                    CapturedAt = File.GetLastWriteTime(file),
                    FileSize = new FileInfo(file).Length
                });
            }
        }
        catch (Exception ex)
        {
            _logService.Error("Screenshots", $"Failed to load existing screenshots: {ex.Message}", ex);
        }
    }

    private bool HasSelectedScreenshot => SelectedScreenshot is not null;

    private void OnDeviceConnected(object? sender, DeviceConnectedEventArgs e)
    {
        _currentSession = e.Session;
    }

    private void OnDeviceDisconnected(object? sender, DeviceDisconnectedEventArgs e)
    {
        if (_currentSession?.Serial == e.Serial)
            _currentSession = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _deviceManager.DeviceConnected -= OnDeviceConnected;
        _deviceManager.DeviceDisconnected -= OnDeviceDisconnected;

        GC.SuppressFinalize(this);
    }
}

public sealed class ScreenshotItem
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public DateTime CapturedAt { get; init; }
    public long FileSize { get; init; }
}
