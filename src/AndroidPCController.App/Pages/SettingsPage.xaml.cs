using System.Windows;
using System.Windows.Controls;

namespace AndroidPCController.App.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private void Category_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radio || radio.Tag is not string category) return;

        CategoryTitle.Text = category;

        GeneralPanel.Visibility = Visibility.Collapsed;
        ConnectionPanel.Visibility = Visibility.Collapsed;
        StreamingPanel.Visibility = Visibility.Collapsed;
        InputPanel.Visibility = Visibility.Collapsed;
        FilesPanel.Visibility = Visibility.Collapsed;
        PrivacyPanel.Visibility = Visibility.Collapsed;
        AdvancedPanel.Visibility = Visibility.Collapsed;

        switch (category)
        {
            case "General":
                GeneralPanel.Visibility = Visibility.Visible;
                break;
            case "Connection":
                ConnectionPanel.Visibility = Visibility.Visible;
                break;
            case "Streaming":
                StreamingPanel.Visibility = Visibility.Visible;
                break;
            case "Input":
                InputPanel.Visibility = Visibility.Visible;
                break;
            case "Files":
                FilesPanel.Visibility = Visibility.Visible;
                break;
            case "Privacy":
                PrivacyPanel.Visibility = Visibility.Visible;
                break;
            case "Advanced":
                AdvancedPanel.Visibility = Visibility.Visible;
                break;
        }
    }
}
