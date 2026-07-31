using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using D2RBuffTracker.Models;
using D2RBuffTracker.Services;

namespace D2RBuffTracker.Views;

/// <summary>Loads a stored buff icon path into a cached ImageSource.</summary>
public sealed class IconPathToImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var resolved = AssetPaths.Resolve(value as string);
        if (resolved == null)
            return null;
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.UriSource = new Uri(resolved);
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Converts a DiabloClass? into its accent brush.</summary>
public sealed class ClassAccentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hex = value is DiabloClass c ? DiabloClassInfo.AccentHex(c) : "#7C4DFF";
        var brush = (Brush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>True/false to Visible/Collapsed, with optional "Invert" parameter.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
            flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

/// <summary>Not-null (or non-empty count) to Visible/Collapsed, "Invert" supported.</summary>
public sealed class EmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var empty = value switch
        {
            null => true,
            int i => i == 0,
            System.Collections.ICollection col => col.Count == 0,
            string s => string.IsNullOrEmpty(s),
            _ => false
        };
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
            empty = !empty;
        return empty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Returns a friendly label for a binding, or a placeholder if unset.</summary>
public sealed class BindingLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is InputBinding b && !string.IsNullOrEmpty(b.Display) ? b.Display : "Not set";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Enum equals parameter -> Visible, else Collapsed.</summary>
public sealed class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Enum equals parameter -> true, else false (for nav highlighting).</summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true && parameter != null
            ? Enum.Parse(targetType, parameter.ToString()!)
            : Binding.DoNothing;
}
