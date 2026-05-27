using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koli.Config;
using Koli.Models;
using Koli.Platform;
using Koli.Services;
using Koli.WinUI.Services;
using Microsoft.UI.Dispatching;
using NAudio.Wave;

namespace Koli.WinUI.ViewModels;

public sealed partial class HistoryViewModel : ObservableObject, IDisposable
{
    private readonly AppSettings _settings;
    private readonly SecureSettingsStore _secureStore;
    private readonly HistoryService _history;
    private readonly PendingAudioStore _pendingAudio;
    private readonly AudioPlaybackService _audioPlayback;
    private readonly DebugLogService _debugLog;
    private readonly ToastNotificationService _toast;
    private readonly DispatcherQueue _dispatcher;
    private bool _isRetrying;

    [ObservableProperty] private IReadOnlyList<TranscriptHistoryEntry> _entries = Array.Empty<TranscriptHistoryEntry>();
    [ObservableProperty] private IReadOnlyList<PendingAudioEntry> _pendingEntries = Array.Empty<PendingAudioEntry>();
    [ObservableProperty] private Guid? _currentlyPlayingId;

    public HistoryViewModel(
        AppSettings settings,
        SecureSettingsStore secureStore,
        HistoryService history,
        PendingAudioStore pendingAudio,
        AudioPlaybackService audioPlayback,
        DebugLogService debugLog,
        ToastNotificationService toast,
        DispatcherQueue dispatcher)
    {
        _settings = settings;
        _secureStore = secureStore;
        _history = history;
        _pendingAudio = pendingAudio;
        _audioPlayback = audioPlayback;
        _debugLog = debugLog;
        _toast = toast;
        _dispatcher = dispatcher;

        _history.HistoryChanged += (_, _) => Refresh();
        _audioPlayback.PlaybackEnded += OnPlaybackEnded;
        Refresh();
    }

    public void Refresh()
    {
        Entries = _history.Entries.ToList();
        PendingEntries = _pendingAudio.GetAll().OrderByDescending(e => e.CapturedAt).ToList();
    }

    [RelayCommand]
    private void CopyTranscript(TranscriptHistoryEntry entry)
    {
        try
        {
            var dataPackage = new global::Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(entry.Text);
            global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            _toast.ShowInfo("Copied", "Transcript copied to clipboard");
        }
        catch (Exception ex)
        {
            _toast.ShowError("Copy failed", ex.Message);
        }
    }

    [RelayCommand]
    private void TogglePendingPlayback(PendingAudioEntry entry)
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
                _pendingAudio.Remove(entry.Id);
                Refresh();
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
    private void DeletePending(PendingAudioEntry entry)
    {
        try
        {
            if (CurrentlyPlayingId == entry.Id)
            {
                _audioPlayback.Stop();
                CurrentlyPlayingId = null;
            }
            _pendingAudio.Remove(entry.Id);
            Refresh();
        }
        catch (Exception ex)
        {
            _toast.ShowError("Delete failed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task RetryPendingAsync(PendingAudioEntry entry)
    {
        if (_isRetrying)
        {
            _toast.ShowInfo("Retry in progress", "Please wait for the current retry to finish.");
            return;
        }

        if (!File.Exists(entry.FilePath))
        {
            _toast.ShowWarning("Recording missing", "The audio file was deleted from disk.");
            _pendingAudio.Remove(entry.Id);
            Refresh();
            return;
        }

        if (CurrentlyPlayingId == entry.Id)
        {
            _audioPlayback.Stop();
            CurrentlyPlayingId = null;
        }

        _isRetrying = true;
        _toast.ShowInfo("Koli", "Retrying transcription");

        SpeechToTextService? retryService = null;
        string? collected = null;
        string? lastError = null;
        var errorRaised = false;

        try
        {
            string apiKey;
            try
            {
                apiKey = await _secureStore.ResolveApiKeyAsync(_settings.AzureOpenAI.ApiKey, CancellationToken.None);
            }
            catch (InvalidOperationException ex)
            {
                _pendingAudio.UpdateLastError(entry.Id, ex.Message);
                _toast.ShowError("Configuration error", ex.Message);
                Refresh();
                return;
            }

            byte[] pcm;
            try
            {
                using var reader = new WaveFileReader(entry.FilePath);
                using var ms = new MemoryStream();
                reader.CopyTo(ms);
                pcm = ms.ToArray();
            }
            catch (Exception ex)
            {
                _pendingAudio.UpdateLastError(entry.Id, ex.Message);
                _toast.ShowError("Cannot read WAV", ex.Message);
                Refresh();
                return;
            }

            retryService = new SpeechToTextService(_settings.AzureOpenAI, apiKey, _settings.Translation);

            EventHandler<string> capture = (_, text) =>
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                collected = collected == null ? text.Trim() : collected + " " + text.Trim();
            };
            EventHandler<(string Message, Exception? Exception)> errorCapture = (_, args) =>
            {
                errorRaised = true;
                lastError = args.Message;
            };

            retryService.TranscriptionReceived += capture;
            retryService.ErrorLogging += errorCapture;
            retryService.RequestLogging += (_, args) => _debugLog.LogRequest(args.Method, args.Url, args.Headers, args.Body);
            retryService.ResponseLogging += (_, args) => _debugLog.LogResponse(args.StatusCode, args.StatusMessage, args.Headers, args.Body);

            using var cts = new CancellationTokenSource();
            await retryService.TranscribeAudioAsync(pcm, cts.Token);

            retryService.TranscriptionReceived -= capture;
            retryService.ErrorLogging -= errorCapture;
            await retryService.DisposeAsync();
        }
        catch (Exception ex)
        {
            errorRaised = true;
            lastError = ex.Message;
            _debugLog.LogError("Retry transcription failed", ex);
        }
        finally
        {
            if (retryService != null)
            {
                try { await retryService.DisposeAsync(); }
                catch { /* ignore */ }
            }
            _isRetrying = false;
        }

        if (!errorRaised && !string.IsNullOrWhiteSpace(collected))
        {
            _history.Add(collected!, _settings.AzureOpenAI.Language ?? "en");
            _pendingAudio.Remove(entry.Id);
            Refresh();
            _toast.ShowInfo("Transcription succeeded", "Audio has been transcribed and moved to history.");
        }
        else
        {
            var msg = lastError ?? "No transcription was produced.";
            _pendingAudio.UpdateLastError(entry.Id, msg);
            Refresh();
            _toast.ShowError("Retry failed", msg);
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
