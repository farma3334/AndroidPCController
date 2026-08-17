using System.Collections.ObjectModel;
using System.Windows.Threading;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndroidPCController.App.ViewModels;

public partial class NotificationsViewModel : ObservableObject, IDisposable
{
    private readonly IDeviceManager _deviceManager;
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;
    private readonly IAdbClient _adbClient;
    private IDeviceSession? _currentSession;
    private readonly DispatcherTimer _pollTimer;
    private readonly Dictionary<string, string> _appNames = new(StringComparer.OrdinalIgnoreCase);
    private bool _polling;
    private bool _disposed;

    [ObservableProperty]
    private ObservableCollection<NotificationInfo> _notifications = [];

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSyncEnabled;

    public NotificationsViewModel(IDeviceManager deviceManager, ISettingsService settingsService, ILogService logService, IAdbClient adbClient)
    {
        _deviceManager = deviceManager;
        _settingsService = settingsService;
        _logService = logService;
        _adbClient = adbClient;

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _pollTimer.Tick += async (_, _) => await PollAsync();

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;

        _currentSession = _deviceManager.ActiveSessions.FirstOrDefault();
        IsSyncEnabled = _settingsService.Get(SettingKeys.NotificationSync, false);
    }

    public void Start()
    {
        if (_disposed) return;
        IsSyncEnabled = _settingsService.Get(SettingKeys.NotificationSync, false);
        _polling = true;
        _pollTimer.Start();
        _ = PollAsync();
    }

    public void Stop()
    {
        _polling = false;
        _pollTimer.Stop();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await PollAsync();
    }

    private async Task PollAsync()
    {
        if (!_polling)
            return;

        try
        {
            IsSyncEnabled = _settingsService.Get(SettingKeys.NotificationSync, false);
            if (!IsSyncEnabled)
            {
                Notifications.Clear();
                StatusText = "Notification sync is disabled. Enable it in Settings > Privacy.";
                return;
            }

            if (_currentSession is null)
            {
                Notifications.Clear();
                StatusText = "No device connected.";
                return;
            }

            IsLoading = true;
            var items = await _adbClient.GetNotificationsAsync(_currentSession.Serial);

            if (_appNames.Count == 0)
            {
                try
                {
                    var apps = await _currentSession.AppManager.GetInstalledAppsAsync(includeSystem: true);
                    foreach (var app in apps.UserApps.Concat(apps.SystemApps))
                        _appNames[app.PackageName] = app.AppName;
                }
                catch (Exception ex)
                {
                    _logService.Warning("Notifications", $"Failed to load app names: {ex.Message}");
                }
            }

            Notifications.Clear();
            foreach (var item in items
                         .OrderByDescending(n => n.WhenMs)
                         .Where(n => !string.IsNullOrEmpty(n.Title) || !string.IsNullOrEmpty(n.Text)))
            {
                var resolved = item with
                {
                    AppName = _appNames.TryGetValue(item.PackageName, out var name) ? name : PrettyPackageName(item.PackageName)
                };
                Notifications.Add(resolved);
            }

            StatusText = $"Updated {DateTime.Now:HH:mm:ss} - {Notifications.Count} active notification(s)";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed: {ex.Message}";
            _logService.Error("Notifications", $"Poll failed: {ex.Message}", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void DismissAll()
    {
        _appNames.Clear();
        Notifications.Clear();
        StatusText = "List cleared.";
    }

    private static string PrettyPackageName(string packageName)
    {
        var segments = packageName.Split('.');
        var meaningful = segments.Where(s =>
            !s.Equals("com", StringComparison.OrdinalIgnoreCase) &&
            !s.Equals("org", StringComparison.OrdinalIgnoreCase) &&
            !s.Equals("net", StringComparison.OrdinalIgnoreCase) &&
            !s.Equals("android", StringComparison.OrdinalIgnoreCase)).ToList();

        if (meaningful.Count == 0)
            return packageName;

        var last = meaningful[^1];
        return char.ToUpperInvariant(last[0]) + last[1..];
    }

    private void OnDeviceConnected(object? sender, DeviceConnectedEventArgs e)
    {
        _currentSession = e.Session;
    }

    private void OnDeviceDisconnected(object? sender, DeviceDisconnectedEventArgs e)
    {
        if (_currentSession?.Serial == e.Serial)
            _currentSession = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _pollTimer.Stop();
        _deviceManager.DeviceConnected -= OnDeviceConnected;
        _deviceManager.DeviceDisconnected -= OnDeviceDisconnected;

        GC.SuppressFinalize(this);
    }
}