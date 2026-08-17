namespace AndroidPCController.App.Services;

public sealed class TrayIconService : IDisposable
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private bool _disposed;

    public bool IsEnabled { get; private set; }

    public event Action? StateChanged;

    public event Action? RestoreRequested;

    public event Action? ExitRequested;

    public void Enable()
    {
        if (IsEnabled) return;
        IsEnabled = true;

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)
                   ?? System.Drawing.SystemIcons.Application,
            Text = "Android PC Controller",
            Visible = true
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open Android PC Controller", null, (_, _) => RestoreRequested?.Invoke());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => RestoreRequested?.Invoke();

        StateChanged?.Invoke();
    }

    public void Disable()
    {
        if (!IsEnabled) return;
        IsEnabled = false;

        _trayIcon?.Dispose();
        _trayIcon = null;

        StateChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}