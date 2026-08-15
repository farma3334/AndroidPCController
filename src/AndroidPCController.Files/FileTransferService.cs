using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;

namespace AndroidPCController.Files;

public sealed class FileTransferService : IFileTransferService
{
    private readonly IAdbClient _adbClient;
    private readonly string _serial;
    private readonly ILogger<FileTransferService> _logger;
    private readonly ConcurrentDictionary<string, TransferProgress> _activeTransfers = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancelTokens = new();
    private bool _disposed;

    public FileTransferService(IAdbClient adbClient, string serial, ILogger<FileTransferService> logger)
    {
        _adbClient = adbClient ?? throw new ArgumentNullException(nameof(adbClient));
        _serial = serial ?? throw new ArgumentNullException(nameof(serial));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<TransferProgress> ActiveTransfers =>
        _activeTransfers.Values.ToList().AsReadOnly();

    public event EventHandler<TransferProgressEventArgs>? TransferProgressChanged;

    public async Task<IReadOnlyList<AndroidFileInfo>> ListDirectoryAsync(string remotePath, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Listing directory: {Path}", remotePath);

        string output = await _adbClient.ExecuteCommandAsync(
            _serial,
            $"ls -la {remotePath}",
            ct).ConfigureAwait(false);

        var entries = new List<AndroidFileInfo>();
        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("total"))
                continue;

            var fileInfo = ParseLsLine(line, remotePath);
            if (fileInfo is not null)
            {
                entries.Add(fileInfo);
            }
        }

        _logger.LogInformation("Found {Count} entries in {Path}", entries.Count, remotePath);
        return entries.AsReadOnly();
    }

    public async Task<byte[]> DownloadFileAsync(string remotePath, IProgress<TransferProgress>? progress = null, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        string transferId = Guid.NewGuid().ToString("N");
        string fileName = Path.GetFileName(remotePath);

        var transfer = new TransferProgress
        {
            TransferId = transferId,
            FileName = fileName,
            SourcePath = remotePath,
            DestinationPath = string.Empty,
            TotalBytes = 0,
            State = TransferState.InProgress
        };

        _activeTransfers[transferId] = transfer;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _cancelTokens[transferId] = cts;

        try
        {
            _logger.LogInformation("Downloading {Path} (TransferId={TransferId})", remotePath, transferId);

            byte[] data = await _adbClient.PullFileAsync(_serial, remotePath, cts.Token).ConfigureAwait(false);

            transfer.TransferredBytes = data.Length;
            transfer.State = TransferState.Completed;

            OnTransferProgressChanged(transfer);
            _logger.LogInformation("Download complete: {FileName} ({Size} bytes)", fileName, data.Length);

            return data;
        }
        catch (OperationCanceledException)
        {
            transfer.State = TransferState.Cancelled;
            OnTransferProgressChanged(transfer);
            throw;
        }
        catch (Exception ex)
        {
            transfer.State = TransferState.Failed;
            transfer.ErrorMessage = ex.Message;
            OnTransferProgressChanged(transfer);
            _logger.LogError(ex, "Download failed for {Path}", remotePath);
            throw;
        }
        finally
        {
            _activeTransfers.TryRemove(transferId, out _);
            _cancelTokens.TryRemove(transferId, out _);
            cts.Dispose();
        }
    }

    public async Task UploadFileAsync(string localPath, string remotePath, IProgress<TransferProgress>? progress = null, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (!File.Exists(localPath))
            throw new FileNotFoundException($"Local file not found: {localPath}");

        string transferId = Guid.NewGuid().ToString("N");
        string fileName = Path.GetFileName(localPath);
        long totalBytes = new FileInfo(localPath).Length;

        var transfer = new TransferProgress
        {
            TransferId = transferId,
            FileName = fileName,
            SourcePath = localPath,
            DestinationPath = remotePath,
            TotalBytes = totalBytes,
            State = TransferState.InProgress
        };

        _activeTransfers[transferId] = transfer;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _cancelTokens[transferId] = cts;

        try
        {
            _logger.LogInformation("Uploading {LocalPath} to {RemotePath} (TransferId={TransferId})", localPath, remotePath, transferId);

            await _adbClient.PushFileAsync(
                _serial,
                localPath,
                remotePath,
                progress,
                cts.Token).ConfigureAwait(false);

            transfer.TransferredBytes = totalBytes;
            transfer.State = TransferState.Completed;

            OnTransferProgressChanged(transfer);
            _logger.LogInformation("Upload complete: {FileName} ({Size} bytes)", fileName, totalBytes);
        }
        catch (OperationCanceledException)
        {
            transfer.State = TransferState.Cancelled;
            OnTransferProgressChanged(transfer);
            throw;
        }
        catch (Exception ex)
        {
            transfer.State = TransferState.Failed;
            transfer.ErrorMessage = ex.Message;
            OnTransferProgressChanged(transfer);
            _logger.LogError(ex, "Upload failed for {LocalPath}", localPath);
            throw;
        }
        finally
        {
            _activeTransfers.TryRemove(transferId, out _);
            _cancelTokens.TryRemove(transferId, out _);
            cts.Dispose();
        }
    }

    public async Task DeleteFileAsync(string remotePath, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Deleting: {Path}", remotePath);
        await _adbClient.ExecuteCommandAsync(_serial, $"rm -rf {remotePath}", ct).ConfigureAwait(false);
        _logger.LogInformation("Deleted: {Path}", remotePath);
    }

    public async Task CreateDirectoryAsync(string remotePath, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Creating directory: {Path}", remotePath);
        await _adbClient.ExecuteCommandAsync(_serial, $"mkdir -p {remotePath}", ct).ConfigureAwait(false);
        _logger.LogInformation("Created directory: {Path}", remotePath);
    }

    public async Task RenameAsync(string oldPath, string newPath, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Renaming {OldPath} to {NewPath}", oldPath, newPath);
        await _adbClient.ExecuteCommandAsync(_serial, $"mv {oldPath} {newPath}", ct).ConfigureAwait(false);
        _logger.LogInformation("Renamed {OldPath} to {NewPath}", oldPath, newPath);
    }

    public async Task<AndroidFileInfo> GetFileInfoAsync(string remotePath, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("Getting file info: {Path}", remotePath);

        string output = await _adbClient.ExecuteCommandAsync(
            _serial,
            $"stat -c '%n|%s|%Y|%a|%U|%F' {remotePath} 2>/dev/null || ls -la {remotePath}",
            ct).ConfigureAwait(false);

        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            throw new FileNotFoundException($"File not found: {remotePath}");

        string line = lines[0].Trim();
        string name = Path.GetFileName(remotePath);
        bool isDir = line.Contains("d");
        long size = 0;
        string? permissions = null;
        string? owner = null;
        DateTime lastModified = DateTime.MinValue;

        if (line.Contains('|'))
        {
            string[] parts = line.Split('|');
            if (parts.Length >= 2) name = parts[0];
            if (parts.Length >= 2 && long.TryParse(parts[1], out long s)) size = s;
            if (parts.Length >= 3 && long.TryParse(parts[2], out long ts)) lastModified = DateTimeOffset.FromUnixTimeSeconds(ts).DateTime;
            if (parts.Length >= 4) permissions = parts[3];
            if (parts.Length >= 5) owner = parts[4];
            if (parts.Length >= 6) isDir = parts[5] == "directory";
        }
        else
        {
            var parsed = ParseLsLine(line, remotePath);
            if (parsed is not null) return parsed;
        }

        return new AndroidFileInfo
        {
            FullName = remotePath,
            Name = name,
            IsDirectory = isDir,
            Size = size,
            LastModified = lastModified,
            Permissions = permissions,
            Owner = owner
        };
    }

    public async Task CancelTransferAsync(string transferId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (_cancelTokens.TryGetValue(transferId, out var cts))
        {
            _logger.LogInformation("Cancelling transfer {TransferId}", transferId);
            await cts.CancelAsync().ConfigureAwait(false);
        }

        if (_activeTransfers.TryGetValue(transferId, out var transfer))
        {
            transfer.State = TransferState.Cancelled;
            OnTransferProgressChanged(transfer);
        }
    }

    private AndroidFileInfo? ParseLsLine(string line, string basePath)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        string[] parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 7) return null;

        bool isDir = parts[0].StartsWith('d');
        string? permissions = parts[0];
        string? owner = parts.Length > 2 ? parts[2] : null;

        long size = 0;
        if (long.TryParse(parts[4], out long s))
            size = s;

        DateTime lastModified = DateTime.MinValue;
        string month = parts[5];
        string day = parts[6];
        string timeOrYear = parts.Length > 7 ? parts[7] : "0";

        try
        {
            string dateStr = $"{month} {day} {timeOrYear}";
            if (int.TryParse(timeOrYear, out _))
            {
                lastModified = DateTime.Parse($"{dateStr}/{DateTime.Now.Year}");
            }
            else
            {
                lastModified = DateTime.Parse(dateStr);
            }
        }
        catch
        {
            lastModified = DateTime.MinValue;
        }

        string name = parts.Length > 8 ? string.Join(' ', parts[8..]) : parts.Length > 7 ? parts[7] : parts[^1];
        if (name == "." || name == "..") return null;

        string fullName = basePath.TrimEnd('/') + "/" + name;

        return new AndroidFileInfo
        {
            FullName = fullName,
            Name = name,
            IsDirectory = isDir,
            Size = size,
            LastModified = lastModified,
            Permissions = permissions,
            Owner = owner
        };
    }

    private void OnTransferProgressChanged(TransferProgress transfer)
    {
        TransferProgressChanged?.Invoke(this, new TransferProgressEventArgs { Progress = transfer });
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var cts in _cancelTokens.Values)
        {
            await cts.CancelAsync().ConfigureAwait(false);
            cts.Dispose();
        }

        _cancelTokens.Clear();
        _activeTransfers.Clear();
        _logger.LogInformation("FileTransferService disposed for device {Serial}", _serial);
    }
}
