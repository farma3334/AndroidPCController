using System.Windows;
using System.Windows.Controls;
using AndroidPCController.App.ViewModels;

namespace AndroidPCController.App.Pages;

public partial class PhoneDesktopPage : UserControl
{
    public PhoneDesktopPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PhoneDesktopViewModel vm)
        {
            vm.ScrcpyWindowReady += OnScrcpyWindowReady;
            vm.ScrcpyStopped += OnScrcpyStopped;
            await vm.StartScrcpyAsync();
        }
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PhoneDesktopViewModel vm)
        {
            vm.ScrcpyWindowReady -= OnScrcpyWindowReady;
            vm.ScrcpyStopped -= OnScrcpyStopped;
            await vm.StopScrcpyAsync();
        }
    }

    private void OnScrcpyWindowReady(object? sender, IntPtr hwnd)
    {
        ScrcpyHost.AttachWindow(hwnd);
    }

    private void OnScrcpyStopped(object? sender, EventArgs e)
    {
        ScrcpyHost.DetachWindow();
    }
}