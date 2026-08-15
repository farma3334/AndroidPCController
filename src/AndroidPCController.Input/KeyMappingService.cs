using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AndroidPCController.Input;

public sealed class KeyMappingService : IAsyncDisposable
{
    private readonly IAdbClient _adbClient;
    private readonly string _serial;
    private readonly ILogger<KeyMappingService> _logger;
    private readonly string _profilesDirectory;
    private readonly Dictionary<string, GameProfile> _activeProfiles = new();
    private readonly HashSet<int> _activeToggles = new();
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public KeyMappingService(IAdbClient adbClient, string serial, ILogger<KeyMappingService> logger)
    {
        _adbClient = adbClient ?? throw new ArgumentNullException(nameof(adbClient));
        _serial = serial ?? throw new ArgumentNullException(nameof(serial));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _profilesDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AndroidPCController",
            "Profiles");
        Directory.CreateDirectory(_profilesDirectory);
    }

    public IReadOnlyList<GameProfile> LoadedProfiles => _activeProfiles.Values.ToList().AsReadOnly();

    public event EventHandler<GameProfile>? ProfileApplied;
    public event EventHandler<GameProfile>? ProfileRemoved;

    public async Task<GameProfile> CreateProfileAsync(
        string name,
        string? packageName,
        IReadOnlyList<KeyMapping> keyMappings,
        IReadOnlyList<MouseMapping> mouseMappings,
        float sensitivity = 1.0f,
        int deadZone = 10,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var profile = new GameProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            PackageName = packageName,
            KeyMappings = keyMappings,
            MouseMappings = mouseMappings,
            Sensitivity = sensitivity,
            DeadZone = deadZone,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        await SaveProfileAsync(profile, ct).ConfigureAwait(false);
        _logger.LogInformation("Created profile '{Name}' (Id={Id})", name, profile.Id);
        return profile;
    }

    public async Task SaveProfileAsync(GameProfile profile, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        string filePath = GetProfilePath(profile.Id);
        string json = JsonSerializer.Serialize(profile, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, ct).ConfigureAwait(false);
        _logger.LogDebug("Saved profile '{Name}' to {Path}", profile.Name, filePath);
    }

    public async Task<IReadOnlyList<GameProfile>> LoadAllProfilesAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var profiles = new List<GameProfile>();

        if (!Directory.Exists(_profilesDirectory))
            return profiles;

        foreach (string file in Directory.EnumerateFiles(_profilesDirectory, "*.json"))
        {
            try
            {
                string json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                var profile = JsonSerializer.Deserialize<GameProfile>(json, JsonOptions);
                if (profile is not null)
                {
                    profiles.Add(profile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load profile from {Path}", file);
            }
        }

        _logger.LogInformation("Loaded {Count} profiles", profiles.Count);
        return profiles.AsReadOnly();
    }

    public async Task<GameProfile?> LoadProfileAsync(string profileId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        string filePath = GetProfilePath(profileId);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Profile {Id} not found at {Path}", profileId, filePath);
            return null;
        }

        string json = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        var profile = JsonSerializer.Deserialize<GameProfile>(json, JsonOptions);
        if (profile is not null)
        {
            _activeProfiles[profileId] = profile;
            _logger.LogInformation("Loaded profile '{Name}' (Id={Id})", profile.Name, profileId);
        }
        return profile;
    }

    public async Task ApplyProfileAsync(GameProfile profile, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _activeProfiles[profile.Id] = profile;

        _logger.LogInformation("Applying profile '{Name}' with {KeyCount} key mappings and {MouseCount} mouse mappings",
            profile.Name, profile.KeyMappings.Count, profile.MouseMappings.Count);

        foreach (var mapping in profile.KeyMappings)
        {
            ct.ThrowIfCancellationRequested();
            if (mapping.IsToggle && _activeToggles.Contains(mapping.KeyCode))
            {
                continue;
            }

            await _adbClient.ExecuteCommandAsync(
                _serial,
                $"input tap {mapping.TouchX} {mapping.TouchY}",
                ct).ConfigureAwait(false);

            if (mapping.DurationMs > 0)
            {
                await Task.Delay(mapping.DurationMs, ct).ConfigureAwait(false);
            }
        }

        ProfileApplied?.Invoke(this, profile);
    }

    public async Task SimulateKeyAsync(int keyCode, GameProfile profile, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var mapping = profile.KeyMappings.FirstOrDefault(k => k.KeyCode == keyCode);
        if (mapping is null)
        {
            _logger.LogDebug("No mapping found for keyCode {KeyCode} in profile '{ProfileName}'", keyCode, profile.Name);
            await _adbClient.SendKeyEventAsync(_serial, keyCode, ct).ConfigureAwait(false);
            return;
        }

        if (mapping.IsToggle)
        {
            if (_activeToggles.Contains(keyCode))
            {
                _activeToggles.Remove(keyCode);
                _logger.LogDebug("Toggle OFF: key {KeyName} at ({X}, {Y})", mapping.KeyName, mapping.TouchX, mapping.TouchY);
            }
            else
            {
                _activeToggles.Add(keyCode);
                _logger.LogDebug("Toggle ON: key {KeyName} at ({X}, {Y})", mapping.KeyName, mapping.TouchX, mapping.TouchY);
                await _adbClient.ExecuteCommandAsync(
                    _serial,
                    $"input tap {mapping.TouchX} {mapping.TouchY}",
                    ct).ConfigureAwait(false);
            }
        }
        else if (mapping.DurationMs > 0)
        {
            await _adbClient.ExecuteCommandAsync(
                _serial,
                $"input swipe {mapping.TouchX} {mapping.TouchY} {mapping.TouchX} {mapping.TouchY} {mapping.DurationMs}",
                ct).ConfigureAwait(false);
        }
        else
        {
            await _adbClient.ExecuteCommandAsync(
                _serial,
                $"input tap {mapping.TouchX} {mapping.TouchY}",
                ct).ConfigureAwait(false);
        }
    }

    public async Task HandleMouseMoveAsync(int deltaX, int deltaY, GameProfile profile, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        float scaledDeltaX = deltaX * profile.Sensitivity;
        float scaledDeltaY = deltaY * profile.Sensitivity;

        if (Math.Abs(scaledDeltaX) < profile.DeadZone && Math.Abs(scaledDeltaY) < profile.DeadZone)
            return;

        _logger.LogDebug("Mouse move delta: ({DeltaX}, {DeltaY}) scaled: ({ScaledX}, {ScaledY})",
            deltaX, deltaY, scaledDeltaX, scaledDeltaY);

        await _adbClient.ExecuteCommandAsync(
            _serial,
            $"input mouse {(int)scaledDeltaX} {(int)scaledDeltaY}",
            ct).ConfigureAwait(false);
    }

    public async Task HandleMouseButtonAsync(string action, int x, int y, GameProfile profile, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var mapping = profile.MouseMappings.FirstOrDefault(m =>
            m.Action.Equals(action, StringComparison.OrdinalIgnoreCase));

        if (mapping is null)
        {
            _logger.LogDebug("No mouse mapping found for action '{Action}' in profile '{ProfileName}'", action, profile.Name);
            return;
        }

        switch (action.ToLowerInvariant())
        {
            case "leftclick":
                await _adbClient.ExecuteCommandAsync(_serial, $"input tap {mapping.TouchX} {mapping.TouchY}", ct).ConfigureAwait(false);
                break;
            case "rightclick":
                await _adbClient.ExecuteCommandAsync(_serial, $"input swipe {mapping.TouchX} {mapping.TouchY} {mapping.TouchX} {mapping.TouchY} 1000", ct).ConfigureAwait(false);
                break;
            case "leftdrag":
                await _adbClient.ExecuteCommandAsync(
                    _serial,
                    $"input swipe {mapping.TouchX} {mapping.TouchY} {mapping.EndX} {mapping.EndY} 300",
                    ct).ConfigureAwait(false);
                break;
        }
    }

    public async Task DeleteProfileAsync(string profileId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        string filePath = GetProfilePath(profileId);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        if (_activeProfiles.TryGetValue(profileId, out var profile))
        {
            _activeProfiles.Remove(profileId);
            ProfileRemoved?.Invoke(this, profile);
        }

        _logger.LogInformation("Deleted profile {Id}", profileId);
        await Task.CompletedTask;
    }

    private string GetProfilePath(string profileId) =>
        Path.Combine(_profilesDirectory, $"{profileId}.json");

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _activeProfiles.Clear();
        _activeToggles.Clear();
        _logger.LogInformation("KeyMappingService disposed for device {Serial}", _serial);
        return ValueTask.CompletedTask;
    }
}
