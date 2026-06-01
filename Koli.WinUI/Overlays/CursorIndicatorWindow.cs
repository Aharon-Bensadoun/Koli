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
    private const int HaloSize = 44;
    private const int RingSize = 24;
    private const int CoreSize = 14;
    private const int WindowSize = 56;
    private const int CursorOffset = 14;

    private const int GwlExstyle = -20;
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly Ellipse _halo;
    private readonly Ellipse _ring;
    private readonly Ellipse _core;
    private readonly ScaleTransform _haloScale;
    private readonly DispatcherQueue _dispatcher;
    private DispatcherQueueTimer? _followTimer;
    private DispatcherQueueTimer? _pulseTimer;
    private CursorIndicatorState _state = CursorIndicatorState.Hidden;
    private double _pulsePhase;
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

        _haloScale = new ScaleTransform { ScaleX = 1, ScaleY = 1, CenterX = HaloSize / 2.0, CenterY = HaloSize / 2.0 };

        _halo = new Ellipse
        {
            Width = HaloSize,
            Height = HaloSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.0,
            RenderTransformOrigin = new global::Windows.Foundation.Point(0.5, 0.5),
            RenderTransform = _haloScale,
        };

        _ring = new Ellipse
        {
            Width = RingSize,
            Height = RingSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            StrokeThickness = 1.5,
        };

        _core = new Ellipse
        {
            Width = CoreSize,
            Height = CoreSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Content = new Grid
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(0, 0, 0, 0)),
            Children = { _halo, _ring, _core }
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
        _pulsePhase = 0;

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
        // Pull aurora brushes from app resources so the orb stays in sync with the theme.
        var resources = Application.Current.Resources;

        Brush halo = state switch
        {
            CursorIndicatorState.AssistantRecording => SafeBrush(resources, "AssistantHaloBrush",
                ColorHelper.FromArgb(0xCC, 0x22, 0xD3, 0xEE)),
            CursorIndicatorState.Processing => SafeBrush(resources, "AuroraHaloBrush",
                ColorHelper.FromArgb(0xCC, 0x7C, 0x3A, 0xED)),
            _ => SafeBrush(resources, "RecordingHaloBrush",
                ColorHelper.FromArgb(0xCC, 0xFF, 0x4D, 0x6A)),
        };

        Brush core = state switch
        {
            CursorIndicatorState.AssistantRecording => new SolidColorBrush(ColorHelper.FromArgb(255, 0x22, 0xD3, 0xEE)),
            CursorIndicatorState.Processing => new SolidColorBrush(ColorHelper.FromArgb(255, 0xA7, 0x8B, 0xFA)),
            _ => new SolidColorBrush(ColorHelper.FromArgb(255, 0xFF, 0x4D, 0x6A)),
        };

        Brush ring = state switch
        {
            CursorIndicatorState.AssistantRecording => new SolidColorBrush(ColorHelper.FromArgb(0xCC, 0xA7, 0x8B, 0xFA)),
            CursorIndicatorState.Processing => SafeBrush(resources, "AuroraRibbonBrush",
                ColorHelper.FromArgb(0xCC, 0x7C, 0x3A, 0xED)),
            _ => new SolidColorBrush(ColorHelper.FromArgb(0xCC, 0xFF, 0x7A, 0x93)),
        };

        _halo.Fill = halo;
        _core.Fill = core;
        _ring.Stroke = ring;
    }

    private static Brush SafeBrush(Microsoft.UI.Xaml.ResourceDictionary resources, string key, Color fallback)
    {
        if (resources.TryGetValue(key, out var value) && value is Brush brush)
            return brush;
        return new SolidColorBrush(fallback);
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
        _pulseTimer.Interval = TimeSpan.FromMilliseconds(40);
        _pulseTimer.Tick += (_, _) =>
        {
            if (_state == CursorIndicatorState.Hidden)
                return;

            _pulsePhase += _state == CursorIndicatorState.Processing ? 0.18 : 0.12;
            double sin01 = (Math.Sin(_pulsePhase) + 1) * 0.5; // 0..1

            // Halo opacity 0.20 → 0.65, halo scale 0.92 → 1.10
            _halo.Opacity = 0.20 + sin01 * 0.45;
            double scale = 0.92 + sin01 * 0.18;
            _haloScale.ScaleX = scale;
            _haloScale.ScaleY = scale;

            // Core gentle opacity 0.85 → 1.0
            _core.Opacity = 0.85 + sin01 * 0.15;
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
