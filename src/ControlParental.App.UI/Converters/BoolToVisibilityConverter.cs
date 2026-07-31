namespace ControlParental.App.UI.Converters;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
    {
        return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}
