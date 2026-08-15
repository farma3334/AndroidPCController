namespace AndroidPCController.Core.Models;

public sealed class AndroidAppInfo
{
    public required string PackageName { get; init; }
    public required string AppName { get; init; }
    public required string VersionName { get; init; }
    public required int VersionCode { get; init; }
    public required bool IsSystemApp { get; init; }
    public long Size { get; init; }
    public DateTime? InstallDate { get; init; }
    public bool IsEnabled { get; init; }
    public string? DataDir { get; init; }
    public string? SourceDir { get; init; }
}

public sealed class InstalledAppsResult
{
    public required IReadOnlyList<AndroidAppInfo> UserApps { get; init; }
    public required IReadOnlyList<AndroidAppInfo> SystemApps { get; init; }
}
