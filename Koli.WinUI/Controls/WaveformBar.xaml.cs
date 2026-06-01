using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Koli.WinUI.Controls;

public sealed partial class WaveformBar : UserControl
{
    private const int BarCount = 18;
    private readonly Rectangle[] _bars = new Rectangle[BarCount];
    private readonly double[] _currentHeights = new double[BarCount];
    private readonly double[] _phaseOffsets = new double[BarCount];
    private DispatcherTimer? _renderTimer;
    private double _phase;

    public WaveformBar()
    {
        InitializeComponent();
        BuildBars();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public double Level
    {
        get => (double)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public static readonly DependencyProperty LevelProperty =
        DependencyProperty.Register(nameof(Level), typeof(double), typeof(WaveformBar),
            new PropertyMetadata(0.0));

    private void BuildBars()
    {
        BarsHost.Children.Clear();
        var rand = new Random(7);
        for (int i = 0; i < BarCount; i++)
        {
            var bar = new Rectangle
            {
                Width = 4,
                RadiusX = 2,
                RadiusY = 2,
                Height = 4,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = (Brush)Application.Current.Resources["AuroraRibbonBrush"],
                Opacity = 0.85,
            };
            _bars[i] = bar;
            _phaseOffsets[i] = (i / (double)BarCount) * Math.PI * 2 + rand.NextDouble() * 0.5;
            BarsHost.Children.Add(bar);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _renderTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _renderTimer.Tick += OnTick;
        _renderTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_renderTimer is null) return;
        _renderTimer.Stop();
        _renderTimer.Tick -= OnTick;
    }

    private void OnTick(object? sender, object e)
    {
        _phase += 0.18;

        double level = Math.Clamp(Level, 0.0, 1.0);
        double available = LayoutRoot.ActualHeight > 0 ? LayoutRoot.ActualHeight : 36;
        double maxHeight = Math.Max(6, available - 2);
        double idleHeight = 4;

        for (int i = 0; i < BarCount; i++)
        {
            // Envelope: shape of |sin| so the wave forms a soft hump centered around middle bars
            double bell = Math.Sin(((double)i / (BarCount - 1)) * Math.PI);
            double waveOscillation = (Math.Sin(_phase + _phaseOffsets[i]) + 1) * 0.5; // 0..1
            double target = idleHeight + (maxHeight - idleHeight) * level * bell * (0.4 + 0.6 * waveOscillation);

            // Smoothing
            _currentHeights[i] += (target - _currentHeights[i]) * 0.25;
            _bars[i].Height = Math.Max(3, _currentHeights[i]);
        }
    }
}
