using Koli.Services;
using Koli.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Koli.WinUI.Views;

public sealed partial class DebugPage : Page
{
    private DebugViewModel? _vm;

    public DebugPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _vm = App.Services.Get<DebugViewModel>();
            DataContext = _vm;
            _vm.Refresh();
        };
        Unloaded += (_, _) => _vm?.Dispose();
    }

    private void AudioPlay_Click(object sender, RoutedEventArgs e)
    {
        if (_vm != null && sender is Button { Tag: DebugAudioEntry entry })
            _vm.ToggleAudioPlaybackCommand.Execute(entry);
    }

    private void AudioDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_vm != null && sender is Button { Tag: DebugAudioEntry entry })
            _vm.DeleteAudioCommand.Execute(entry);
    }

    private async void ClearAudio_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || !_vm.HasAudioEntries)
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Clear debug recordings?",
            Content = "This permanently deletes all retained debug WAV files.",
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            _vm.ClearAudioCommand.Execute(null);
    }
}
