using System.Windows;
using System.Windows.Controls;

namespace AndroidPCController.App.Pages;

public partial class LogsPage : UserControl
{
    public LogsPage()
    {
        InitializeComponent();
    }

    private void CloseExceptionPanel_Click(object sender, RoutedEventArgs e)
    {
        ExceptionPanel.Visibility = Visibility.Collapsed;
    }

    public void ShowException(string exceptionText)
    {
        ExceptionText.Text = exceptionText;
        ExceptionPanel.Visibility = Visibility.Visible;
    }
}
