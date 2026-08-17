using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AndroidPCController.App.ViewModels;

public partial class AutomationViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IDeviceManager _deviceManager;
    private readonly ILogService _logService;
    private bool _disposed;
    private IDeviceSession? _currentSession;
    private CancellationTokenSource? _runCts;
    private int _nodeCounter;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string ScriptsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AndroidPCController", "scripts");

    public ObservableCollection<FlowNodeViewModel> Nodes { get; } = [];

    public IReadOnlyList<AutomationAction> PaletteActions { get; } =
    [
        AutomationAction.LaunchApp, AutomationAction.Wait, AutomationAction.Tap,
        AutomationAction.LongPress, AutomationAction.Swipe, AutomationAction.PressKey,
        AutomationAction.InputText, AutomationAction.TakeScreenshot, AutomationAction.StopRecording,
        AutomationAction.Back, AutomationAction.Home, AutomationAction.Recent, AutomationAction.Sleep
    ];

    [ObservableProperty]
    private string _scriptName = "My Script";

    [ObservableProperty]
    private int _loopCount = 1;

    [ObservableProperty]
    private bool _loopForever;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = "Drag nodes to arrange the flow. Connect a device to run it.";

    [ObservableProperty]
    private string _currentStepText = string.Empty;

    [ObservableProperty]
    private FlowNodeViewModel? _selectedNode;

    public AutomationViewModel(IDeviceManager deviceManager, ILogService logService)
    {
        _deviceManager = deviceManager;
        _logService = logService;

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;

        SetSession(_deviceManager.ActiveSessions.FirstOrDefault());
    }

    public void SetSession(IDeviceSession? session)
    {
        _currentSession = session;
    }

    [RelayCommand]
    private void AddNode(AutomationAction action)
    {
        var offset = _nodeCounter++ % 6;
        var node = new FlowNodeViewModel(action, 40 + offset * 30, 40 + offset * 20);
        Nodes.Add(node);
        SelectedNode = node;
        StatusText = $"Added {node.Title}. Connect the previous node by arranging the flow.";
    }

    [RelayCommand]
    private void DeleteNode(FlowNodeViewModel? node)
    {
        if (node is null || IsRunning) return;
        Nodes.Remove(node);
        if (SelectedNode == node) SelectedNode = null;
        StatusText = $"Removed {node.Title}";
    }

    [RelayCommand]
    private void MoveNodeUp(FlowNodeViewModel? node)
    {
        if (node is null || IsRunning) return;
        var index = Nodes.IndexOf(node);
        if (index <= 0) return;
        Nodes.Move(index, index - 1);
    }

    [RelayCommand]
    private void MoveNodeDown(FlowNodeViewModel? node)
    {
        if (node is null || IsRunning) return;
        var index = Nodes.IndexOf(node);
        if (index < 0 || index >= Nodes.Count - 1) return;
        Nodes.Move(index, index + 1);
    }

    [RelayCommand]
    private void ClearScript()
    {
        if (IsRunning) return;
        Nodes.Clear();
        StatusText = "Flow cleared";
    }

    [RelayCommand]
    private async Task RunScriptAsync()
    {
        if (_currentSession is null)
        {
            StatusText = "No device connected. Connect a device first.";
            _logService.Warning("Automation", "Run requested without a device session");
            return;
        }

        if (Nodes.Count == 0)
        {
            StatusText = "The flow is empty. Add nodes first.";
            return;
        }

        if (IsRunning) return;

        try
        {
            IsRunning = true;
            _runCts = new CancellationTokenSource();
            StatusText = "Running...";

            var steps = Nodes.Select(n => n.ToStep()).ToList();
            var totalLoops = LoopForever ? -1 : Math.Max(1, LoopCount);

            for (var loop = 1; totalLoops < 0 || loop <= totalLoops; loop++)
            {
                if (_runCts.IsCancellationRequested) break;

                CurrentStepText = LoopForever || totalLoops > 1
                    ? $"Loop {loop}/{(LoopForever ? "\u221E" : totalLoops.ToString())}"
                    : string.Empty;

                foreach (var node in Nodes)
                {
                    if (_runCts.IsCancellationRequested) break;
                    node.IsCurrent = true;
                    CurrentStepText = $"{node.Title} ({Nodes.IndexOf(node) + 1}/{Nodes.Count})";
                    await ExecuteStepAsync(node, _runCts.Token);
                    node.IsCurrent = false;

                    if (node.DelayMs > 0)
                    {
                        await Task.Delay(node.DelayMs, _runCts.Token);
                    }
                }
            }

            StatusText = _runCts.IsCancellationRequested ? "Stopped" : "Script completed";
            _logService.Information("Automation", _runCts.IsCancellationRequested ? "Script stopped" : "Script completed");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Stopped";
        }
        catch (Exception ex)
        {
            StatusText = $"Script failed: {ex.Message}";
            _logService.Error("Automation", $"Script failed: {ex.Message}", ex);
        }
        finally
        {
            foreach (var node in Nodes) node.IsCurrent = false;
            CurrentStepText = string.Empty;
            IsRunning = false;
            _runCts?.Dispose();
            _runCts = null;
        }
    }

    [RelayCommand]
    private void StopScript()
    {
        _runCts?.Cancel();
    }

    private async Task ExecuteStepAsync(FlowNodeViewModel node, CancellationToken ct)
    {
        if (_currentSession is null) return;

        var input = _currentSession.InputController;
        var step = node.ToStep();

        switch (step.Action)
        {
            case AutomationAction.LaunchApp:
                if (!string.IsNullOrWhiteSpace(step.Text))
                {
                    await _currentSession.AppManager.LaunchAppAsync(step.Text, ct);
                }
                break;
            case AutomationAction.Wait:
                await Task.Delay(Math.Max(1, step.DelayMs), ct);
                break;
            case AutomationAction.Tap:
                await input.SendTapAsync(step.X, step.Y, ct);
                break;
            case AutomationAction.LongPress:
                await input.SendLongPressAsync(step.X, step.Y, Math.Max(100, step.DurationMs), ct);
                break;
            case AutomationAction.Swipe:
                await input.SendSwipeAsync(step.X, step.Y, step.EndX, step.EndY, Math.Max(1, step.DurationMs), ct);
                break;
            case AutomationAction.PressKey:
                await input.SendKeyEventAsync(step.KeyCode, ct: ct);
                break;
            case AutomationAction.InputText:
                if (!string.IsNullOrEmpty(step.Text))
                {
                    await input.SendTextAsync(step.Text, ct);
                }
                break;
            case AutomationAction.TakeScreenshot:
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads", "AndroidPCController");
                Directory.CreateDirectory(dir);
                await _currentSession.Screenshot.CaptureAndSaveAsync(dir, ct: ct);
                break;
            case AutomationAction.StopRecording:
                try { await _currentSession.ScreenRecorder.StopAsync(ct); }
                catch { }
                break;
            case AutomationAction.Back:
                await input.PressBackAsync(ct);
                break;
            case AutomationAction.Home:
                await input.PressHomeAsync(ct);
                break;
            case AutomationAction.Recent:
                await input.PressRecentAppsAsync(ct);
                break;
            case AutomationAction.Sleep:
                await input.SendKeyEventAsync(26, ct: ct);
                break;
        }
    }

    [RelayCommand]
    private void SaveScript()
    {
        if (Nodes.Count == 0)
        {
            StatusText = "Nothing to save — the flow is empty.";
            return;
        }

        try
        {
            Directory.CreateDirectory(ScriptsDirectory);

            var dialog = new SaveFileDialog
            {
                Title = "Save Automation Script",
                Filter = "Automation script (*.json)|*.json",
                FileName = $"{SanitizeFileName(ScriptName)}.json",
                InitialDirectory = ScriptsDirectory
            };

            if (dialog.ShowDialog() != true) return;

            var script = new AutomationScript
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = ScriptName,
                Steps = Nodes.Select(n => n.ToStep()).ToList(),
                LoopForever = LoopForever,
                LoopCount = LoopCount,
                CreatedAt = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(script, JsonOptions);
            File.WriteAllText(dialog.FileName, json);

            StatusText = $"Saved to {Path.GetFileName(dialog.FileName)}";
            _logService.Information("Automation", $"Script saved: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            StatusText = $"Save failed: {ex.Message}";
            _logService.Error("Automation", $"Save failed: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private void LoadScript()
    {
        if (IsRunning) return;

        try
        {
            Directory.CreateDirectory(ScriptsDirectory);

            var dialog = new OpenFileDialog
            {
                Title = "Load Automation Script",
                Filter = "Automation script (*.json)|*.json",
                InitialDirectory = ScriptsDirectory
            };

            if (dialog.ShowDialog() != true) return;

            var json = File.ReadAllText(dialog.FileName);
            var script = JsonSerializer.Deserialize<AutomationScript>(json, JsonOptions);
            if (script is null)
            {
                StatusText = "Could not read the script file.";
                return;
            }

            Nodes.Clear();
            var x = 40d;
            var y = 40d;
            foreach (var step in script.Steps)
            {
                var node = new FlowNodeViewModel(step.Action, x, y)
                {
                    XCoord = step.X,
                    YCoord = step.Y,
                    EndX = step.EndX,
                    EndY = step.EndY,
                    DurationMs = step.DurationMs,
                    DelayMs = step.DelayMs,
                    KeyCode = step.KeyCode,
                    Text = step.Text ?? string.Empty
                };
                Nodes.Add(node);
                x += 40;
                y += 20;
            }

            ScriptName = script.Name;
            LoopForever = script.LoopForever;
            LoopCount = script.LoopCount;
            StatusText = $"Loaded {Path.GetFileName(dialog.FileName)} ({Nodes.Count} steps)";
            _logService.Information("Automation", $"Script loaded: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            StatusText = $"Load failed: {ex.Message}";
            _logService.Error("Automation", $"Load failed: {ex.Message}", ex);
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }

    private void OnDeviceConnected(object? sender, DeviceConnectedEventArgs e)
    {
        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => SetSession(e.Session));
    }

    private void OnDeviceDisconnected(object? sender, DeviceDisconnectedEventArgs e)
    {
        if (_currentSession?.Serial == e.Serial)
        {
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                SetSession(null);
                _runCts?.Cancel();
            });
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _runCts?.Cancel();
        _deviceManager.DeviceConnected -= OnDeviceConnected;
        _deviceManager.DeviceDisconnected -= OnDeviceDisconnected;

        GC.SuppressFinalize(this);
        await Task.CompletedTask;
    }
}