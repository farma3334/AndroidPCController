using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using AndroidPCController.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AndroidPCController.App.Controls;

public sealed class ScrcpyHost : HwndHost
{
    public static readonly List<ScrcpyHost> LiveHosts = [];

    private IntPtr _hostChildWindow;
    private IntPtr _childWindow;

    public IntPtr HostChildWindow => _hostChildWindow;

    public IntPtr ChildWindow => _childWindow;

    private const int WsChild = 0x40000000;
    private const int WsVisible = 0x10000000;
    private const int GwlStyle = -16;
    private const uint SwpNoZOrder = 0x4;
    private const uint SwpNoActivate = 0x10;
    private const uint WmClose = 0x0010;

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName,
        int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        var hwnd = CreateWindowEx(0, "static", string.Empty, WsChild | WsVisible,
            0, 0, 1, 1, hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        _hostChildWindow = hwnd;
        return new HandleRef(this, hwnd);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (_childWindow != IntPtr.Zero)
        {
            SendMessage(_childWindow, WmClose, IntPtr.Zero, IntPtr.Zero);
            _childWindow = IntPtr.Zero;
        }
        DestroyWindow(hwnd.Handle);
    }

    public void AttachWindow(IntPtr child)
    {
        if (child == IntPtr.Zero || _hostChildWindow == IntPtr.Zero) return;

        var manager = App.Services.GetService<ScrcpyManager>();
        if (manager is { IsRunning: true })
        {
            manager.SetHost(_hostChildWindow);
        }
        else
        {
            SetParent(child, _hostChildWindow);
        }

        _childWindow = child;

        var style = GetWindowLong(child, GwlStyle);
        SetWindowLong(child, GwlStyle, style | WsChild);

        ResizeChild();
        ShowWindow(child, 5);
        SetWindowPos(child, IntPtr.Zero, 0, 0, 0, 0, SwpNoZOrder | SwpNoActivate);

        if (!LiveHosts.Contains(this)) LiveHosts.Add(this);
    }

    public void ReleaseWindow()
    {
        if (_childWindow == IntPtr.Zero) return;

        _childWindow = IntPtr.Zero;

        var manager = App.Services.GetService<ScrcpyManager>();
        if (manager is { IsRunning: true })
        {
            manager.ReleaseHost(_hostChildWindow);
        }

        LiveHosts.Remove(this);
    }

    public void DetachWindow()
    {
        ReleaseWindow();
    }

    protected override void OnWindowPositionChanged(Rect rcBoundingBox)
    {
        base.OnWindowPositionChanged(rcBoundingBox);
        ResizeChild();
    }

    private void ResizeChild()
    {
        if (_childWindow == IntPtr.Zero) return;

        var width = Math.Max(1, (int)ActualWidth);
        var height = Math.Max(1, (int)ActualHeight);
        MoveWindow(_childWindow, 0, 0, width, height, true);
    }
}