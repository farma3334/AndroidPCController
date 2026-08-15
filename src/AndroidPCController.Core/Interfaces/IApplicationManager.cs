using AndroidPCController.Core.Models;

namespace AndroidPCController.Core.Interfaces;

public interface IApplicationManager : IAsyncDisposable
{
    Task<InstalledAppsResult> GetInstalledAppsAsync(bool includeSystem = false, CancellationToken ct = default);
    Task LaunchAppAsync(string packageName, CancellationToken ct = default);
    Task ForceStopAppAsync(string packageName, CancellationToken ct = default);
    Task UninstallAppAsync(string packageName, CancellationToken ct = default);
    Task ClearAppDataAsync(string packageName, CancellationToken ct = default);
    Task InstallApkAsync(string apkPath, IProgress<TransferProgress>? progress = null, CancellationToken ct = default);
    Task EnableAppAsync(string packageName, CancellationToken ct = default);
    Task DisableAppAsync(string packageName, CancellationToken ct = default);
}
