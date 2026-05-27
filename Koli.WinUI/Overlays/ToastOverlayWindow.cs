using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.Storage.Streams;
using WinUIEx;

namespace Koli.WinUI.Overlays;

public sealed class ToastOverlayWindow : WindowEx
{
    private const int ToastWidth = 340;
    private const int ToastMinHeight = 96;
    private const int ToastMaxHeight = 380;
    private const int HorizontalMargin = 16;
    private const int VerticalMargin = 16;

    private readonly TextBlock _titleBlock;
    private readonly TextBlock _messageBlock;
    private readonly Image _iconImage;
    private readonly Border _root;
    private readonly DispatcherQueue _dispatcher;
    private DispatcherQueueTimer? _closeTimer;
    private DispatcherQueueTimer? _fadeTimer;
    private double _opacity;
    private bool _fadingOut;
    private bool _chromeConfigured;

    public ToastOverlayWindow()
    {
        Title = "";
        Width = ToastWidth;
        Height = ToastMinHeight;
        IsAlwaysOnTop = true;
        IsShownInSwitchers = false;
        IsTitleBarVisible = false;
        IsMaximizable = false;
        IsMinimizable = false;
        IsResizable = false;

        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _iconImage = new Image
        {
            Width = 36,
            Height = 36,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _titleBlock = new TextBlock
        {
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 248, 250, 252)),
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        _messageBlock = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 163, 163, 178)),
            TextWrapping = TextWrapping.WrapWholeWords,
            MaxWidth = 248
        };

        var accentBar = new Border
        {
            Width = 3,
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 124, 58, 237)),
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, 12, 0, 12)
        };

        var textPanel = new StackPanel
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _titleBlock, _messageBlock }
        };

        var contentGrid = new Grid
        {
            Padding = new Thickness(12, 14, 16, 14),
            ColumnSpacing = 12,
            MinWidth = ToastWidth
        };
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(accentBar, 0);
        Grid.SetColumn(_iconImage, 1);
        Grid.SetColumn(textPanel, 2);
        contentGrid.Children.Add(accentBar);
        contentGrid.Children.Add(_iconImage);
        contentGrid.Children.Add(textPanel);

        _root = new Border
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 22, 22, 30)),
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 42, 42, 56)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Child = contentGrid,
            Opacity = 0
        };

        Content = _root;
        Activated += OnActivated;
        Closed += (_, _) => StopTimers();
    }

    public void ShowToast(string title, string message, int displayDurationMs = 3000)
    {
        StopTimers();
        _fadingOut = false;

        _titleBlock.Text = title;
        _messageBlock.Text = message;
        _ = LoadIconAsync();

        var messageHeight = EstimateMessageHeight(message);
        var height = Math.Clamp(56 + messageHeight + 16, ToastMinHeight, ToastMaxHeight);
        Height = height;

        ConfigureChrome();
        PositionTopRight();

        _opacity = 0;
        _root.Opacity = 0;

        AppWindow.Show();
        Activate();

        StartFadeIn();
        ScheduleClose(Math.Max(1000, displayDurationMs));
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args) => ConfigureChrome();

    private void ConfigureChrome()
    {
        if (_chromeConfigured)
            return;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.SetBorderAndTitleBar(false, false);

        _chromeConfigured = true;
    }

    private void PositionTopRight()
    {
        var displayArea = DisplayArea.Primary;
        if (displayArea == null)
            return;

        var work = displayArea.WorkArea;
        var x = work.X + work.Width - (int)Width - HorizontalMargin;
        var y = work.Y + VerticalMargin;
        AppWindow.Move(new PointInt32(x, y));
    }

    private static double EstimateMessageHeight(string message)
    {
        if (string.IsNullOrEmpty(message))
            return 20;

        var lineCount = Math.Max(1, (int)Math.Ceiling(message.Length / 38.0) + message.Count(c => c == '\n'));
        return Math.Clamp(lineCount * 18, 20, ToastMaxHeight - 72);
    }

    private async Task LoadIconAsync()
    {
        var pngPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Koli.png");
        var icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Koli.ico");
        var path = File.Exists(pngPath) ? pngPath : icoPath;
        if (!File.Exists(path))
        {
            _iconImage.Source = null;
            return;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var randomAccessStream = stream.AsRandomAccessStream();
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(randomAccessStream);
            _iconImage.Source = bitmap;
        }
        catch
        {
            _iconImage.Source = null;
        }
    }

    private void StartFadeIn()
    {
        StopFadeTimer();
        _fadeTimer = _dispatcher.CreateTimer();
        _fadeTimer.Interval = TimeSpan.FromMilliseconds(16);
        _fadeTimer.Tick += (_, _) =>
        {
            if (_fadingOut)
                return;

            _opacity += 0.12;
            if (_opacity >= 0.96)
            {
                _opacity = 0.96;
                _fadeTimer?.Stop();
            }

            _root.Opacity = _opacity;
        };
        _fadeTimer.Start();
    }

    private void StartFadeOut()
    {
        StopCloseTimer();
        _fadingOut = true;
        StopFadeTimer();
        _fadeTimer = _dispatcher.CreateTimer();
        _fadeTimer.Interval = TimeSpan.FromMilliseconds(16);
        _fadeTimer.Tick += (_, _) =>
        {
            _opacity -= 0.08;
            if (_opacity <= 0)
            {
                _fadeTimer?.Stop();
                AppWindow.Hide();
                return;
            }

            _root.Opacity = _opacity;
        };
        _fadeTimer.Start();
    }

    private void ScheduleClose(int displayDurationMs)
    {
        StopCloseTimer();
        _closeTimer = _dispatcher.CreateTimer();
        _closeTimer.Interval = TimeSpan.FromMilliseconds(displayDurationMs);
        _closeTimer.Tick += (_, _) => StartFadeOut();
        _closeTimer.IsRepeating = false;
        _closeTimer.Start();
    }

    private void StopTimers()
    {
        StopCloseTimer();
        StopFadeTimer();
    }

    private void StopCloseTimer()
    {
        if (_closeTimer == null)
            return;
        _closeTimer.Stop();
        _closeTimer = null;
    }

    private void StopFadeTimer()
    {
        if (_fadeTimer == null)
            return;
        _fadeTimer.Stop();
        _fadeTimer = null;
    }
}
