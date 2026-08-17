using System.Collections.ObjectModel;
using System.IO;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndroidPCController.App.ViewModels;

public partial class AppsViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IDeviceManager _deviceManager;
    private readonly ILogService _logService;
    private IDeviceSession? _currentSession;
    private bool _disposed;
    private IReadOnlyList<AndroidAppInfo> _allUserApps = [];
    private IReadOnlyList<AndroidAppInfo> _allSystemApps = [];

    [ObservableProperty]
    private ObservableCollection<AndroidAppInfo> _apps = [];

    [ObservableProperty]
    private AndroidAppInfo? _selectedApp;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private bool _showUserApps = true;

    [ObservableProperty]
    private bool _showSystemApps;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _totalApps;

    [ObservableProperty]
    private int _userAppsCount;

    [ObservableProperty]
    private int _systemAppsCount;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private double _installProgress;

    public AppsViewModel(IDeviceManager deviceManager, ILogService logService)
    {
        _deviceManager = deviceManager;
        _logService = logService;

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;

        SetSession(_deviceManager.ActiveSessions.FirstOrDefault());
    }

    public void SetSession(IDeviceSession? session)
    {
        _currentSession = session;
        if (session is not null) _ = RefreshAsync();
    }

    partial void OnFilterTextChanged(string value) => ApplyFilterInternal();
    partial void OnShowUserAppsChanged(bool value) => ApplyFilterInternal();
    partial void OnShowSystemAppsChanged(bool value) => ApplyFilterInternal();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_currentSession is null)
        {
            _logService.Warning("Apps", "No device session. Connect a device first.");
            return;
        }

        try
        {
            IsLoading = true;
            _logService.Information("Apps", "Loading installed apps...");

            var result = await _currentSession.AppManager.GetInstalledAppsAsync(includeSystem: true);
            _allUserApps = result.UserApps;
            _allSystemApps = result.SystemApps;

            UserAppsCount = _allUserApps.Count;
            SystemAppsCount = _allSystemApps.Count;
            TotalApps = UserAppsCount + SystemAppsCount;

            ApplyFilterInternal();
            _logService.Information("Apps", $"Loaded {TotalApps} apps ({UserAppsCount} user, {SystemAppsCount} system)");
        }
        catch (Exception ex)
        {
            _logService.Error("Apps", $"Failed to load apps: {ex.Message}", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedApp))]
    private async Task LaunchAsync()
    {
        if (_currentSession is null || SelectedApp is null) return;
        try
        {
            await _currentSession.AppManager.LaunchAppAsync(SelectedApp.PackageName);
            _logService.Information("Apps", $"Launched {SelectedApp.AppName} ({SelectedApp.PackageName})");
        }
        catch (Exception ex)
        {
            _logService.Error("Apps", $"Failed to launch {SelectedApp.PackageName}: {ex.Message}", ex);
        }
    }

    public async Task LaunchAppAsync(string packageName)
    {
        if (_currentSession is null) return;
        try
        {
            await _currentSession.AppManager.LaunchAppAsync(packageName);
            _logService.Information("Apps", $"Launched {packageName}");
        }
        catch (Exception ex)
        {
            _logService.Error("Apps", $"Failed to launch {packageName}: {ex.Message}", ex);
        }
    }

    public void LaunchApp(string packageName)
    {
        _ = LaunchAppAsync(packageName);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedApp))]
    private async Task ForceStopAsync()
    {
        if (_currentSession is null || SelectedApp is null) return;
        try
        {
            await _currentSession.AppManager.ForceStopAppAsync(SelectedApp.PackageName);
            _logService.Information("Apps", $"Force stopped {SelectedApp.AppName} ({SelectedApp.PackageName})");
        }
        catch (Exception ex)
        {
            _logService.Error("Apps", $"Failed to force stop {SelectedApp.PackageName}: {ex.Message}", ex);
        }
    }

    public async Task ForceStopAppAsync(string packageName)
    {
        if (_currentSession is null) return;
        try
        {
            await _currentSession.AppManager.ForceStopAppAsync(packageName);
            _logService.Information("Apps", $"Force stopped {packageName}");
        }
        catch (Exception ex)
        {
            _logService.Error("Apps", $"Failed to force stop {packageName}: {ex.Message}", ex);
        }
    }

    public void ForceStopApp(string packageName)
    {
        _ = ForceStopAppAsync(packageName);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedApp))]
    private async Task UninstallAsync()
    {
        if (_currentSession is null || SelectedApp is null) return;
        try
        {
            await _currentSession.AppManager.UninstallAppAsync(SelectedApp.PackageName);
            _logService.Information("Apps", $"Uninstalled {SelectedApp.AppName} ({SelectedApp.PackageName})");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logService.Error("Apps", $"Failed to uninstall {SelectedApp.PackageName}: {ex.Message}", ex);
        }
    }

    public async Task UninstallAppAsync(string packageName)
    {
        if (_currentSession is null) return;
        try
        {
            await _currentSession.AppManager.UninstallAppAsync(packageName);
            _logService.Information("Apps", $"Uninstalled {packageName}");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logService.Error("Apps", $"Failed to uninstall {packageName}: {ex.Message}", ex);
        }
    }

    public void UninstallApp(string packageName)
    {
        _ = UninstallAppAsync(packageName);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedApp))]
    private async Task ClearDataAsync()
    {
        if (_currentSession is null || SelectedApp is null) return;
        try
        {
            await _currentSession.AppManager.ClearAppDataAsync(SelectedApp.PackageName);
            _logService.Information("Apps", $"Cleared data for {SelectedApp.AppName} ({SelectedApp.PackageName})");
        }
        catch (Exception ex)
        {
            _logService.Error("Apps", $"Failed to clear data for {SelectedApp.PackageName}: {ex.Message}", ex);
        }
    }

    public async Task ClearAppDataAsync(string packageName)
    {
        if (_currentSession is null) return;
        try
        {
            await _currentSession.AppManager.ClearAppDataAsync(packageName);
            _logService.Information("Apps", $"Cleared data for {packageName}");
        }
        catch (Exception ex)
        {
            _logService.Error("Apps", $"Failed to clear data for {packageName}: {ex.Message}", ex);
        }
    }

    public void ClearAppData(string packageName)
    {
        _ = ClearAppDataAsync(packageName);
    }

    [RelayCommand]
    private async Task InstallApkAsync()
    {
        if (_currentSession is null) return;

        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select APK file",
                Filter = "APK files (*.apk)|*.apk|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                IsInstalling = true;
                _logService.Information("Apps", $"Installing APK: {Path.GetFileName(dialog.FileName)}");

                var progress = new Progress<TransferProgress>(p =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        InstallProgress = p.TotalBytes > 0 ? (double)p.TransferredBytes / p.TotalBytes * 100 : 0;
                    });
                });

                await _currentSession.AppManager.InstallApkAsync(dialog.FileName, progress);
                _logService.Information("Apps", $"APK installed: {Path.GetFileName(dialog.FileName)}");
                await RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            _logService.Error("Apps", $"APK install failed: {ex.Message}", ex);
        }
        finally
        {
            IsInstalling = false;
            InstallProgress = 0;
        }
    }

    private void ApplyFilterInternal()
    {
        var filtered = new List<AndroidAppInfo>();

        if (ShowSystemApps) filtered.AddRange(_allSystemApps);
        if (ShowUserApps) filtered.AddRange(_allUserApps);

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            var search = FilterText.Trim();
            filtered = filtered.Where(a =>
                a.AppName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.PackageName.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        filtered = filtered.OrderBy(a => a.AppName).ToList();

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Apps.Clear();
            foreach (var app in filtered) Apps.Add(app);
        });
    }

    private bool HasSelectedApp => SelectedApp is not null;

    private void OnDeviceConnected(object? sender, DeviceConnectedEventArgs e)
    {
        _currentSession = e.Session;
        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => _ = RefreshAsync());
    }

    private void OnDeviceDisconnected(object? sender, DeviceDisconnectedEventArgs e)
    {
        if (_currentSession?.Serial == e.Serial)
        {
            _currentSession = null;
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Apps.Clear();
                _allUserApps = [];
                _allSystemApps = [];
                TotalApps = 0;
                UserAppsCount = 0;
                SystemAppsCount = 0;
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
