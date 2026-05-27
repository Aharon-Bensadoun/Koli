using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinUIEx;

namespace Koli.WinUI.Overlays;

public sealed class CursorOverlayWindow : WindowEx
{
    private readonly TextBlock _label;

    public CursorOverlayWindow()
    {
        Title = "";
        Width = 280;
        Height = 48;
        IsAlwaysOnTop = true;
        IsShownInSwitchers = false;
        IsTitleBarVisible = false;

        var root = new Border
        {
            Background = Application.Current.Resources["CardBackgroundBrush"] as Brush,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 10, 16, 10),
            Child = _label = new TextBlock
            {
                Foreground = Application.Current.Resources["TextPrimaryBrush"] as Brush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.WrapWholeWords
            }
        };

        Content = root;
    }

    public void ShowMessage(string text)
    {
        _label.Text = text;
        if (GetCursorPos(out var point))
            AppWindow.Move(new global::Windows.Graphics.PointInt32(point.X + 16, point.Y + 16));
        Activate();
    }

    public void HideOverlay() => AppWindow.Hide();

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}
