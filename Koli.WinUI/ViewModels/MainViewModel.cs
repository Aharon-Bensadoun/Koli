using CommunityToolkit.Mvvm.ComponentModel;

namespace Koli.WinUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusText = "Ready";
}
