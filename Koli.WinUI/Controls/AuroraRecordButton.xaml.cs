using System;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Koli.WinUI.Controls;

public enum AuroraRecordMode
{
    Dictation,
    Assistant,
    Processing
}

public sealed partial class AuroraRecordButton : UserControl
{
    private Storyboard? _breathing;

    public AuroraRecordButton()
    {
        InitializeComponent();
        BuildBreathingStoryboard();
        UpdateVisualState();
    }

    private void BuildBreathingStoryboard()
    {
        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
        var duration = new Duration(TimeSpan.FromMilliseconds(1200));

        var opacityAnim = new DoubleAnimation
        {
            From = 0.22,
            To = 0.55,
            Duration = duration,
            EasingFunction = ease,
        };
        Storyboard.SetTarget(opacityAnim, HaloEllipse);
        Storyboard.SetTargetProperty(opacityAnim, "Opacity");

        var scaleXAnim = new DoubleAnimation
        {
            From = 0.92,
            To = 1.05,
            Duration = duration,
            EasingFunction = ease,
        };
        Storyboard.SetTarget(scaleXAnim, HaloScale);
        Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");

        var scaleYAnim = new DoubleAnimation
        {
            From = 0.92,
            To = 1.05,
            Duration = duration,
            EasingFunction = ease,
        };
        Storyboard.SetTarget(scaleYAnim, HaloScale);
        Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");

        _breathing = new Storyboard
        {
            RepeatBehavior = RepeatBehavior.Forever,
            AutoReverse = true,
        };
        _breathing.Children.Add(opacityAnim);
        _breathing.Children.Add(scaleXAnim);
        _breathing.Children.Add(scaleYAnim);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(AuroraRecordButton),
            new PropertyMetadata(false, (d, _) => ((AuroraRecordButton)d).UpdateVisualState()));

    public AuroraRecordMode Mode
    {
        get => (AuroraRecordMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(nameof(Mode), typeof(AuroraRecordMode), typeof(AuroraRecordButton),
            new PropertyMetadata(AuroraRecordMode.Dictation, (d, _) => ((AuroraRecordButton)d).UpdateVisualState()));

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(AuroraRecordButton),
            new PropertyMetadata("", (d, e) => ((AuroraRecordButton)d).GlyphIcon.Glyph = (string)e.NewValue));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(AuroraRecordButton),
            new PropertyMetadata(null));

    public string? ToolTip
    {
        get => (string?)GetValue(ToolTipProperty);
        set => SetValue(ToolTipProperty, value);
    }

    public static readonly DependencyProperty ToolTipProperty =
        DependencyProperty.Register(nameof(ToolTip), typeof(string), typeof(AuroraRecordButton),
            new PropertyMetadata(null, (d, e) =>
                ToolTipService.SetToolTip(((AuroraRecordButton)d).CoreButton, e.NewValue)));

    private void CoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (Command?.CanExecute(null) == true)
            Command.Execute(null);
    }

    private void UpdateVisualState()
    {
        Brush halo = Mode switch
        {
            AuroraRecordMode.Assistant => (Brush)Application.Current.Resources["AssistantHaloBrush"],
            AuroraRecordMode.Processing => (Brush)Application.Current.Resources["AuroraHaloBrush"],
            _ => (Brush)Application.Current.Resources["RecordingHaloBrush"],
        };

        Brush ring = Mode switch
        {
            AuroraRecordMode.Assistant => (Brush)Application.Current.Resources["AuroraCyanBrush"],
            AuroraRecordMode.Processing => (Brush)Application.Current.Resources["AuroraGlowBrush"],
            _ => (Brush)Application.Current.Resources["RecordingGlowBrush"],
        };

        HaloEllipse.Fill = halo;
        RingEllipse.Stroke = IsActive ? (Brush)Application.Current.Resources["AuroraRibbonBrush"] : ring;
        RingEllipse.Opacity = IsActive ? 0.95 : 0.45;

        if (_breathing is null) return;

        if (IsActive)
        {
            _breathing.Begin();
        }
        else
        {
            _breathing.Stop();
            HaloEllipse.Opacity = 0;
        }
    }
}
