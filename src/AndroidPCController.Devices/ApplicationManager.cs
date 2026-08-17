using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using Microsoft.Extensions.Logging;

namespace AndroidPCController.Devices;

public sealed class ApplicationManager : IApplicationManager
{
    private readonly IAdbClient _adbClient;
    private readonly string _serial;
    private readonly ILogger<ApplicationManager> _logger;
    private bool _disposed;

    public ApplicationManager(IAdbClient adbClient, string serial, ILogger<ApplicationManager> logger)
    {
        _adbClient = adbClient ?? throw new ArgumentNullException(nameof(adbClient));
        _serial = serial ?? throw new ArgumentNullException(nameof(serial));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InstalledAppsResult> GetInstalledAppsAsync(bool includeSystem = false, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Getting installed apps (includeSystem={IncludeSystem})", includeSystem);

        string cmd = includeSystem ? "pm list packages -f" : "pm list packages -3";
        string output = await _adbClient.ExecuteCommandAsync(_serial, cmd, ct).ConfigureAwait(false);

        var userApps = new List<AndroidAppInfo>();
        var systemApps = new List<AndroidAppInfo>();

        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            ct.ThrowIfCancellationRequested();
            string trimmed = line.Trim();
            if (!trimmed.StartsWith("package:")) continue;

            string packageInfo = trimmed["package:".Length..];
            int equalsIdx = packageInfo.IndexOf('=');
            if (equalsIdx < 0) continue;

            string apkPath = packageInfo[..equalsIdx];
            string packageName = packageInfo[(equalsIdx + 1)..];

            try
            {
                var appInfo = await GetAppDetailsAsync(packageName, apkPath, ct).ConfigureAwait(false);
                if (appInfo is not null)
                {
                    if (appInfo.IsSystemApp)
                        systemApps.Add(appInfo);
                    else
                        userApps.Add(appInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get details for package {PackageName}", packageName);
            }
        }

        _logger.LogInformation("Found {UserCount} user apps and {SystemCount} system apps",
            userApps.Count, systemApps.Count);

        return new InstalledAppsResult
        {
            UserApps = userApps.AsReadOnly(),
            SystemApps = systemApps.AsReadOnly()
        };
    }

    public async Task LaunchAppAsync(string packageName, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogInformation("Launching app: {PackageName}", packageName);
        await _adbClient.LaunchAppAsync(_serial, packageName, ct).ConfigureAwait(false);
    }

    public async Task ForceStopAppAsync(string packageName, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogInformation("Force stopping app: {PackageName}", packageName);
        await _adbClient.ForceStopAppAsync(_serial, packageName, ct).ConfigureAwait(false);
    }

    public async Task UninstallAppAsync(string packageName, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogInformation("Uninstalling app: {PackageName}", packageName);
        await _adbClient.UninstallAppAsync(_serial, packageName, ct).ConfigureAwait(false);
    }

    public async Task ClearAppDataAsync(string packageName, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogInformation("Clearing app data: {PackageName}", packageName);
        await _adbClient.ClearAppDataAsync(_serial, packageName, ct).ConfigureAwait(false);
    }

    public async Task InstallApkAsync(string apkPath, IProgress<TransferProgress>? progress = null, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (!File.Exists(apkPath))
            throw new FileNotFoundException($"APK file not found: {apkPath}");

        _logger.LogInformation("Installing APK: {ApkPath}", apkPath);
        await _adbClient.InstallApkAsync(_serial, apkPath, progress, ct).ConfigureAwait(false);
    }

    public async Task EnableAppAsync(string packageName, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogInformation("Enabling app: {PackageName}", packageName);
        await _adbClient.ExecuteCommandAsync(_serial, $"pm enable {packageName}", ct).ConfigureAwait(false);
    }

    public async Task DisableAppAsync(string packageName, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogInformation("Disabling app: {PackageName}", packageName);
        await _adbClient.ExecuteCommandAsync(_serial, $"pm disable-user {packageName}", ct).ConfigureAwait(false);
    }

    private async Task<AndroidAppInfo?> GetAppDetailsAsync(string packageName, string apkPath, CancellationToken ct)
    {
        try
        {
            string versionOutput = await _adbClient.ExecuteCommandAsync(
                _serial,
                $"dumpsys package {packageName} | grep -E 'versionName|versionCode|firstInstallTime|dataDir|sourceDir'",
                ct).ConfigureAwait(false);

            string? versionName = null;
            int versionCode = 0;
            string? dataDir = null;
            string? sourceDir = null;

            foreach (string line in versionOutput.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("versionName="))
                    versionName = trimmed["versionName=".Length..].Trim();
                else if (trimmed.StartsWith("versionCode="))
                    int.TryParse(trimmed["versionCode=".Length..].Trim(), out versionCode);
                else if (trimmed.StartsWith("dataDir="))
                    dataDir = trimmed["dataDir=".Length..].Trim();
                else if (trimmed.StartsWith("codePath="))
                    sourceDir = trimmed["codePath=".Length..].Trim();
            }

            bool isSystemApp = apkPath.StartsWith("/system/") || apkPath.StartsWith("/product/");

            string enabledOutput = await _adbClient.ExecuteCommandAsync(
                _serial,
                $"pm list packages -e | grep {packageName}",
                ct).ConfigureAwait(false);

            bool isEnabled = enabledOutput.Contains(packageName);

            return new AndroidAppInfo
            {
                PackageName = packageName,
                AppName = packageName,
                VersionName = versionName ?? "unknown",
                VersionCode = versionCode,
                IsSystemApp = isSystemApp,
                IsEnabled = isEnabled,
                DataDir = dataDir,
                SourceDir = sourceDir ?? apkPath
            };
        }
        catch
        {
            return new AndroidAppInfo
            {
                PackageName = packageName,
                AppName = packageName,
                VersionName = "unknown",
                VersionCode = 0,
                IsSystemApp = apkPath.StartsWith("/system/") || apkPath.StartsWith("/product/"),
                IsEnabled = true
            };
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _logger.LogInformation("ApplicationManager disposed for device {Serial}", _serial);
        return ValueTask.CompletedTask;
    }
}
