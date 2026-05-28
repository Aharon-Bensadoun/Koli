using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Koli.WinUI.Controls;

public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && TryParseHex(hex, out var color))
            return new SolidColorBrush(color);
        return new SolidColorBrush(Color.FromArgb(0xFF, 0x7C, 0x3A, 0xED));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();

    private static bool TryParseHex(string input, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var s = input.Trim();
        if (s.StartsWith('#'))
            s = s[1..];

        try
        {
            if (s.Length == 6)
            {
                var r = System.Convert.ToByte(s.Substring(0, 2), 16);
                var g = System.Convert.ToByte(s.Substring(2, 2), 16);
                var b = System.Convert.ToByte(s.Substring(4, 2), 16);
                color = Color.FromArgb(0xFF, r, g, b);
                return true;
            }

            if (s.Length == 8)
            {
                var a = System.Convert.ToByte(s.Substring(0, 2), 16);
                var r = System.Convert.ToByte(s.Substring(2, 2), 16);
                var g = System.Convert.ToByte(s.Substring(4, 2), 16);
                var b = System.Convert.ToByte(s.Substring(6, 2), 16);
                color = Color.FromArgb(a, r, g, b);
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
