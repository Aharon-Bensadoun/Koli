using Microsoft.UI.Xaml.Controls;
using Koli.WinUI.ViewModels;

namespace Koli.WinUI.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => DataContext = App.Services.Get<SettingsViewModel>();
    }
}
