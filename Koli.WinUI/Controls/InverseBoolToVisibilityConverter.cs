using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Koli.WinUI.Controls;

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility v && v != Visibility.Visible;
}
