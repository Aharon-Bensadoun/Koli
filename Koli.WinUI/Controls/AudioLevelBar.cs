using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Koli.WinUI.Controls;

public sealed class AudioLevelBar : UserControl
{
    public static readonly DependencyProperty LevelProperty =
        DependencyProperty.Register(nameof(Level), typeof(float), typeof(AudioLevelBar),
            new PropertyMetadata(0f, (d, _) => ((AudioLevelBar)d).Redraw()));

    public float Level
    {
        get => (float)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    private readonly Canvas _canvas = new();

    public AudioLevelBar()
    {
        Content = _canvas;
        SizeChanged += (_, _) => Redraw();
    }

    private void Redraw()
    {
        _canvas.Children.Clear();
        if (ActualWidth <= 0 || ActualHeight <= 0)
            return;

        const int bars = 24;
        var barWidth = ActualWidth / bars - 2;
        var brush = Application.Current.Resources["AccentGlowBrush"] as SolidColorBrush;
        var idle = Application.Current.Resources["BorderBrush"] as SolidColorBrush;

        for (var i = 0; i < bars; i++)
        {
            var threshold = (float)(i + 1) / bars;
            var active = Level >= threshold * 0.85f;
            var height = ActualHeight * (0.25 + threshold * 0.75);
            var rect = new Rectangle
            {
                Width = Math.Max(2, barWidth),
                Height = height,
                Fill = active ? brush : idle,
                RadiusX = 2,
                RadiusY = 2
            };
            Canvas.SetLeft(rect, i * (barWidth + 2));
            Canvas.SetTop(rect, ActualHeight - height);
            _canvas.Children.Add(rect);
        }
    }
}
