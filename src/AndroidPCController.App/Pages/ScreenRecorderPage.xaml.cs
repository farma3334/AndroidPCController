using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace AndroidPCController.App.Pages;

public partial class ScreenRecorderPage : UserControl
{
    public ScreenRecorderPage()
    {
        InitializeComponent();
    }

    private void PlayRecording_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string filePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open file: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string filePath)
        {
            try
            {
                var directory = System.IO.Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Process.Start("explorer.exe", directory);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open folder: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
