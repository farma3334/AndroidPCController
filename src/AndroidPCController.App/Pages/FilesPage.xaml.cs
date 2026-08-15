using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace AndroidPCController.App.Pages;

public partial class FilesPage : UserControl
{
    public static readonly IValueConverter PercentageToWidthConverter = new PercentageToWidth();
    public static readonly IValueConverter BytesToStringConverter = new BytesToString();

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

    private bool _storageExpanded = true;

    private void StorageToggle_Click(object sender, MouseButtonEventArgs e)
    {
        _storageExpanded = !_storageExpanded;
        StorageContent.Visibility = _storageExpanded ? Visibility.Visible : Visibility.Collapsed;
        StorageChevron.Kind = _storageExpanded
            ? MaterialDesignThemes.Wpf.PackIconKind.ChevronDown
            : MaterialDesignThemes.Wpf.PackIconKind.ChevronRight;
    }
}

internal sealed class PercentageToWidth : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percentage)
        {
            return Math.Max(0, percentage);
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

internal sealed class BytesToString : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long bytes)
        {
            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.#} {sizes[order]}";
        }
        return "0 B";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
