using Microsoft.UI.Xaml.Data;

namespace Koli.WinUI.Controls;

public sealed class NameToInitialsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string name || string.IsNullOrWhiteSpace(name))
            return "?";

        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "?";

        if (parts.Length == 1)
        {
            var single = parts[0];
            return single.Length == 1
                ? single.ToUpperInvariant()
                : single[..2].ToUpperInvariant();
        }

        return (char.ToUpperInvariant(parts[0][0]).ToString()
                + char.ToUpperInvariant(parts[^1][0]).ToString());
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
