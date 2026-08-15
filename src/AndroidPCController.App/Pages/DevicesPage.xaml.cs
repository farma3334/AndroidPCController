using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using AndroidPCController.Core.Models;

namespace AndroidPCController.App.Pages;

public partial class DevicesPage : UserControl
{
    public DevicesPage()
    {
        InitializeComponent();
    }

    private void DeviceItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is DeviceInfo device)
        {
            if (DataContext is ViewModels.DevicesViewModel vm)
            {
                vm.SelectedDevice = device;
            }
        }
    }
}
