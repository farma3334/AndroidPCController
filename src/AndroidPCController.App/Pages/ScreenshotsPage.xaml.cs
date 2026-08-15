using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AndroidPCController.App.Pages;

public partial class ScreenshotsPage : UserControl
{
    public ScreenshotsPage()
    {
        InitializeComponent();
    }

    private void Thumbnail_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ViewModels.ScreenshotItem item && DataContext is ViewModels.ScreenshotsViewModel vm)
        {
            vm.SelectedScreenshot = item;
        }
    }
}
