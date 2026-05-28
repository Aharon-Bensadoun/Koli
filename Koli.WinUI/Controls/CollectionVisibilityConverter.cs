using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Koli.WinUI.Controls;

public sealed class CollectionVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var hasItems = value switch
        {
            null => false,
            ICollection c => c.Count > 0,
            IEnumerable e => e.GetEnumerator().MoveNext(),
            int n => n > 0,
            _ => false
        };
        return hasItems ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
