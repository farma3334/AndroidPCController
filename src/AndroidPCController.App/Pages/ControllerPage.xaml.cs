using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AndroidPCController.App.Pages;

public partial class ControllerPage : UserControl
{
    public ControllerPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.ControllerViewModel vm && !vm.IsStreaming && vm.HasDeviceSession)
        {
            vm.StartStreamCommand.Execute(null);
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ViewModels.ControllerViewModel oldVm)
        {
            oldVm.ScrcpyWindowReady -= OnScrcpyWindowReady;
            oldVm.ScrcpyStopped -= OnScrcpyStopped;
        }

        if (DataContext is ViewModels.ControllerViewModel vm)
        {
            vm.ScrcpyWindowReady += OnScrcpyWindowReady;
            vm.ScrcpyStopped += OnScrcpyStopped;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.ControllerViewModel vm)
        {
            vm.ScrcpyWindowReady -= OnScrcpyWindowReady;
            vm.ScrcpyStopped -= OnScrcpyStopped;
            _ = vm.StopStreamAsync();
        }
    }

    private void OnScrcpyWindowReady(object? sender, IntPtr hwnd)
    {
        ScrcpyHost.AttachWindow(hwnd);
    }

    private void OnScrcpyStopped(object? sender, EventArgs e)
    {
        ScrcpyHost.DetachWindow();
        _fullscreenWindow?.CloseAndSkipReparent();
    }

    private void StreamImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.ControllerViewModel vm)
        {
            var position = e.GetPosition(StreamImage);
            var relativeX = position.X / StreamImage.ActualWidth;
            var relativeY = position.Y / StreamImage.ActualHeight;
            vm.HandleTap(relativeX, relativeY);
        }
    }

    private void StreamImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && DataContext is ViewModels.ControllerViewModel vm)
        {
            var position = e.GetPosition(StreamImage);
            var relativeX = position.X / StreamImage.ActualWidth;
            var relativeY = position.Y / StreamImage.ActualHeight;
            vm.HandleMouseMove(relativeX, relativeY);
        }
    }

    private void StreamImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.ControllerViewModel vm)
        {
            var position = e.GetPosition(StreamImage);
            var relativeX = position.X / StreamImage.ActualWidth;
            var relativeY = position.Y / StreamImage.ActualHeight;
            vm.HandleRightClick(relativeX, relativeY);
        }
    }

    private FullscreenStreamWindow? _fullscreenWindow;

    private void FullscreenButton_Click(object sender, RoutedEventArgs e)
    {
        if (_fullscreenWindow != null)
        {
            _fullscreenWindow.Close();
            return;
        }

        var hwnd = ScrcpyHost.ChildWindow;
        if (hwnd == IntPtr.Zero)
            return;

        _fullscreenWindow = new FullscreenStreamWindow(ScrcpyHost, hwnd)
        {
            Owner = Window.GetWindow(this)
        };
        _fullscreenWindow.Exited += () =>
        {
            _fullscreenWindow = null;
            FullscreenIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Fullscreen;
        };
        _fullscreenWindow.Show();
        FullscreenIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.FullscreenExit;
    }
}
