using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koli.Config;
using Koli.Platform;
using Koli.WinUI.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Koli.WinUI.ViewModels;

public sealed partial class DebugViewModel : ObservableObject
{
    private readonly DebugLogService _debugLog;
    private readonly ToastNotificationService _toast;
    private readonly DispatcherQueue _dispatcher;

    [ObservableProperty] private string _logText = "";

    public DebugViewModel(DebugLogService debugLog, ToastNotificationService toast, DispatcherQueue dispatcher)
    {
        _debugLog = debugLog;
        _toast = toast;
        _dispatcher = dispatcher;
        Refresh();
        _debugLog.LogChanged += (_, _) =>
            _dispatcher.TryEnqueue(() => LogText = _debugLog.FullText);
    }

    private void Refresh() => LogText = _debugLog.FullText;

    [RelayCommand]
    private void Clear() => _debugLog.Clear();

    [RelayCommand]
    private void Copy()
    {
        if (string.IsNullOrEmpty(LogText))
            return;
        var package = new DataPackage();
        package.SetText(LogText);
        Clipboard.SetContent(package);
        _toast.ShowInfo("Debug", "Log copied to clipboard");
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var picker = new FileSavePicker();
        var hwnd = MainWindowHolder.Instance?.WindowHandle ?? IntPtr.Zero;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("Text", [".txt"]);
        picker.SuggestedFileName = $"Koli_Debug_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var file = await picker.PickSaveFileAsync();
        if (file == null) return;
        await FileIO.WriteTextAsync(file, LogText);
        _toast.ShowInfo("Debug", "Logs exported");
    }
}
