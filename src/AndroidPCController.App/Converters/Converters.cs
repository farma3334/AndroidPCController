using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AndroidPCController.App.Converters;

public sealed class HealthScoreToColorConverter : IValueConverter
{
    public static readonly HealthScoreToColorConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int score = value is int i ? i : 0;
        if (score >= 80) return Color.FromRgb(34, 197, 94);
        if (score >= 60) return Color.FromRgb(245, 158, 11);
        if (score >= 40) return Color.FromRgb(249, 115, 22);
        return Color.FromRgb(239, 68, 68);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BytesToSizeConverter : IValueConverter
{
    public static readonly BytesToSizeConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long bytes) return "0 B";

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

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class CpuUsageToColorConverter : IValueConverter
{
    public static readonly CpuUsageToColorConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double usage = value is double d ? d : 0;
        if (usage < 50) return Color.FromRgb(0, 210, 255);
        if (usage < 75) return Color.FromRgb(245, 158, 11);
        return Color.FromRgb(239, 68, 68);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class RamUsageToColorConverter : IValueConverter
{
    public static readonly RamUsageToColorConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double usage = value is double d ? d : 0;
        if (usage < 60) return Color.FromRgb(0, 210, 255);
        if (usage < 80) return Color.FromRgb(245, 158, 11);
        return Color.FromRgb(239, 68, 68);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BatteryLevelToColorConverter : IValueConverter
{
    public static readonly BatteryLevelToColorConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int level = value is int i ? i : 0;
        if (level > 50) return Color.FromRgb(34, 197, 94);
        if (level > 20) return Color.FromRgb(245, 158, 11);
        return Color.FromRgb(239, 68, 68);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ConnectionQualityToColorConverter : IValueConverter
{
    public static readonly ConnectionQualityToColorConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string quality = value as string ?? "Unknown";
        return quality switch
        {
            "Excellent" => Color.FromRgb(34, 197, 94),
            "Good" => Color.FromRgb(0, 210, 255),
            "Fair" => Color.FromRgb(245, 158, 11),
            "Poor" => Color.FromRgb(249, 115, 22),
            "Critical" => Color.FromRgb(239, 68, 68),
            _ => Color.FromRgb(128, 128, 128)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ConnectionTypeToBrushConverter : IValueConverter
{
    public static readonly ConnectionTypeToBrushConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string type = value?.ToString() ?? "Unknown";
        return type switch
        {
            "Usb" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
            "Wireless" => new SolidColorBrush(Color.FromRgb(0, 210, 255)),
            _ => new SolidColorBrush(Color.FromRgb(128, 128, 128))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BatteryToColorConverter : IValueConverter
{
    public static readonly BatteryToColorConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int level = value is int i ? i : 0;
        if (level > 50) return Color.FromRgb(34, 197, 94);
        if (level > 20) return Color.FromRgb(245, 158, 11);
        return Color.FromRgb(239, 68, 68);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StringToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? input = value?.ToString();
        if (string.IsNullOrEmpty(input)) return new SolidColorBrush(Colors.Gray);

        if (input.StartsWith("#"))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(input);
                return new SolidColorBrush(color);
            }
            catch { }
        }

        return input switch
        {
            "Connected" or "Excellent" or "Good" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
            "Fair" or "Unknown" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
            "Poor" or "Critical" or "Disconnected" or "Error" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            "Unauthorized" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
            "USB" or "Usb" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
            "Wireless" => new SolidColorBrush(Color.FromRgb(0, 210, 255)),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
