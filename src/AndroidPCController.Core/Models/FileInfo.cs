namespace AndroidPCController.Core.Models;

public sealed class AndroidFileInfo
{
    public required string FullName { get; init; }
    public required string Name { get; init; }
    public required bool IsDirectory { get; init; }
    public long Size { get; init; }
    public DateTime LastModified { get; init; }
    public string? Permissions { get; init; }
    public string? Owner { get; init; }
    public string? MimeType { get; init; }
}

public sealed class TransferProgress
{
    public required string TransferId { get; init; }
    public required string FileName { get; init; }
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
    public required TransferState State { get; set; }
    public long TotalBytes { get; init; }
    public long TransferredBytes { get; set; }
    public double SpeedBytesPerSecond { get; set; }
    public TimeSpan EstimatedTimeRemaining { get; set; }
    public DateTime StartTime { get; init; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}
