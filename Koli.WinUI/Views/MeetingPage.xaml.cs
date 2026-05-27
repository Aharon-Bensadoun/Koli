using Microsoft.UI.Xaml.Controls;
using Koli.WinUI.ViewModels;

namespace Koli.WinUI.Views;

public sealed partial class MeetingPage : Page
{
    public MeetingPage()
    {
        InitializeComponent();
        Loaded += (_, _) => DataContext = App.Services.Get<MeetingViewModel>();
    }
}
