using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
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
    private const int CircleSize = 20;
    private const int WindowSize = 28;
    private const int CursorOffset = 14;

    private const int GwlExstyle = -20;
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly Ellipse _circle;
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

        _circle = new Ellipse
        {
            Width = CircleSize,
            Height = CircleSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        Content = new Grid
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(0, 0, 0, 0)),
            Children = { _circle }
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
        _circle.Fill = new SolidColorBrush(GetBaseColor(state));
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
        _pulseTimer.Interval = TimeSpan.FromMilliseconds(50);
        _pulseTimer.Tick += (_, _) =>
        {
            if (_state == CursorIndicatorState.Hidden)
                return;

            _pulsePhase += _state == CursorIndicatorState.Processing ? 0.22 : 0.14;
            var alpha = 0.55 + (Math.Sin(_pulsePhase) + 1) * 0.225;
            var color = GetBaseColor(_state);
            _circle.Fill = new SolidColorBrush(ColorHelper.FromArgb(
                (byte)(alpha * 255),
                color.R,
                color.G,
                color.B));
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

    private static global::Windows.UI.Color GetBaseColor(CursorIndicatorState state) =>
        state switch
        {
            CursorIndicatorState.AssistantRecording => ColorHelper.FromArgb(255, 124, 58, 237),
            CursorIndicatorState.Processing => ColorHelper.FromArgb(255, 167, 139, 250),
            _ => ColorHelper.FromArgb(255, 239, 68, 68)
        };

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
