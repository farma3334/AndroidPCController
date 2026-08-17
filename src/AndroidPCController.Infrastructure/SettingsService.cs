using System.Text.Json;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;

namespace AndroidPCController.Infrastructure;

public sealed class SettingsService : ISettingsService
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly string _settingsPath;
    private Dictionary<string, JsonElement> _settings = new();

    public event EventHandler<SettingChangedEventArgs>? SettingChanged;

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "AndroidPCController", "config");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
    }

    public T Get<T>(string key, T defaultValue = default!) where T : notnull
    {
        _lock.EnterReadLock();
        try
        {
            if (_settings.TryGetValue(key, out var element))
            {
                try
                {
                    return element.Deserialize<T>() ?? defaultValue;
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Set<T>(string key, T value) where T : notnull
    {
        _lock.EnterWriteLock();
        try
        {
            var element = JsonSerializer.SerializeToElement(value);
            _settings[key] = element;
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        SettingChanged?.Invoke(this, new SettingChangedEventArgs { Key = key, Value = value });
    }

    public void Save()
    {
        _lock.EnterReadLock();
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Load()
    {
        if (!File.Exists(_settingsPath))
        {
            SeedDefaults();
            Save();
            return;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            _lock.EnterWriteLock();
            try
            {
                _settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        catch
        {
            SeedDefaults();
            Save();
        }
    }

    private void SeedDefaults()
    {
        var defaults = new AppSettings();
        Set(SettingKeys.Theme, defaults.Theme);
        Set(SettingKeys.Language, defaults.Language);
        Set(SettingKeys.StartMinimized, defaults.StartMinimized);
        Set(SettingKeys.MinimizeToTray, defaults.MinimizeToTray);
        Set(SettingKeys.AutoReconnect, defaults.AutoReconnect);
        Set(SettingKeys.ConnectionTimeout, defaults.ConnectionTimeoutMs);
        Set(SettingKeys.DefaultFps, defaults.DefaultFps);
        Set(SettingKeys.DefaultBitrate, defaults.DefaultBitrate);
        Set(SettingKeys.DefaultResolution, defaults.DefaultResolution);
        Set(SettingKeys.DefaultCodec, defaults.DefaultCodec);
        Set(SettingKeys.HardwareAcceleration, defaults.HardwareAcceleration);
        Set(SettingKeys.ClipboardSync, defaults.ClipboardSync);
        Set(SettingKeys.NotificationSync, defaults.NotificationSync);
        Set(SettingKeys.UsageAnalytics, defaults.UsageAnalytics);
        Set(SettingKeys.CrashReports, defaults.CrashReports);
        Set(SettingKeys.DeviceHistory, defaults.DeviceHistory);
        Set(SettingKeys.DownloadDirectory, defaults.DownloadDirectory);
        Set(SettingKeys.AdbPath, defaults.AdbPath ?? string.Empty);
        Set(SettingKeys.DebugLogging, defaults.DebugLogging);
        Set(SettingKeys.MouseSensitivity, 1.0);
        Set(SettingKeys.ScrollSensitivity, 1.0);
        Set(SettingKeys.DoubleTapTimeout, 300);
        Set(SettingKeys.LongPressDuration, 500);
        Set(SettingKeys.ShowTouchFeedback, true);
        Set(SettingKeys.EnableGestures, true);
    }
}
