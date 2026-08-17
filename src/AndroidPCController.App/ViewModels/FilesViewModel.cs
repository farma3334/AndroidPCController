using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using AndroidPCController.App.Models;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndroidPCController.App.ViewModels;

public partial class FilesViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IDeviceManager _deviceManager;
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;
    private IDeviceSession? _currentSession;
    private bool _disposed;
    private IReadOnlyList<AndroidFileInfo> _currentItems = [];

    [ObservableProperty]
    private string _currentPath = "/sdcard/";

    [ObservableProperty]
    private ObservableCollection<AndroidFileInfo> _files = [];

    [ObservableProperty]
    private ObservableCollection<BreadcrumbSegment> _breadcrumbSegments = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private AndroidFileInfo? _selectedFile;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private double _transferProgress;

    [ObservableProperty]
    private bool _isDragOver;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private long _storageUsedBytes;

    [ObservableProperty]
    private long _storageTotalBytes;

    [ObservableProperty]
    private ObservableCollection<StorageCategory> _storageCategories = [];

    [ObservableProperty]
    private bool _isStorageLoading;

    [ObservableProperty]
    private string _storageInfoText = "Used 0 B / 0 B (0%)";

    [ObservableProperty]
    private double _storagePercentage;

    public FilesViewModel(IDeviceManager deviceManager, ISettingsService settingsService, ILogService logService)
    {
        _deviceManager = deviceManager;
        _settingsService = settingsService;
        _logService = logService;

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;

        SetSession(_deviceManager.ActiveSessions.FirstOrDefault());
    }

    public void SetSession(IDeviceSession? session)
    {
        _currentSession = session;
        if (session is not null)
        {
            _ = NavigateToPathAsync("/sdcard/");
        }
    }

    [RelayCommand]
    private async Task NavigateToPathAsync(string? path = null)
    {
        if (_currentSession is null || string.IsNullOrEmpty(path)) return;

        try
        {
            IsLoading = true;
            StatusText = $"Loading {path}...";

            var items = await _currentSession.FileTransfer.ListDirectoryAsync(path);
            CurrentPath = path;
            _currentItems = items.OrderByDescending(f => f.IsDirectory).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();
            UpdateBreadcrumbs();
            ApplyFilterInternal();

            StatusText = $"{Files.Count} items in {path}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            _logService.Error("Files", $"Failed to navigate to {path}: {ex.Message}", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task NavigateUpAsync()
    {
        if (CurrentPath == "/") return;
        var parent = Path.GetDirectoryName(CurrentPath.TrimEnd('/'))?.Replace('\\', '/') ?? "/";
        if (!parent.EndsWith('/')) parent += "/";
        await NavigateToPathAsync(parent);
    }

    [RelayCommand]
    private async Task NavigateToBreadcrumbAsync(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        await NavigateToPathAsync(path);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilterInternal();

    private void ApplyFilterInternal()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Files.Clear();
            IEnumerable<AndroidFileInfo> filtered = _currentItems;
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.Trim();
                filtered = filtered.Where(f =>
                    f.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
            }
            foreach (var file in filtered)
            {
                Files.Add(file);
            }
        });
    }

    private void UpdateBreadcrumbs()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            BreadcrumbSegments.Clear();
            var segments = new List<BreadcrumbSegment>();

            if (CurrentPath != "/")
            {
                var path = CurrentPath.TrimEnd('/');
                var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var accumulated = "/";
                segments.Add(new BreadcrumbSegment { Name = "Root", Path = "/" });
                for (var i = 0; i < parts.Length; i++)
                {
                    accumulated += parts[i];
                    if (i < parts.Length - 1) accumulated += "/";
                    segments.Add(new BreadcrumbSegment { Name = parts[i], Path = accumulated });
                }
            }
            else
            {
                segments.Add(new BreadcrumbSegment { Name = "Root", Path = "/" });
            }

            foreach (var segment in segments)
            {
                BreadcrumbSegments.Add(segment);
            }
        });
    }

    [RelayCommand]
    private async Task NavigateToSelectedAsync()
    {
        if (SelectedFile is null) return;
        if (SelectedFile.IsDirectory)
        {
            var path = SelectedFile.FullName;
            if (!path.EndsWith('/')) path += "/";
            await NavigateToPathAsync(path);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedFile))]
    private async Task UploadAsync()
    {
        if (_currentSession is null) return;

        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select file to upload",
                Filter = "All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                IsLoading = true;
                StatusText = $"Uploading {Path.GetFileName(dialog.FileName)}...";

                var remotePath = CurrentPath.TrimEnd('/') + "/" + Path.GetFileName(dialog.FileName);
                var progress = new Progress<TransferProgress>(p =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        TransferProgress = p.TotalBytes > 0 ? (double)p.TransferredBytes / p.TotalBytes * 100 : 0;
                        StatusText = $"Uploading {p.FileName}: {TransferProgress:F1}%";
                    });
                });

                await _currentSession.FileTransfer.UploadFileAsync(dialog.FileName, remotePath, progress);
                StatusText = $"Uploaded {Path.GetFileName(dialog.FileName)}";
                _logService.Information("Files", $"Uploaded {dialog.FileName} to {remotePath}");
                await NavigateToPathAsync(CurrentPath);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Upload failed: {ex.Message}";
            _logService.Error("Files", $"Upload failed: {ex.Message}", ex);
        }
        finally
        {
            IsLoading = false;
            TransferProgress = 0;
        }
    }

    [RelayCommand]
    public async Task UploadFilesAsync(string[] files)
    {
        if (_currentSession is null || files.Length == 0) return;

        foreach (var localPath in files)
        {
            if (!File.Exists(localPath)) continue;

            try
            {
                IsLoading = true;
                var remotePath = CurrentPath.TrimEnd('/') + "/" + Path.GetFileName(localPath);
                StatusText = $"Uploading {Path.GetFileName(localPath)}...";

                await _currentSession.FileTransfer.UploadFileAsync(localPath, remotePath);
                _logService.Information("Files", $"Uploaded {localPath} to {remotePath}");
            }
            catch (Exception ex)
            {
                _logService.Error("Files", $"Upload of {localPath} failed: {ex.Message}", ex);
            }
        }

        StatusText = "Upload complete";
        IsLoading = false;
        await NavigateToPathAsync(CurrentPath);
    }

    public void UploadFiles(string[] files)
    {
        _ = UploadFilesAsync(files);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedFile))]
    private async Task DownloadAsync()
    {
        if (_currentSession is null || SelectedFile is null || SelectedFile.IsDirectory) return;

        try
        {
            var downloadDir = _settingsService.Get(SettingKeys.DownloadDirectory, string.Empty);
            if (string.IsNullOrEmpty(downloadDir))
                downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "AndroidPCController");

            Directory.CreateDirectory(downloadDir);
            var localPath = Path.Combine(downloadDir, SelectedFile.Name);

            IsLoading = true;
            StatusText = $"Downloading {SelectedFile.Name}...";

            var data = await _currentSession.FileTransfer.DownloadFileAsync(SelectedFile.FullName);
            await File.WriteAllBytesAsync(localPath, data);

            StatusText = $"Downloaded to {localPath}";
            _logService.Information("Files", $"Downloaded {SelectedFile.FullName} to {localPath}");
        }
        catch (Exception ex)
        {
            StatusText = $"Download failed: {ex.Message}";
            _logService.Error("Files", $"Download failed: {ex.Message}", ex);
        }
        finally
        {
            IsLoading = false;
            TransferProgress = 0;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedFile))]
    private async Task DeleteAsync()
    {
        if (_currentSession is null || SelectedFile is null) return;

        try
        {
            IsLoading = true;
            StatusText = $"Deleting {SelectedFile.Name}...";
            await _currentSession.FileTransfer.DeleteFileAsync(SelectedFile.FullName);
            StatusText = $"Deleted {SelectedFile.Name}";
            _logService.Information("Files", $"Deleted {SelectedFile.FullName}");
            await NavigateToPathAsync(CurrentPath);
        }
        catch (Exception ex)
        {
            StatusText = $"Delete failed: {ex.Message}";
            _logService.Error("Files", $"Delete failed: {ex.Message}", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreateFolderAsync()
    {
        if (_currentSession is null) return;

        try
        {
            var folderName = "NewFolder";
            var remotePath = CurrentPath.TrimEnd('/') + "/" + folderName;
            await _currentSession.FileTransfer.CreateDirectoryAsync(remotePath);
            StatusText = $"Created folder: {folderName}";
            _logService.Information("Files", $"Created folder {remotePath}");
            await NavigateToPathAsync(CurrentPath);
        }
        catch (Exception ex)
        {
            StatusText = $"Create folder failed: {ex.Message}";
            _logService.Error("Files", $"Create folder failed: {ex.Message}", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedFile))]
    private async Task RenameAsync()
    {
        if (_currentSession is null || SelectedFile is null) return;

        try
        {
            var oldPath = SelectedFile.FullName;
            var oldName = Path.GetFileName(oldPath);
            var dir = Path.GetDirectoryName(oldPath)?.Replace('\\', '/') ?? CurrentPath.TrimEnd('/');

            var dialog = new Controls.InputDialog("Rename", $"Rename '{oldName}' to:", oldName)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            if (dialog.ShowDialog() != true) return;

            var newName = dialog.InputValue.Trim();
            if (string.IsNullOrEmpty(newName) || newName == oldName) return;

            var newPath = dir + "/" + newName;
            await _currentSession.FileTransfer.RenameAsync(oldPath, newPath);
            StatusText = $"Renamed to {newName}";
            _logService.Information("Files", $"Renamed {oldPath} to {newPath}");
            await NavigateToPathAsync(CurrentPath);
        }
        catch (Exception ex)
        {
            StatusText = $"Rename failed: {ex.Message}";
            _logService.Error("Files", $"Rename failed: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await NavigateToPathAsync(CurrentPath);
    }

    [RelayCommand]
    private async Task RefreshStorageAsync()
    {
        if (_currentSession is null) return;

        try
        {
            IsStorageLoading = true;

            var dfOutput = await _currentSession.ExecuteShellCommandAsync("df /data");
            var lines = dfOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length >= 2)
            {
                var parts = lines[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    long.TryParse(parts[1], out var total);
                    long.TryParse(parts[2], out var used);
                    StorageTotalBytes = total * 1024;
                    StorageUsedBytes = used * 1024;
                }
            }

            var categories = new List<StorageCategory>();

            var videoSize = await GetCategorySizeAsync("find /sdcard/DCIM /sdcard/Movies -type f \\( -name '*.mp4' -o -name '*.avi' -o -name '*.mkv' \\) 2>/dev/null | xargs du -sb 2>/dev/null | tail -1");
            categories.Add(new StorageCategory { Name = "Videos", SizeBytes = videoSize, Icon = "Video", Color = "#FF5722" });

            var photoSize = await GetCategorySizeAsync("find /sdcard/DCIM/Camera /sdcard/Pictures -type f \\( -name '*.jpg' -o -name '*.jpeg' -o -name '*.png' -o -name '*.gif' \\) 2>/dev/null | xargs du -sb 2>/dev/null | tail -1");
            categories.Add(new StorageCategory { Name = "Photos", SizeBytes = photoSize, Icon = "Image", Color = "#9C27B0" });

            var apkSize = await GetCategorySizeAsync("find /sdcard -name '*.apk' 2>/dev/null | xargs du -sb 2>/dev/null | tail -1");
            categories.Add(new StorageCategory { Name = "Apps", SizeBytes = apkSize, Icon = "Android", Color = "#2196F3" });

            var downloadSize = await GetCategorySizeAsync("du -sb /sdcard/Downloads 2>/dev/null | tail -1");
            categories.Add(new StorageCategory { Name = "Downloads", SizeBytes = downloadSize, Icon = "Download", Color = "#FF9800" });

            var musicSize = await GetCategorySizeAsync("find /sdcard/Music -type f \\( -name '*.mp3' -o -name '*.wav' -o -name '*.flac' \\) 2>/dev/null | xargs du -sb 2>/dev/null | tail -1");
            categories.Add(new StorageCategory { Name = "Music", SizeBytes = musicSize, Icon = "Music", Color = "#E91E63" });

            var docSize = await GetCategorySizeAsync("find /sdcard/Documents -type f 2>/dev/null | xargs du -sb 2>/dev/null | tail -1");
            categories.Add(new StorageCategory { Name = "Documents", SizeBytes = docSize, Icon = "FileDocument", Color = "#607D8B" });

            long categorizedTotal = categories.Sum(c => c.SizeBytes);
            long otherSize = StorageUsedBytes > categorizedTotal ? StorageUsedBytes - categorizedTotal : 0;
            categories.Add(new StorageCategory { Name = "Other", SizeBytes = otherSize, Icon = "DotsHorizontal", Color = "#757575" });

            var totalForPercent = StorageUsedBytes > 0 ? StorageUsedBytes : 1;
            var result = categories.Select(c => new StorageCategory
            {
                Name = c.Name,
                SizeBytes = c.SizeBytes,
                Icon = c.Icon,
                Color = c.Color,
                Percentage = Math.Round((double)c.SizeBytes / totalForPercent * 100, 1)
            }).ToList();

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StorageCategories.Clear();
                foreach (var cat in result)
                {
                    StorageCategories.Add(cat);
                }

                var usedGB = StorageUsedBytes / (1024.0 * 1024 * 1024);
                var totalGB = StorageTotalBytes / (1024.0 * 1024 * 1024);
                var pct = StorageTotalBytes > 0 ? (double)StorageUsedBytes / StorageTotalBytes * 100 : 0;
                StoragePercentage = pct;
                StorageInfoText = $"Used {usedGB:F2} GB / {totalGB:F2} GB ({pct:F1}%)";
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Storage analysis failed: {ex.Message}";
            _logService.Error("Files", $"Storage analysis failed: {ex.Message}", ex);
        }
        finally
        {
            IsStorageLoading = false;
        }
    }

    private async Task<long> GetCategorySizeAsync(string command)
    {
        try
        {
            var result = await _currentSession!.ExecuteShellCommandAsync(command);
            var match = Regex.Match(result, @"(\d+)");
            if (match.Success && long.TryParse(match.Groups[1].Value, out var size))
            {
                return size;
            }
        }
        catch
        {
        }
        return 0;
    }

    public void OpenItem(object item)
    {
        if (item is AndroidFileInfo fileInfo)
        {
            SelectedFile = fileInfo;
            _ = NavigateToSelectedAsync();
        }
    }

    private bool HasSelectedFile => SelectedFile is not null;

    private void OnDeviceConnected(object? sender, DeviceConnectedEventArgs e)
    {
        _currentSession = e.Session;
        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => _ = NavigateToPathAsync("/sdcard/"));
    }

    private void OnDeviceDisconnected(object? sender, DeviceDisconnectedEventArgs e)
    {
        if (_currentSession?.Serial == e.Serial)
        {
            _currentSession = null;
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Files.Clear();
                CurrentPath = "/sdcard/";
                StatusText = "Device disconnected";
            });
        }
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
