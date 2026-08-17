using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using AndroidPCController.Core.Models;

namespace AndroidPCController.App.Pages;

public partial class AppsPage : UserControl
{
    public AppsPage()
    {
        InitializeComponent();
    }

    private void AppListView_ContextMenu(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListView listView && listView.SelectedItem != null &&
            Resources["AppContextMenu"] is ContextMenu contextMenu)
        {
            contextMenu.PlacementTarget = listView;
            contextMenu.IsOpen = true;
        }
    }

    private void LaunchMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (AppListView.SelectedItem is AndroidAppInfo app && DataContext is ViewModels.AppsViewModel vm)
        {
            vm.LaunchApp(app.PackageName);
        }
    }

    private void ForceStopMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (AppListView.SelectedItem is AndroidAppInfo app && DataContext is ViewModels.AppsViewModel vm)
        {
            vm.ForceStopApp(app.PackageName);
        }
    }

    private void UninstallMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (AppListView.SelectedItem is AndroidAppInfo app && DataContext is ViewModels.AppsViewModel vm)
        {
            vm.UninstallApp(app.PackageName);
        }
    }

    private void ClearDataMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (AppListView.SelectedItem is AndroidAppInfo app && DataContext is ViewModels.AppsViewModel vm)
        {
            vm.ClearAppData(app.PackageName);
        }
    }
}
