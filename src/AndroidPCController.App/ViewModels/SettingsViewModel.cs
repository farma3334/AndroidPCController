using System.IO;
using AndroidPCController.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndroidPCController.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;
    private readonly Services.TrayIconService _trayIconService;

    [ObservableProperty]
    private string _theme = "Dark";

    [ObservableProperty]
    private string _language = "en";

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _autoReconnect = true;

    [ObservableProperty]
    private int _connectionTimeout = 10000;

    [ObservableProperty]
    private int _defaultFps = 60;

    [ObservableProperty]
    private int _defaultBitrate = 8_000_000;

    [ObservableProperty]
    private int _defaultBitrateMbps = 8;

    [ObservableProperty]
    private string _defaultResolution = "Native";

    [ObservableProperty]
    private string _defaultCodec = "H264";

    [ObservableProperty]
    private bool _clipboardSync = true;

    [ObservableProperty]
    private bool _notificationSync;

    [ObservableProperty]
    private bool _usageAnalytics;

    [ObservableProperty]
    private bool _crashReports = true;

    [ObservableProperty]
    private bool _deviceHistory = true;

    [ObservableProperty]
    private string _downloadDirectory = string.Empty;

    [ObservableProperty]
    private string _adbPath = string.Empty;

    [ObservableProperty]
    private bool _debugLogging;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    public IReadOnlyList<string> AvailableLanguages { get; } = ["en", "fr", "es", "de", "ar", "zh", "hi"];

    public IReadOnlyList<string> AvailableResolutions { get; } = ["Native", "1920x1080", "1280x720", "800x480"];

    public IReadOnlyList<string> AvailableCodecs { get; } = ["H264", "H265"];

    public SettingsViewModel(ISettingsService settingsService, ILogService logService, Services.TrayIconService trayIconService)
    {
        _settingsService = settingsService;
        _logService = logService;
        _trayIconService = trayIconService;
        LoadSettings();
    }

    private void LoadSettings()
    {
        Theme = _settingsService.Get(SettingKeys.Theme, "Dark");
        Language = _settingsService.Get(SettingKeys.Language, "en");
        StartMinimized = _settingsService.Get(SettingKeys.StartMinimized, false);
        MinimizeToTray = _settingsService.Get(SettingKeys.MinimizeToTray, true);
        AutoReconnect = _settingsService.Get(SettingKeys.AutoReconnect, true);
        ConnectionTimeout = _settingsService.Get(SettingKeys.ConnectionTimeout, 10000);
        DefaultFps = _settingsService.Get(SettingKeys.DefaultFps, 60);
        DefaultBitrate = _settingsService.Get(SettingKeys.DefaultBitrate, 8_000_000);
        DefaultResolution = _settingsService.Get(SettingKeys.DefaultResolution, "Native");
        DefaultCodec = _settingsService.Get(SettingKeys.DefaultCodec, "H264");
        ClipboardSync = _settingsService.Get(SettingKeys.ClipboardSync, true);
        NotificationSync = _settingsService.Get(SettingKeys.NotificationSync, false);
        DownloadDirectory = _settingsService.Get(SettingKeys.DownloadDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "AndroidPCController"));
        AdbPath = _settingsService.Get(SettingKeys.AdbPath, string.Empty);
        DebugLogging = _settingsService.Get(SettingKeys.DebugLogging, false);
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            _settingsService.Set(SettingKeys.Theme, Theme);
            _settingsService.Set(SettingKeys.Language, Language);
            _settingsService.Set(SettingKeys.StartMinimized, StartMinimized);
            _settingsService.Set(SettingKeys.MinimizeToTray, MinimizeToTray);
            _settingsService.Set(SettingKeys.AutoReconnect, AutoReconnect);
            _settingsService.Set(SettingKeys.ConnectionTimeout, ConnectionTimeout);
            _settingsService.Set(SettingKeys.DefaultFps, DefaultFps);
            _settingsService.Set(SettingKeys.DefaultBitrate, DefaultBitrate);
            _settingsService.Set(SettingKeys.DefaultResolution, DefaultResolution);
            _settingsService.Set(SettingKeys.DefaultCodec, DefaultCodec);
            _settingsService.Set(SettingKeys.ClipboardSync, ClipboardSync);
            _settingsService.Set(SettingKeys.NotificationSync, NotificationSync);
            _settingsService.Set(SettingKeys.DownloadDirectory, DownloadDirectory);
            _settingsService.Set(SettingKeys.AdbPath, AdbPath);
            _settingsService.Set(SettingKeys.DebugLogging, DebugLogging);

            _settingsService.Save();
            HasUnsavedChanges = false;
            StatusMessage = "Settings saved successfully.";
            _logService.Information("Settings", "Settings saved");

            if (MinimizeToTray) _trayIconService.Enable();
            else _trayIconService.Disable();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save: {ex.Message}";
            _logService.Error("Settings", $"Save failed: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        Theme = "Dark";
        Language = "en";
        StartMinimized = false;
        MinimizeToTray = true;
        AutoReconnect = true;
        ConnectionTimeout = 10000;
        DefaultFps = 60;
        DefaultBitrate = 8_000_000;
        DefaultResolution = "Native";
        DefaultCodec = "H264";
        ClipboardSync = true;
        NotificationSync = false;
        UsageAnalytics = false;
        CrashReports = true;
        DeviceHistory = true;
        DownloadDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "AndroidPCController");
        AdbPath = string.Empty;
        DebugLogging = false;

        HasUnsavedChanges = true;
        StatusMessage = "Defaults applied. Click Save to confirm.";
        _logService.Information("Settings", "Defaults applied");
    }

    [RelayCommand]
    private void BrowseAdbPath()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select ADB executable",
            Filter = "ADB executable (adb.exe)|adb.exe|All files (*.*)|*.*",
            FileName = "adb.exe"
        };

        if (dialog.ShowDialog() == true)
        {
            AdbPath = dialog.FileName;
            HasUnsavedChanges = true;
            StatusMessage = $"ADB path set to: {AdbPath}";
        }
    }

    [RelayCommand]
    private void BrowseDownloadDir()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select download directory",
            FolderName = DownloadDirectory
        };

        if (dialog.ShowDialog() == true)
        {
            DownloadDirectory = dialog.FolderName;
            HasUnsavedChanges = true;
            StatusMessage = $"Download directory set to: {DownloadDirectory}";
        }
    }

    partial void OnThemeChanged(string value) => HasUnsavedChanges = true;
    partial void OnLanguageChanged(string value) => HasUnsavedChanges = true;
    partial void OnStartMinimizedChanged(bool value) => HasUnsavedChanges = true;
    partial void OnMinimizeToTrayChanged(bool value) => HasUnsavedChanges = true;
    partial void OnAutoReconnectChanged(bool value) => HasUnsavedChanges = true;
    partial void OnConnectionTimeoutChanged(int value) => HasUnsavedChanges = true;
    partial void OnDefaultFpsChanged(int value) => HasUnsavedChanges = true;
    partial void OnDefaultBitrateMbpsChanged(int value)
    {
        DefaultBitrate = value * 1_000_000;
        HasUnsavedChanges = true;
    }

    partial void OnDefaultBitrateChanged(int value)
    {
        DefaultBitrateMbps = value / 1_000_000;
        HasUnsavedChanges = true;
    }
    partial void OnDefaultResolutionChanged(string value) => HasUnsavedChanges = true;
    partial void OnDefaultCodecChanged(string value) => HasUnsavedChanges = true;
    partial void OnClipboardSyncChanged(bool value) => HasUnsavedChanges = true;
    partial void OnNotificationSyncChanged(bool value) => HasUnsavedChanges = true;
    partial void OnDownloadDirectoryChanged(string value) => HasUnsavedChanges = true;
    partial void OnAdbPathChanged(string value) => HasUnsavedChanges = true;
    partial void OnDebugLoggingChanged(bool value) => HasUnsavedChanges = true;
}
