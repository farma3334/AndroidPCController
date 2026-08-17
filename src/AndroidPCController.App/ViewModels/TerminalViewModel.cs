using System.Collections.ObjectModel;
using System.IO;
using AndroidPCController.Core.Interfaces;
using AndroidPCController.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndroidPCController.App.ViewModels;

public partial class TerminalViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IDeviceManager _deviceManager;
    private readonly ILogService _logService;
    private IDeviceSession? _currentSession;
    private bool _disposed;
    private int _historyIndex = -1;

    [ObservableProperty]
    private string _commandText = string.Empty;

    [ObservableProperty]
    private string _outputText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _commandHistory = [];

    [ObservableProperty]
    private DeviceInfo? _selectedDevice;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _autoScroll = true;

    [ObservableProperty]
    private ObservableCollection<DeviceInfo> _availableDevices = [];

    public TerminalViewModel(IDeviceManager deviceManager, ILogService logService)
    {
        _deviceManager = deviceManager;
        _logService = logService;

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;

        RefreshDevices();

        AppendOutput("Android PC Controller - Terminal");
        AppendOutput("Type commands below. Use Up/Down arrows for history.");
        AppendOutput(new string('=', 50));
    }

    partial void OnSelectedDeviceChanged(DeviceInfo? value)
    {
        if (value is null) return;
        var session = _deviceManager.GetSession(value.Serial);
        if (session is not null)
        {
            _currentSession = session;
            SetSession(session);
        }
    }

    private void RefreshDevices()
    {
        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            AvailableDevices.Clear();
            IReadOnlyList<DeviceInfo> devices;
            try
            {
                devices = await _deviceManager.GetAvailableDevicesAsync();
            }
            catch
            {
                devices = [];
            }
            foreach (var device in devices)
            {
                AvailableDevices.Add(device);
            }
        });
    }

    [RelayCommand]
    private async Task ExecuteCommandAsync(string? command = null)
    {
        var effectiveCommand = (command ?? CommandText).Trim();
        if (string.IsNullOrWhiteSpace(effectiveCommand) || _currentSession is null || IsExecuting) return;

        var commandText = effectiveCommand;
        CommandText = string.Empty;

        try
        {
            IsExecuting = true;

            if (CommandHistory.Count == 0 || CommandHistory[^1] != commandText)
                CommandHistory.Add(commandText);
            _historyIndex = CommandHistory.Count;

            AppendOutput($"\n$ {commandText}");
            var result = await _currentSession.ExecuteShellCommandAsync(commandText);
            AppendOutput(result);
        }
        catch (Exception ex)
        {
            AppendOutput($"Error: {ex.Message}");
            _logService.Error("Terminal", $"Command failed: {commandText} - {ex.Message}", ex);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    [RelayCommand]
    private void Clear()
    {
        OutputText = string.Empty;
        AppendOutput("Terminal cleared.");
    }

    [RelayCommand]
    private async Task ExportLogsAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export terminal logs",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"terminal_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                await File.WriteAllTextAsync(dialog.FileName, OutputText);
                _logService.Information("Terminal", $"Logs exported to {dialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            _logService.Error("Terminal", $"Export failed: {ex.Message}", ex);
        }
    }

    public void NavigateHistoryUp()
    {
        if (CommandHistory.Count == 0) return;
        if (_historyIndex > 0)
        {
            _historyIndex--;
            CommandText = CommandHistory[_historyIndex];
        }
        else if (_historyIndex == 0)
        {
            CommandText = CommandHistory[0];
        }
    }

    public void NavigateHistoryDown()
    {
        if (CommandHistory.Count == 0) return;
        if (_historyIndex < CommandHistory.Count - 1)
        {
            _historyIndex++;
            CommandText = CommandHistory[_historyIndex];
        }
        else
        {
            _historyIndex = CommandHistory.Count;
            CommandText = string.Empty;
        }
    }

    public void SetSession(IDeviceSession? session)
    {
        _currentSession = session;

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            SelectedDevice = session?.DeviceInfo;
            IsConnected = session is not null;
            if (session is not null)
                AppendOutput($"\nConnected to: {session.DeviceInfo.Model} ({session.Serial})");
            else
                AppendOutput("\nDevice disconnected.");
        });
    }

    private void AppendOutput(string text)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
            OutputText += text + Environment.NewLine);
    }

    private void OnDeviceConnected(object? sender, DeviceConnectedEventArgs e)
    {
        _currentSession = e.Session;
        RefreshDevices();
        SetSession(e.Session);
    }

    private void OnDeviceDisconnected(object? sender, DeviceDisconnectedEventArgs e)
    {
        if (_currentSession?.Serial == e.Serial)
        {
            SetSession(null);
            RefreshDevices();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _deviceManager.DeviceConnected -= OnDeviceConnected;
        _deviceManager.DeviceDisconnected -= OnDeviceDisconnected;

        GC.SuppressFinalize(this);
    }
}
