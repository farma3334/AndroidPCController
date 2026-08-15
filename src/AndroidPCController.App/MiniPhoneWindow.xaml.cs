using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using AndroidPCController.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AndroidPCController.App;

public partial class MiniPhoneWindow : Window
{
    public MiniPhoneWindow()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MiniPhoneViewModel>();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleTopmost();
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleTopmost()
    {
        if (DataContext is MiniPhoneViewModel vm)
        {
            vm.ToggleTopmostCommand.Execute(null);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is IAsyncDisposable disposable)
        {
            disposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        base.OnClosed(e);
    }
}
