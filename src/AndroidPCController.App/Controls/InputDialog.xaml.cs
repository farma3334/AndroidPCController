using System.Windows;
using System.Windows.Input;

namespace AndroidPCController.App.Controls;

public partial class InputDialog : Window
{
    public string InputValue => ValueTextBox.Text;

    public InputDialog(string title, string prompt, string initialValue = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        ValueTextBox.Text = initialValue;
        ValueTextBox.Focus();
        ValueTextBox.SelectAll();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ValueTextBox.Text)) return;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ValueTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OkButton_Click(sender, e);
        }
    }
}