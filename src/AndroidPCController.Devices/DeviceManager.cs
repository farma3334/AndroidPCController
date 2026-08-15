using System.Collections.Concurrent;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using Microsoft.Extensions.Logging;

namespace AndroidPCController.Devices;

public sealed class DeviceManager : IDeviceManager
{
    private readonly IAdbClient _adbClient;
    private readonly ILogger<DeviceManager> _logger;
    private readonly ConcurrentDictionary<string, DeviceSession> _sessions = new();
    private bool _disposed;

    public event EventHandler<DeviceConnectedEventArgs>? DeviceConnected;
    public event EventHandler<DeviceDisconnectedEventArgs>? DeviceDisconnected;

    public DeviceManager(IAdbClient adbClient, ILogger<DeviceManager> logger)
    {
        _adbClient = adbClient ?? throw new ArgumentNullException(nameof(adbClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<IDeviceSession> ActiveSessions =>
        _sessions.Values.Where(s => s.State != ConnectionState.Disconnected)
            .OrderBy(s => s.DeviceInfo.Serial)
            .ToList()
            .AsReadOnly();

    public async Task<IDeviceSession> ConnectAsync(string serial, ConnectionType type = ConnectionType.Usb, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);

        if (_sessions.TryGetValue(serial, out var existing) && existing.State == ConnectionState.Connected)
        {
            _logger.LogDebug("Device {Serial} already connected, returning existing session.", serial);
            return existing;
        }

        _logger.LogInformation("Connecting to device {Serial} via {ConnectionType}.", serial, type);

        var capabilities = await _adbClient.GetCapabilitiesAsync(serial, ct).ConfigureAwait(false);

        var deviceInfo = await _adbClient.GetDeviceInfoAsync(serial, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Device with serial '{serial}' not found.");

        var session = new DeviceSession(_adbClient, _logger, deviceInfo, capabilities, type);
        _sessions.AddOrUpdate(serial, session, (_, _) => session);

        try
        {
            await session.ConnectAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to device {Serial}.", serial);
            _sessions.TryRemove(serial, out _);
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        DeviceConnected?.Invoke(this, new DeviceConnectedEventArgs { Device = deviceInfo, Session = session });
        _logger.LogInformation("Device {Serial} connected successfully.", serial);

        return session;
    }

    public async Task<IDeviceSession> ConnectWirelessAsync(string host, int port, string? pairingCode = null, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        _logger.LogInformation("Connecting wirelessly to {Host}:{Port}.", host, port);

        if (!string.IsNullOrEmpty(pairingCode))
        {
            _logger.LogDebug("Pairing with device at {Host}:{Port}.", host, port);
            await _adbClient.PairWirelessAsync(host, port, pairingCode, ct).ConfigureAwait(false);
        }

        await _adbClient.ConnectWirelessAsync(host, port, ct).ConfigureAwait(false);

        var serial = $"{host}:{port}";
        var deviceInfo = await _adbClient.GetDeviceInfoAsync(serial, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Wireless device at {host}:{port} not found after connection.");

        var capabilities = await _adbClient.GetCapabilitiesAsync(serial, ct).ConfigureAwait(false);

        var session = new DeviceSession(_adbClient, _logger, deviceInfo, capabilities, ConnectionType.Wireless);
        _sessions.AddOrUpdate(serial, session, (_, _) => session);

        try
        {
            await session.ConnectAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect wirelessly to {Serial}.", serial);
            _sessions.TryRemove(serial, out _);
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        DeviceConnected?.Invoke(this, new DeviceConnectedEventArgs { Device = deviceInfo, Session = session });
        _logger.LogInformation("Wireless device {Serial} connected successfully.", serial);

        return session;
    }

    public async Task DisconnectAsync(string serial, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);

        if (!_sessions.TryRemove(serial, out var session))
        {
            _logger.LogWarning("No active session found for device {Serial} to disconnect.", serial);
            return;
        }

        _logger.LogInformation("Disconnecting device {Serial}.", serial);

        try
        {
            await session.DisconnectAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect for device {Serial}.", serial);
        }

        await session.DisposeAsync().ConfigureAwait(false);

        DeviceDisconnected?.Invoke(this, new DeviceDisconnectedEventArgs
        {
            Serial = serial,
            Reason = "Explicit disconnect requested."
        });

        _logger.LogInformation("Device {Serial} disconnected.", serial);
    }

    public async Task<IReadOnlyList<DeviceInfo>> GetAvailableDevicesAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        _logger.LogDebug("Retrieving available devices from ADB.");
        var devices = await _adbClient.GetDevicesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("Found {Count} available device(s).", devices.Count);

        return devices;
    }

    public async Task<DeviceInfo?> GetDeviceInfoAsync(string serial, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);

        return await _adbClient.GetDeviceInfoAsync(serial, ct).ConfigureAwait(false);
    }

    public IDeviceSession? GetSession(string serial)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);

        _sessions.TryGetValue(serial, out var session);
        return session;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _logger.LogInformation("Disposing DeviceManager, disconnecting {Count} session(s).", _sessions.Count);

        var disconnectTasks = _sessions.Keys
            .Select(serial => DisconnectAsync(serial))
            .ToList();

        await Task.WhenAll(disconnectTasks).ConfigureAwait(false);
        _sessions.Clear();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
