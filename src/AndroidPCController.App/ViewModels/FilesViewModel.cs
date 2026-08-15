using System.Collections.ObjectModel;
using System.IO;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndroidPCController.App.ViewModels;

[ObservableObject]
public partial class FilesViewModel : IAsyncDisposable
{
    private readonly IDeviceManager _deviceManager;
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;
    private IDeviceSession? _currentSession;
    private bool _disposed;

    [ObservableProperty]
    private string _currentPath = "/sdcard/";

    [ObservableProperty]
    private ObservableCollection<AndroidFileInfo> _files = [];

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

    public FilesViewModel(IDeviceManager deviceManager, ISettingsService settingsService, ILogService logService)
    {
        _deviceManager = deviceManager;
        _settingsService = settingsService;
        _logService = logService;

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;
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

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Files.Clear();
                var sorted = items.OrderByDescending(f => f.IsDirectory).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase);
                foreach (var file in sorted)
                {
                    Files.Add(file);
                }
            });

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
            var dir = Path.GetDirectoryName(oldPath)?.Replace('\\', '/') ?? CurrentPath.TrimEnd('/');
            var oldName = Path.GetFileName(oldPath);
            var newPath = dir + "/" + oldName;

            await _currentSession.FileTransfer.RenameAsync(oldPath, newPath);
            StatusText = $"Renamed to {oldName}";
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
