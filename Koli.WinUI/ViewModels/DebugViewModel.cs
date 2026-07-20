using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koli.Config;
using Koli.Platform;
using Koli.Services;
using Koli.WinUI.Services;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Koli.WinUI.ViewModels;

public sealed partial class DebugViewModel : ObservableObject, IDisposable
{
    private readonly AppSettings _settings;
    private readonly DebugLogService _debugLog;
    private readonly DebugAudioStore _debugAudio;
    private readonly AudioPlaybackService _audioPlayback;
    private readonly ToastNotificationService _toast;
    private readonly DispatcherQueue _dispatcher;

    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private IReadOnlyList<DebugAudioEntry> _audioEntries = Array.Empty<DebugAudioEntry>();
    [ObservableProperty] private Guid? _currentlyPlayingId;
    [ObservableProperty] private bool _keepDebugAudioEnabled;
    [ObservableProperty] private bool _hasAudioEntries;

    public DebugViewModel(
        AppSettings settings,
        DebugLogService debugLog,
        DebugAudioStore debugAudio,
        AudioPlaybackService audioPlayback,
        ToastNotificationService toast,
        DispatcherQueue dispatcher)
    {
        _settings = settings;
        _debugLog = debugLog;
        _debugAudio = debugAudio;
        _audioPlayback = audioPlayback;
        _toast = toast;
        _dispatcher = dispatcher;

        RefreshLog();
        RefreshAudio();
        _debugLog.LogChanged += (_, _) =>
            _dispatcher.TryEnqueue(() => LogText = _debugLog.FullText);
        _audioPlayback.PlaybackEnded += OnPlaybackEnded;
    }

    public void Refresh()
    {
        RefreshLog();
        RefreshAudio();
    }

    private void RefreshLog() => LogText = _debugLog.FullText;

    private void RefreshAudio()
    {
        KeepDebugAudioEnabled = _settings.Audio.KeepDebugAudio;
        AudioEntries = _debugAudio.GetAll();
        HasAudioEntries = AudioEntries.Count > 0;
    }

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

    [RelayCommand]
    private void ToggleAudioPlayback(DebugAudioEntry entry)
    {
        try
        {
            if (CurrentlyPlayingId == entry.Id && _audioPlayback.IsPlaying)
            {
                _audioPlayback.Stop();
                CurrentlyPlayingId = null;
                return;
            }

            if (!File.Exists(entry.FilePath))
            {
                _toast.ShowWarning("Recording missing", "The audio file was deleted from disk.");
                _debugAudio.Remove(entry.Id);
                RefreshAudio();
                return;
            }

            _audioPlayback.Play(entry.FilePath);
            CurrentlyPlayingId = entry.Id;
        }
        catch (Exception ex)
        {
            _toast.ShowError("Playback error", ex.Message);
        }
    }

    [RelayCommand]
    private void DeleteAudio(DebugAudioEntry entry)
    {
        try
        {
            if (CurrentlyPlayingId == entry.Id)
            {
                _audioPlayback.Stop();
                CurrentlyPlayingId = null;
            }

            _debugAudio.Remove(entry.Id);
            RefreshAudio();
        }
        catch (Exception ex)
        {
            _toast.ShowError("Delete failed", ex.Message);
        }
    }

    [RelayCommand]
    private void ClearAudio()
    {
        try
        {
            if (CurrentlyPlayingId != null)
            {
                _audioPlayback.Stop();
                CurrentlyPlayingId = null;
            }

            _debugAudio.Clear();
            RefreshAudio();
            _toast.ShowInfo("Debug", "Debug recordings cleared");
        }
        catch (Exception ex)
        {
            _toast.ShowError("Clear failed", ex.Message);
        }
    }

    private void OnPlaybackEnded(object? sender, EventArgs e) =>
        _dispatcher.TryEnqueue(() =>
        {
            CurrentlyPlayingId = null;
            OnPropertyChanged(nameof(CurrentlyPlayingId));
        });

    public void Dispose()
    {
        _audioPlayback.PlaybackEnded -= OnPlaybackEnded;
    }
}
