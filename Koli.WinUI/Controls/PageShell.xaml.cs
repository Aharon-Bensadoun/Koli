using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace Koli.WinUI.Controls;

[ContentProperty(Name = nameof(PageContent))]
public sealed partial class PageShell : UserControl
{
    public PageShell()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(PageShell),
            new PropertyMetadata(string.Empty));

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(PageShell),
            new PropertyMetadata(string.Empty, OnSubtitleChanged));

    public Visibility HasSubtitle
    {
        get => (Visibility)GetValue(HasSubtitleProperty);
        private set => SetValue(HasSubtitleProperty, value);
    }

    public static readonly DependencyProperty HasSubtitleProperty =
        DependencyProperty.Register(nameof(HasSubtitle), typeof(Visibility), typeof(PageShell),
            new PropertyMetadata(Visibility.Collapsed));

    private static void OnSubtitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PageShell shell)
            shell.HasSubtitle = string.IsNullOrWhiteSpace(e.NewValue as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object? HeaderActions
    {
        get => GetValue(HeaderActionsProperty);
        set => SetValue(HeaderActionsProperty, value);
    }

    public static readonly DependencyProperty HeaderActionsProperty =
        DependencyProperty.Register(nameof(HeaderActions), typeof(object), typeof(PageShell),
            new PropertyMetadata(null));

    public object? PageContent
    {
        get => GetValue(PageContentProperty);
        set => SetValue(PageContentProperty, value);
    }

    public static readonly DependencyProperty PageContentProperty =
        DependencyProperty.Register(nameof(PageContent), typeof(object), typeof(PageShell),
            new PropertyMetadata(null));
}
