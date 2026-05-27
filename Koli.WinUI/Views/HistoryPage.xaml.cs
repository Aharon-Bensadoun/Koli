using Koli.Models;
using Koli.Services;
using Koli.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Koli.WinUI.Views;

public sealed partial class HistoryPage : Page
{
    private HistoryViewModel? _vm;

    public HistoryPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _vm = App.Services.Get<HistoryViewModel>();
            DataContext = _vm;
            _vm.Refresh();
        };
        Unloaded += (_, _) => _vm?.Dispose();
    }

    private void CopyTranscript_Click(object sender, RoutedEventArgs e)
    {
        if (_vm != null && sender is Button { Tag: TranscriptHistoryEntry entry })
            _vm.CopyTranscriptCommand.Execute(entry);
    }

    private async void PendingPlay_Click(object sender, RoutedEventArgs e)
    {
        if (_vm != null && sender is Button { Tag: PendingAudioEntry entry })
            _vm.TogglePendingPlaybackCommand.Execute(entry);
        await Task.CompletedTask;
    }

    private async void PendingRetry_Click(object sender, RoutedEventArgs e)
    {
        if (_vm != null && sender is Button { Tag: PendingAudioEntry entry })
            await _vm.RetryPendingCommand.ExecuteAsync(entry);
    }

    private void PendingDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_vm != null && sender is Button { Tag: PendingAudioEntry entry })
            _vm.DeletePendingCommand.Execute(entry);
    }
}
