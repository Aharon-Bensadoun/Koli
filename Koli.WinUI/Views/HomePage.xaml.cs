using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        Unloaded += OnUnloaded;
        // HomeViewModel is a singleton; disposed on application exit in MainWindow.Cleanup.
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = App.Services.Get<HomeViewModel>();
        DataContext = _vm;
        _vm.PropertyChanged += OnViewModelChanged;
        SyncRecordButton();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.PropertyChanged -= OnViewModelChanged;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HomeViewModel.IsDictationRecording)
            or nameof(HomeViewModel.IsAssistantRecording))
        {
            DispatcherQueue.TryEnqueue(SyncRecordButton);
        }
    }

    private void SyncRecordButton()
    {
        if (_vm is null) return;
        if (_vm.IsAssistantRecording)
        {
            RecordButton.Mode = AuroraRecordMode.Assistant;
            RecordButton.IsActive = true;
        }
        else if (_vm.IsDictationRecording)
        {
            RecordButton.Mode = AuroraRecordMode.Dictation;
            RecordButton.IsActive = true;
        }
        else
        {
            RecordButton.Mode = AuroraRecordMode.Dictation;
            RecordButton.IsActive = false;
        }
    }
}
