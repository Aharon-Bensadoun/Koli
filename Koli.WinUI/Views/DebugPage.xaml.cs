using Microsoft.UI.Xaml.Controls;
using Koli.WinUI.ViewModels;

namespace Koli.WinUI.Views;

public sealed partial class DebugPage : Page
{
    public DebugPage()
    {
        InitializeComponent();
        Loaded += (_, _) => DataContext = App.Services.Get<DebugViewModel>();
    }
}
