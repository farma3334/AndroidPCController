using System.Windows;

namespace AndroidPCController.App.Controls;

public partial class ClipboardDialog : Window
{
    public string ClipboardText => ClipboardTextBox.Text;

    public ClipboardDialog(string initialContent)
    {
        InitializeComponent();
        ClipboardTextBox.Text = initialContent;
        ClipboardTextBox.Focus();
        ClipboardTextBox.SelectAll();
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}