using AndroidPCController.Core.Models;

namespace AndroidPCController.Core.Interfaces;

public interface IFileTransferService : IAsyncDisposable
{
    IReadOnlyList<TransferProgress> ActiveTransfers { get; }
    Task<IReadOnlyList<AndroidFileInfo>> ListDirectoryAsync(string remotePath, CancellationToken ct = default);
    Task<byte[]> DownloadFileAsync(string remotePath, IProgress<TransferProgress>? progress = null, CancellationToken ct = default);
    Task UploadFileAsync(string localPath, string remotePath, IProgress<TransferProgress>? progress = null, CancellationToken ct = default);
    Task DeleteFileAsync(string remotePath, CancellationToken ct = default);
    Task CreateDirectoryAsync(string remotePath, CancellationToken ct = default);
    Task RenameAsync(string oldPath, string newPath, CancellationToken ct = default);
    Task<AndroidFileInfo> GetFileInfoAsync(string remotePath, CancellationToken ct = default);
    Task CancelTransferAsync(string transferId, CancellationToken ct = default);
    event EventHandler<TransferProgressEventArgs>? TransferProgressChanged;
}

public sealed class TransferProgressEventArgs : EventArgs
{
    public required TransferProgress Progress { get; init; }
}
