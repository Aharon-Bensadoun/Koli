using Microsoft.UI.Xaml.Data;

namespace Koli.WinUI.Controls;

public sealed class BoolNegationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is bool b ? !b : true;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is bool b ? !b : false;
}
