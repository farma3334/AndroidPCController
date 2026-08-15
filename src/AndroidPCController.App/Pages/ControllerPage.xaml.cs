using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AndroidPCController.App.Pages;

public partial class ControllerPage : UserControl
{
    public ControllerPage()
    {
        InitializeComponent();
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

    private bool _isFullscreen;

    private void FullscreenButton_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window == null) return;

        if (!_isFullscreen)
        {
            window.WindowState = WindowState.Maximized;
            window.WindowStyle = WindowStyle.None;
            FullscreenIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.FullscreenExit;
        }
        else
        {
            window.WindowState = WindowState.Normal;
            window.WindowStyle = WindowStyle.SingleBorderWindow;
            FullscreenIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Fullscreen;
        }
        _isFullscreen = !_isFullscreen;
    }
}
