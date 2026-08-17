using System.Windows;
using System.Windows.Input;
using AndroidPCController.App.Controls;

namespace AndroidPCController.App;

public partial class FullscreenStreamWindow : Window
{
    private readonly ScrcpyHost _sourceHost;
    private readonly IntPtr _scrcpyHwnd;
    private bool _skipReparentBack;

    public event Action? Exited;

    public FullscreenStreamWindow(ScrcpyHost sourceHost, IntPtr scrcpyHwnd)
    {
        InitializeComponent();
        _sourceHost = sourceHost;
        _scrcpyHwnd = scrcpyHwnd;

        Loaded += (_, _) =>
        {
            _sourceHost.ReleaseWindow();
            Host.AttachWindow(_scrcpyHwnd);
        };
    }

    public void CloseAndSkipReparent()
    {
        _skipReparentBack = true;
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        Host.ReleaseWindow();

        if (!_skipReparentBack)
        {
            _sourceHost.AttachWindow(_scrcpyHwnd);
        }

        Exited?.Invoke();
    }
}