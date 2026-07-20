using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using WinRT.Interop;
using WinUIEx;

namespace Koli.WinUI.Overlays;

public enum CursorIndicatorState
{
    Hidden,
    DictationRecording,
    AssistantRecording,
    Processing
}

public sealed class CursorIndicatorWindow : WindowEx
{
    private const int WindowSize = 64;
    private const int GlowSize = 40;
    private const int RippleSize = 20;
    private const int CoreSize = 11;
    private const int CoreRingSize = 17;
    private const int CursorOffset = 16;

    private const int GwlExstyle = -20;
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly Ellipse _glow;
    private readonly Ellipse _ripple;
    private readonly Ellipse _coreRing;
    private readonly Ellipse _core;
    private readonly ScaleTransform _glowScale;
    private readonly ScaleTransform _rippleScale;
    private readonly RadialGradientBrush _glowBrush = new();
    private readonly GradientStop _glowInnerStop = new() { Offset = 0.0 };
    private readonly GradientStop _glowOuterStop = new() { Offset = 1.0 };
    private readonly SolidColorBrush _rippleBrush = new();
    private readonly SolidColorBrush _coreRingBrush = new();
    private readonly SolidColorBrush _coreBrush = new();
    private readonly DispatcherQueue _dispatcher;
    private DispatcherQueueTimer? _followTimer;
    private DispatcherQueueTimer? _pulseTimer;
    private CursorIndicatorState _state = CursorIndicatorState.Hidden;
    private double _breathPhase;
    private double _ripplePhase;
    private bool _chromeConfigured;

    public CursorIndicatorWindow()
    {
        Title = "";
        Width = WindowSize;
        Height = WindowSize;
        IsAlwaysOnTop = true;
        IsShownInSwitchers = false;
        IsTitleBarVisible = false;
        IsMaximizable = false;
        IsMinimizable = false;
        IsResizable = false;

        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // Soft ambient glow behind everything — gently breathes.
        _glowBrush.GradientStops.Add(_glowInnerStop);
        _glowBrush.GradientStops.Add(_glowOuterStop);
        // Center of scaling is handled by RenderTransformOrigin (0.5,0.5); leave Center at 0.
        _glowScale = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
        _glow = new Ellipse
        {
            Width = GlowSize,
            Height = GlowSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = _glowBrush,
            Opacity = 0.0,
            IsHitTestVisible = false,
            RenderTransformOrigin = new global::Windows.Foundation.Point(0.5, 0.5),
            RenderTransform = _glowScale,
        };

        // Expanding ripple ring — the "pulse" that radiates outward and fades.
        _rippleScale = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
        _ripple = new Ellipse
        {
            Width = RippleSize,
            Height = RippleSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Stroke = _rippleBrush,
            StrokeThickness = 1.5,
            Opacity = 0.0,
            IsHitTestVisible = false,
            RenderTransformOrigin = new global::Windows.Foundation.Point(0.5, 0.5),
            RenderTransform = _rippleScale,
        };

        // Thin outline that frames the core for a crisp, contained look.
        _coreRing = new Ellipse
        {
            Width = CoreRingSize,
            Height = CoreRingSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Stroke = _coreRingBrush,
            StrokeThickness = 1.5,
            IsHitTestVisible = false,
        };

        // Solid, saturated core dot — the stable anchor of the indicator.
        _core = new Ellipse
        {
            Width = CoreSize,
            Height = CoreSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = _coreBrush,
            IsHitTestVisible = false,
        };

        Content = new Grid
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(0, 0, 0, 0)),
            Children = { _glow, _ripple, _coreRing, _core }
        };
    }

    public void ShowState(CursorIndicatorState state)
    {
        if (state == CursorIndicatorState.Hidden)
        {
            HideIndicator();
            return;
        }

        _state = state;
        ApplyPalette(state);
        _breathPhase = 0;
        _ripplePhase = 0;

        ConfigureChrome();
        MoveNearCursor();
        AppWindow.Show();
        StartFollowTimer();
        StartPulseTimer();
    }

    public void HideIndicator()
    {
        _state = CursorIndicatorState.Hidden;
        StopFollowTimer();
        StopPulseTimer();
        AppWindow.Hide();
    }

    private void ApplyPalette(CursorIndicatorState state)
    {
        // Accent color drives the whole indicator; alpha varies per layer.
        Color accent = state switch
        {
            CursorIndicatorState.AssistantRecording => ColorHelper.FromArgb(255, 0x22, 0xD3, 0xEE), // cyan
            CursorIndicatorState.Processing => ColorHelper.FromArgb(255, 0xA7, 0x8B, 0xFA),         // violet
            _ => ColorHelper.FromArgb(255, 0xFF, 0x45, 0x63),                                        // recording red
        };

        _glowInnerStop.Color = WithAlpha(accent, 0x9E);
        _glowOuterStop.Color = WithAlpha(accent, 0x00);
        _rippleBrush.Color = WithAlpha(accent, 0xE6);
        _coreRingBrush.Color = WithAlpha(accent, 0x99);
        _coreBrush.Color = accent;
    }

    private void ConfigureChrome()
    {
        if (_chromeConfigured)
            return;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.SetBorderAndTitleBar(false, false);

        var hwnd = WindowNative.GetWindowHandle(this);
        var style = GetWindowLong(hwnd, GwlExstyle);
        SetWindowLong(hwnd, GwlExstyle, style | WsExLayered | WsExTransparent | WsExToolWindow | WsExNoActivate);

        _chromeConfigured = true;
    }

    private void MoveNearCursor()
    {
        if (!GetCursorPos(out var point))
            return;

        AppWindow.Move(new global::Windows.Graphics.PointInt32(point.X + CursorOffset, point.Y + CursorOffset));
    }

    private void StartFollowTimer()
    {
        StopFollowTimer();
        _followTimer = _dispatcher.CreateTimer();
        _followTimer.Interval = TimeSpan.FromMilliseconds(40);
        _followTimer.Tick += (_, _) => MoveNearCursor();
        _followTimer.Start();
    }

    private void StopFollowTimer()
    {
        if (_followTimer == null)
            return;
        _followTimer.Stop();
        _followTimer = null;
    }

    private void StartPulseTimer()
    {
        StopPulseTimer();
        _pulseTimer = _dispatcher.CreateTimer();
        _pulseTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 fps for a smooth animation

        // Processing radiates a touch faster to read as "busy".
        double rippleSpeed = _state == CursorIndicatorState.Processing ? 0.011 : 0.008;
        double breathSpeed = _state == CursorIndicatorState.Processing ? 0.055 : 0.040;

        _pulseTimer.Tick += (_, _) =>
        {
            if (_state == CursorIndicatorState.Hidden)
                return;

            // Ripple: linear 0..1, expands outward while fading. Eased so it slows as it fades.
            _ripplePhase += rippleSpeed;
            if (_ripplePhase >= 1.0)
                _ripplePhase -= 1.0;

            double t = _ripplePhase;
            double eased = 1.0 - Math.Pow(1.0 - t, 2.0); // ease-out
            double rippleScale = 0.55 + eased * 1.85;      // 0.55 -> 2.40
            _rippleScale.ScaleX = rippleScale;
            _rippleScale.ScaleY = rippleScale;
            _ripple.Opacity = (1.0 - t) * 0.55;            // brightest at birth, gone at the edge

            // Glow: slow sine breath in opacity + scale, colors stay saturated.
            _breathPhase += breathSpeed;
            double s = (Math.Sin(_breathPhase) + 1.0) * 0.5; // 0..1
            _glow.Opacity = 0.30 + s * 0.30;
            double glowScale = 0.92 + s * 0.14;
            _glowScale.ScaleX = glowScale;
            _glowScale.ScaleY = glowScale;

            // Core breathes very subtly so it feels alive without flickering.
            _core.Opacity = 0.90 + s * 0.10;
        };
        _pulseTimer.Start();
    }

    private void StopPulseTimer()
    {
        if (_pulseTimer == null)
            return;
        _pulseTimer.Stop();
        _pulseTimer = null;
    }

    private static Color WithAlpha(Color color, byte alpha) =>
        ColorHelper.FromArgb(alpha, color.R, color.G, color.B);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}
