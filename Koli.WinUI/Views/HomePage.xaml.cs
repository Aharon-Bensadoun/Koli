using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Koli.WinUI.Controls;
using Koli.WinUI.ViewModels;

namespace Koli.WinUI.Views;

public sealed partial class HomePage : Page
{
    private HomeViewModel? _vm;

    public HomePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        // HomeViewModel is a singleton; disposed on application exit in MainWindow.Cleanup.
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = App.Services.Get<HomeViewModel>();
        DataContext = _vm;
    }
}
