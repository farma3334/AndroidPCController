using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AndroidPCController.App.Pages;

public partial class FilesPage : UserControl
{
    public FilesPage()
    {
        InitializeComponent();
    }

    private void FilesPage_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Copy;
        DragOverlay.Visibility = Visibility.Visible;
        e.Handled = true;
    }

    private void FilesPage_DragLeave(object sender, DragEventArgs e)
    {
        DragOverlay.Visibility = Visibility.Collapsed;
    }

    private void FilesPage_Drop(object sender, DragEventArgs e)
    {
        DragOverlay.Visibility = Visibility.Collapsed;

        if (e.Data.GetDataPresent(DataFormats.FileDrop) && DataContext is ViewModels.FilesViewModel vm)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null)
            {
                vm.UploadFiles(files);
            }
        }
    }

    private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Selection is handled by the ViewModel's SelectedFiles collection binding
    }

    private void FileListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FileListView.SelectedItem != null && DataContext is ViewModels.FilesViewModel vm)
        {
            vm.OpenItem(FileListView.SelectedItem);
        }
    }
}
