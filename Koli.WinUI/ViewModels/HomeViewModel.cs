using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koli.Config;
using Koli.Platform;
using Koli.Services;
using Koli.WinUI.Overlays;
using Koli.WinUI.Services;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;

namespace Koli.WinUI.ViewModels;

internal enum RecordingMode
{
    Dictation,
    Assistant
}

public sealed partial class HomeViewModel : ObservableObject, IDisposable
{
    private readonly AppSettings _settings;
    private readonly SecureSettingsStore _secureStore;
    private readonly string _configPath;
    private readonly DebugLogService _debugLog;
    private readonly HistoryService _history;
    private readonly PendingAudioStore _pendingAudio;
    private readonly TypingService _typing;
    private readonly ToastNotificationService _toast;
    private readonly CursorIndicatorService _cursorIndicator;
    private readonly TrayIconService _tray;
    private readonly InputLanguageService _inputLanguage;
    private readonly DispatcherQueue _dispatcher;
    private readonly Func<IntPtr> _getWindowHandle;

    private AudioCaptureService? _audioCapture;
    private SpeechToTextService? _speechToText;
    private SpeechToTextService? _dictationRealtimeStt;
    private Task? _dictationRealtimeTask;
    private bool _dictationUsedRealtime;
    private RecordingMode _recordingMode = RecordingMode.Dictation;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly StringBuilder _accumulatedTranscription = new();
    private DateTime _recordingStartTime = DateTime.MinValue;
    private DispatcherQueueTimer? _timerTick;

    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string _timerText = "00:00";
    [ObservableProperty] private string _languageLabel = "EN";
    [ObservableProperty] private string _inputLanguageChip = "FR";
    [ObservableProperty] private string _outputLanguageChip = "Auto";
    [ObservableProperty] private bool _isOutputLanguageAvailable = true;
    [ObservableProperty] private string _liveTranscript = "";
    [ObservableProperty] private string _transcriptTitle = "";
    [ObservableProperty] private bool _isAssistantRecording;
    [ObservableProperty] private bool _isDictationRecording;
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private bool _showTranscriptCard;
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private float _audioLevel;
    [ObservableProperty] private string _recordButtonGlyph = "\uE720";

    public HomeViewModel(
        AppSettings settings,
        SecureSettingsStore secureStore,
        IAppPaths paths,
        DebugLogService debugLog,
        HistoryService history,
        PendingAudioStore pendingAudio,
        TypingService typing,
        ToastNotificationService toast,
        CursorIndicatorService cursorIndicator,
        TrayIconService tray,
        InputLanguageService inputLanguage,
        DispatcherQueue dispatcher,
        Func<IntPtr> getWindowHandle)
    {
        _settings = settings;
        _secureStore = secureStore;
        _configPath = paths.ConfigPath;
        _debugLog = debugLog;
        _history = history;
        _pendingAudio = pendingAudio;
        _typing = typing;
        _toast = toast;
        _cursorIndicator = cursorIndicator;
        _tray = tray;
        _inputLanguage = inputLanguage;
        _dispatcher = dispatcher;
        _getWindowHandle = getWindowHandle;

        RefreshLanguageChips();
    }

    public void RefreshLanguageLabel() => LanguageLabel = _inputLanguage.GetLanguageButtonText();

    public void RefreshLanguageChips()
    {
        RefreshLanguageLabel();
        InputLanguageChip = LanguageLabel;
        IsOutputLanguageAvailable = TranscriptionOutputLanguageService.IsOutputLanguageSupported(_settings);
        OutputLanguageChip = TranscriptionOutputLanguageService.GetOutputLanguageChipLabel(_settings);
    }

    [Obsolete("Use RefreshLanguageChips")]
    public void RefreshTranslationChip() => RefreshLanguageChips();

    [RelayCommand]
    public async Task ToggleRecordingAsync()
    {
        if (IsProcessing)
            return;

        if (IsRecording && _recordingMode != RecordingMode.Dictation)
        {
            _toast.ShowInfo("Koli", "Recording already in progress.");
            return;
        }

        if (!IsRecording)
            await StartRecordingAsync(RecordingMode.Dictation);
        else
            await StopRecordingAsync();
    }

    [RelayCommand]
    public async Task ToggleAssistantRecordingAsync()
    {
        if (IsProcessing)
            return;

        if (!_settings.Assistant.Enabled)
        {
            _toast.ShowInfo("Koli", "Voice assistant is disabled in Settings.");
            return;
        }

        if (IsRecording && _recordingMode != RecordingMode.Assistant)
        {
            _toast.ShowInfo("Koli", "Recording already in progress.");
            return;
        }

        if (!IsRecording)
            await StartRecordingAsync(RecordingMode.Assistant);
        else
            await StopRecordingAsync();
    }

    [RelayCommand]
    public async Task CancelRecordingAsync()
    {
        if (!IsRecording)
            return;

        IsRecording = false;
        IsPaused = false;
        IsAssistantRecording = false;
        UpdateRecordingModeFlags();
        _timerTick?.Stop();
        _cursorIndicator.Hide();
        UpdateTrayStatus(isRecording: false);

        if (!_dictationUsedRealtime)
            _cancellationTokenSource?.Cancel();

        await Task.Delay(100);

        try
        {
            if (_audioCapture != null)
                await _audioCapture.DisposeAsync();
        }
        catch { /* ignore */ }

        _audioCapture = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        await FinalizeDictationRealtimeAsync(userCancelled: true);

        _accumulatedTranscription.Clear();
        _typing.ResetRealtimeSession();
        LiveTranscript = "";
        ShowTranscriptCard = false;
        StatusText = "Ready";
        RecordButtonGlyph = "\uE720";
        AudioLevel = 0;
        _recordingMode = RecordingMode.Dictation;
        _toast.ShowInfo("Koli", "Recording cancelled");
    }

    [RelayCommand]
    public async Task TogglePauseAsync()
    {
        if (!IsRecording || _audioCapture == null)
            return;

        try
        {
            if (IsPaused)
            {
                await _audioCapture.ResumeAsync();
                IsPaused = false;
                StatusText = "Recording";
                _toast.ShowInfo("Koli", "Recording resumed");
            }
            else
            {
                await _audioCapture.PauseAsync();
                IsPaused = true;
                StatusText = "Paused";
                AudioLevel = 0;
                _toast.ShowInfo("Koli", "Recording paused");
            }
        }
        catch (Exception ex)
        {
            _debugLog.LogError("Error toggling pause", ex);
            _toast.ShowError("Error", ex.Message);
        }
    }

    private async Task StartRecordingAsync(RecordingMode mode)
    {
        try
        {
            _recordingMode = mode;
            _typing.CaptureTargetWindow();
            SyncInputLanguageBeforeRecording();
            RefreshLanguageChips();

            StatusText = mode == RecordingMode.Assistant ? "Assistant…" : "Starting...";
            IsProcessing = true;

            string apiKey;
            try
            {
                apiKey = await _secureStore.ResolveApiKeyAsync(_settings.AzureOpenAI.ApiKey, CancellationToken.None);
            }
            catch (InvalidOperationException ex)
            {
                _toast.ShowError("Configuration Error", ex.Message);
                StatusText = "Config Error";
                IsProcessing = false;
                return;
            }

            _audioCapture = new AudioCaptureService(_settings.Audio);
            _cancellationTokenSource = new CancellationTokenSource();

            _debugLog.LogInfo($"Starting audio capture - Endpoint: {_settings.AzureOpenAI.Endpoint}, Model: {_settings.AzureOpenAI.Model}");
            await _audioCapture.StartAsync(_cancellationTokenSource.Token);

            _dictationUsedRealtime = mode == RecordingMode.Dictation
                && OpenAiModelProfiles.ShouldUseRealtimeTranscription(_settings.AzureOpenAI);
            if (_dictationUsedRealtime)
            {
                _dictationRealtimeStt = new SpeechToTextService(_settings.AzureOpenAI, apiKey, _settings.Translation);
                _dictationRealtimeStt.RealtimeTranscript += OnDictationRealtimeTranscript;
                _dictationRealtimeStt.ErrorLogging += OnErrorLogging;
                _dictationRealtimeStt.RequestLogging += OnRequestLogging;
                _dictationRealtimeStt.ResponseLogging += OnResponseLogging;

                _dictationRealtimeTask = _dictationRealtimeStt.RunRealtimeTranscriptionAsync(
                    _audioCapture.GetAudioStreamAsync(CancellationToken.None),
                    CancellationToken.None);

                _debugLog.LogInfo("OpenAI Realtime transcription session started.");
            }

            _audioCapture.AudioLevelChanged += OnAudioLevelChanged;

            _accumulatedTranscription.Clear();
            _typing.ResetRealtimeSession();
            IsRecording = true;
            IsPaused = false;
            IsProcessing = false;
            IsAssistantRecording = mode == RecordingMode.Assistant;
            UpdateRecordingModeFlags();
            StatusText = mode == RecordingMode.Assistant ? "Assistant…" : "Recording";
            RecordButtonGlyph = "\uE71A";
            ShowTranscriptCard = true;
            TranscriptTitle = mode == RecordingMode.Assistant ? "Assistant question" : "Live transcript";
            LiveTranscript = mode == RecordingMode.Assistant
                ? "Posez votre question, puis appuyez à nouveau sur Alt Gr."
                : "";

            _recordingStartTime = DateTime.UtcNow;
            UpdateTimerLabel();
            _timerTick?.Stop();
            _timerTick = _dispatcher.CreateTimer();
            _timerTick.Interval = TimeSpan.FromSeconds(1);
            _timerTick.Tick += (_, _) => UpdateTimerLabel();
            _timerTick.Start();

            _cursorIndicator.Show(IsAssistantRecording
                ? CursorIndicatorState.AssistantRecording
                : CursorIndicatorState.DictationRecording);
            UpdateTrayStatus(isRecording: true);
            _toast.ShowInfo("Koli", mode == RecordingMode.Assistant ? "Assistant recording started — press Alt Gr again to stop" : "Recording started");
        }
        catch (Exception ex)
        {
            IsProcessing = false;
            _toast.ShowError("Error", ex.Message);
            StatusText = "Error";
            await StopRecordingAsync();
        }
    }

    private async Task StopRecordingAsync()
    {
        if (!IsRecording && !IsProcessing)
            return;

        IsRecording = false;
        IsProcessing = true;
        IsAssistantRecording = false;
        UpdateRecordingModeFlags();
        _cursorIndicator.Show(CursorIndicatorState.Processing);
        UpdateTrayStatus(isRecording: false);
        _timerTick?.Stop();

        if (_audioCapture != null)
            _audioCapture.AudioLevelChanged -= OnAudioLevelChanged;

        AudioLevel = 0;

        if (!_dictationUsedRealtime)
            _cancellationTokenSource?.Cancel();

        await Task.Delay(100);

        byte[]? collectedAudio = null;
        if (_audioCapture != null)
            collectedAudio = _audioCapture.GetCollectedAudio();

        try
        {
            if (_audioCapture != null)
                await _audioCapture.DisposeAsync();
        }
        catch { /* ignore */ }

        _audioCapture = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        var isAssistant = _recordingMode == RecordingMode.Assistant;
        StatusText = isAssistant ? "Assistant…" : "Transcribing...";
        var progressMessage = isAssistant
            ? "Transcription in progress"
            : _dictationUsedRealtime ? "Finalizing realtime session..." : "Transcription in progress";
        _toast.ShowInfo("Koli", progressMessage);

        var transcriptionErrorRaised = false;
        string? lastTranscriptionError = null;
        var savedAsPending = false;

        try
        {
            string apiKey;
            try
            {
                apiKey = await _secureStore.ResolveApiKeyAsync(_settings.AzureOpenAI.ApiKey, CancellationToken.None);
            }
            catch (InvalidOperationException ex)
            {
                await FinalizeDictationRealtimeAsync(userCancelled: true);
                TrySavePendingAudio(collectedAudio, $"Configuration error: {ex.Message}", ref savedAsPending);
                _toast.ShowError("Configuration Error", ex.Message);
                StatusText = "Config Error";
                return;
            }

            if (_dictationUsedRealtime)
            {
                await FinalizeDictationRealtimeAsync(userCancelled: false);
            }
            else if (collectedAudio != null && collectedAudio.Length > 0)
            {
                var lengthBefore = _accumulatedTranscription.Length;

                EventHandler<(string Message, Exception? Exception)> failureSniffer = (_, args) =>
                {
                    if (args.Exception != null
                        || args.Message.StartsWith("Transcription API error", StringComparison.OrdinalIgnoreCase)
                        || args.Message.StartsWith("Server error", StringComparison.OrdinalIgnoreCase)
                        || args.Message.StartsWith("Error sending", StringComparison.OrdinalIgnoreCase)
                        || args.Message.StartsWith("Error reading", StringComparison.OrdinalIgnoreCase))
                    {
                        transcriptionErrorRaised = true;
                        lastTranscriptionError = args.Message;
                    }
                };

                try
                {
                    _speechToText = new SpeechToTextService(_settings.AzureOpenAI, apiKey, _settings.Translation);
                    _speechToText.TranscriptionReceived += OnTranscriptionReceived;
                    _speechToText.ErrorLogging += OnErrorLogging;
                    _speechToText.ErrorLogging += failureSniffer;
                    _speechToText.RequestLogging += OnRequestLogging;
                    _speechToText.ResponseLogging += OnResponseLogging;

                    using var transcriptionCts = new CancellationTokenSource();
                    await _speechToText.TranscribeAudioAsync(collectedAudio, transcriptionCts.Token);

                    _speechToText.TranscriptionReceived -= OnTranscriptionReceived;
                    _speechToText.ErrorLogging -= OnErrorLogging;
                    _speechToText.ErrorLogging -= failureSniffer;
                    _speechToText.RequestLogging -= OnRequestLogging;
                    _speechToText.ResponseLogging -= OnResponseLogging;
                    await _speechToText.DisposeAsync();
                    _speechToText = null;
                }
                catch (Exception ex)
                {
                    _debugLog.LogError("Error during transcription", ex);
                    transcriptionErrorRaised = true;
                    lastTranscriptionError ??= ex.Message;
                }

                if (transcriptionErrorRaised || _accumulatedTranscription.Length == lengthBefore)
                    TrySavePendingAudio(collectedAudio, lastTranscriptionError, ref savedAsPending);
            }

            if (isAssistant)
            {
                if (await ProcessAssistantStopAsync(apiKey, collectedAudio, lastTranscriptionError))
                    savedAsPending = true;
            }
            else
            {
                await ApplyTranslationAsync(apiKey, _speechToText?.OutputAlreadyApplied == true || _dictationRealtimeStt?.OutputAlreadyApplied == true);
                await ApplyRewriteAsync(apiKey);
                await DeliverDictationResultAsync();
            }

            if (!isAssistant && _accumulatedTranscription.Length == 0 && !savedAsPending)
                TrySavePendingAudio(collectedAudio, lastTranscriptionError ?? "No transcription was produced", ref savedAsPending);

            StatusText = "Ready";
        }
        catch (Exception ex)
        {
            _debugLog.LogError("Error after recording stopped", ex);
            TrySavePendingAudio(collectedAudio, ex.Message, ref savedAsPending);
            _toast.ShowError("Error", ex.Message);
            StatusText = "Ready";
        }
        finally
        {
            IsProcessing = false;
            IsAssistantRecording = false;
            UpdateRecordingModeFlags();
            RecordButtonGlyph = "\uE720";
            _recordingMode = RecordingMode.Dictation;
            _cursorIndicator.Hide();
            UpdateTrayStatus(isRecording: false);
        }
    }

    private void UpdateRecordingModeFlags() =>
        IsDictationRecording = IsRecording && !IsAssistantRecording;

    private void UpdateTrayStatus(bool isRecording)
    {
        if (!isRecording)
        {
            _tray.SetTooltip("Koli");
            return;
        }

        _tray.SetTooltip(_recordingMode == RecordingMode.Assistant
            ? "Koli — Assistant actif (Alt Gr pour arrêter)"
            : "Koli — Dictée active (F9 pour arrêter)");
    }

    private void SyncInputLanguageBeforeRecording()
    {
        if (_settings.AzureOpenAI.LanguageMode == "Manual"
            && !string.IsNullOrWhiteSpace(_settings.AzureOpenAI.ManualLanguage))
        {
            _settings.AzureOpenAI.Language = _settings.AzureOpenAI.ManualLanguage.Trim().ToLowerInvariant();
            return;
        }

        _inputLanguage.UpdateFromKeyboard();
    }

    private async Task<bool> ProcessAssistantStopAsync(
        string apiKey,
        byte[]? collectedAudio,
        string? lastTranscriptionError)
    {
        var savedAsPending = false;
        var question = _accumulatedTranscription.ToString().Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            TrySavePendingAudio(collectedAudio, lastTranscriptionError ?? "No question was transcribed", ref savedAsPending);
            if (!savedAsPending)
                _toast.ShowError("Assistant", "Could not transcribe your question.");
            return savedAsPending;
        }

        StatusText = "Assistant…";
        _toast.ShowInfo("Koli", "Searching for an answer…");

        try
        {
            var assistantService = new VoiceAssistantService(_settings.Assistant, _settings.AzureOpenAI.Endpoint, apiKey);
            assistantService.RequestLogging += OnRequestLogging;
            assistantService.ResponseLogging += OnResponseLogging;
            assistantService.ErrorLogging += OnErrorLogging;

            var answer = await assistantService.QueryAsync(question, CancellationToken.None);

            assistantService.RequestLogging -= OnRequestLogging;
            assistantService.ResponseLogging -= OnResponseLogging;
            assistantService.ErrorLogging -= OnErrorLogging;
            await assistantService.DisposeAsync();

            if (string.IsNullOrWhiteSpace(answer))
            {
                if (!VoiceAssistantService.IsSupportedEndpoint(_settings.AzureOpenAI.Endpoint))
                    _toast.ShowError("Assistant", "Voice assistant requires a public OpenAI endpoint (api.openai.com).");
                else
                    _toast.ShowError("Assistant", "Could not get an answer. Check API key and model settings.");
                LiveTranscript = $"Q: {question}";
                TranscriptTitle = "Assistant response";
                ShowTranscriptCard = true;
                return savedAsPending;
            }

            DeliverText(answer);
            var historyEntry = $"Q: {question}\nA: {answer}";
            _history.Add(historyEntry, _settings.AzureOpenAI.Language ?? "en");
            TranscriptTitle = "Assistant response";
            LiveTranscript = $"Q: {question}\n\nA: {answer}";
            ShowTranscriptCard = true;
        }
        catch (Exception ex)
        {
            _debugLog.LogError("Assistant query failed", ex);
            _toast.ShowError("Assistant", ex.Message);
        }

        return savedAsPending;
    }

    private async Task DeliverDictationResultAsync()
    {
        if (_accumulatedTranscription.Length == 0)
            return;

        var finalText = _accumulatedTranscription.ToString();
        DeliverText(finalText);

        if (_typing.RealtimeTypedAnything)
        {
            var hasOutputTransform = TranscriptionOutputLanguageService.IsOpenAiEndpoint(_settings.AzureOpenAI.Endpoint)
                ? TranscriptionOutputLanguageService.GetOutputLanguage(_settings.Translation) != null
                : _settings.Translation.Enabled && !string.IsNullOrWhiteSpace(_settings.Translation.TargetLanguage);
            if (hasOutputTransform || _settings.Rewrite.Enabled)
                _toast.ShowInfo("Realtime", "Translation/Rewrite applied to clipboard only (live typing kept original).");
        }

        _history.Add(finalText, _settings.AzureOpenAI.Language ?? "en");
        TranscriptTitle = "Last transcript";
        LiveTranscript = finalText;
        ShowTranscriptCard = true;

        await Task.CompletedTask;
    }

    private void DeliverText(string text)
    {
        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
        }
        catch (Exception ex)
        {
            _debugLog.LogError("Error copying text to clipboard", ex);
        }

        if (_settings.Typing.TypeInActiveWindow && !_typing.RealtimeTypedAnything)
            _typing.TypeText(text, _settings.Typing, _getWindowHandle(), addLeadingSpace: false);
    }

    private async Task ApplyTranslationAsync(string apiKey, bool outputAlreadyApplied)
    {
        if (outputAlreadyApplied)
            return;

        var isOpenAi = TranscriptionOutputLanguageService.IsOpenAiEndpoint(_settings.AzureOpenAI.Endpoint);
        var outputLanguage = TranscriptionOutputLanguageService.GetOutputLanguage(_settings.Translation);
        var needsOutput = isOpenAi
            ? !string.IsNullOrWhiteSpace(outputLanguage)
            : _settings.Translation.Enabled && !string.IsNullOrWhiteSpace(_settings.Translation.TargetLanguage);

        if (!needsOutput || _accumulatedTranscription.Length == 0)
            return;

        var targetLanguage = isOpenAi
            ? outputLanguage!
            : _settings.Translation.TargetLanguage;

        var isFallback = isOpenAi
            && TranscriptionOutputLanguageService.RequiresCrossLingualOutput(
                _inputLanguage.CurrentLanguageCode,
                outputLanguage);

        StatusText = "Transcribing...";
        if (!isFallback)
        {
            StatusText = "Translating...";
            _toast.ShowInfo("Koli", "Translation in progress");
        }

        try
        {
            var translationService = new TextTranslationService(_settings.Translation, _settings.AzureOpenAI.Endpoint, apiKey);
            translationService.RequestLogging += OnRequestLogging;
            translationService.ResponseLogging += OnResponseLogging;
            translationService.ErrorLogging += OnErrorLogging;

            var originalText = _accumulatedTranscription.ToString();
            var translatedText = await translationService.TranslateAsync(originalText, targetLanguage, CancellationToken.None);

            translationService.RequestLogging -= OnRequestLogging;
            translationService.ResponseLogging -= OnResponseLogging;
            translationService.ErrorLogging -= OnErrorLogging;
            await translationService.DisposeAsync();

            if (!string.IsNullOrWhiteSpace(translatedText))
            {
                _accumulatedTranscription.Clear();
                _accumulatedTranscription.Append(translatedText);
            }
            else if (!isFallback)
            {
                _toast.ShowWarning("Translation Warning", "Translation failed, keeping original transcription");
            }
        }
        catch (Exception ex)
        {
            _debugLog.LogError($"Translation failed: {ex.Message}", ex);
            if (!isFallback)
                _toast.ShowError("Translation Error", ex.Message);
        }
    }

    private async Task ApplyRewriteAsync(string apiKey)
    {
        if (!_settings.Rewrite.Enabled || _accumulatedTranscription.Length == 0)
            return;

        StatusText = "Rewriting...";
        _toast.ShowInfo("Koli", "Rewrite in progress");

        try
        {
            var rewriteService = new TextRewriteService(_settings.Rewrite, apiKey);
            rewriteService.RequestLogging += OnRequestLogging;
            rewriteService.ResponseLogging += OnResponseLogging;
            rewriteService.ErrorLogging += OnErrorLogging;

            var originalText = _accumulatedTranscription.ToString();
            var rewrittenText = await rewriteService.RewriteTextAsync(originalText, _settings.AzureOpenAI.Language, CancellationToken.None);

            rewriteService.RequestLogging -= OnRequestLogging;
            rewriteService.ResponseLogging -= OnResponseLogging;
            rewriteService.ErrorLogging -= OnErrorLogging;
            await rewriteService.DisposeAsync();

            if (!string.IsNullOrWhiteSpace(rewrittenText))
            {
                _accumulatedTranscription.Clear();
                _accumulatedTranscription.Append(rewrittenText);
                _toast.ShowInfo("Text Rewritten", "Text has been rewritten professionally");
            }
            else
            {
                _toast.ShowWarning("Rewrite Warning", "Rewrite returned empty, using original text");
            }
        }
        catch (Exception ex)
        {
            _debugLog.LogError("Error rewriting text", ex);
            _toast.ShowWarning("Rewrite Error", "Failed to rewrite, using original text");
        }
    }

    private void TrySavePendingAudio(byte[]? audio, string? errorMessage, ref bool alreadySaved)
    {
        if (alreadySaved || audio == null || audio.Length == 0)
            return;

        try
        {
            _pendingAudio.Add(audio, _settings.Audio.SampleRate, _settings.AzureOpenAI.Language ?? "", errorMessage);
            alreadySaved = true;
            _toast.ShowWarning("Transcription failed", "Recording saved to History for retry.");
        }
        catch (Exception ex)
        {
            _debugLog.LogError("Failed to save pending audio", ex);
        }
    }

    private async Task FinalizeDictationRealtimeAsync(bool userCancelled)
    {
        if (_dictationRealtimeStt == null)
            return;

        if (userCancelled)
            await _dictationRealtimeStt.StopRealtimeTranscriptionAsync();

        var wait = userCancelled ? TimeSpan.FromSeconds(8) : TimeSpan.FromSeconds(45);
        if (_dictationRealtimeTask != null)
            await Task.WhenAny(_dictationRealtimeTask, Task.Delay(wait));

        if (!userCancelled)
            await _dictationRealtimeStt.StopRealtimeTranscriptionAsync();

        _dictationRealtimeStt.RealtimeTranscript -= OnDictationRealtimeTranscript;
        _dictationRealtimeStt.ErrorLogging -= OnErrorLogging;
        _dictationRealtimeStt.RequestLogging -= OnRequestLogging;
        _dictationRealtimeStt.ResponseLogging -= OnResponseLogging;
        await _dictationRealtimeStt.DisposeAsync();
        _dictationRealtimeStt = null;
        _dictationRealtimeTask = null;
        _dictationUsedRealtime = false;
    }

    private void OnTranscriptionReceived(object? sender, string text)
    {
        _dispatcher.TryEnqueue(() =>
        {
            AppendAccumulated(text);
            LiveTranscript = _accumulatedTranscription.ToString();
        });
    }

    private void OnDictationRealtimeTranscript(object? sender, RealtimeTranscriptEventArgs e)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (!e.IsFinal)
            {
                StatusText = "Recording";
                LiveTranscript = e.Text ?? string.Empty;
                if (_settings.Typing.TypeInActiveWindow && !string.IsNullOrEmpty(e.Delta))
                    _typing.TypeRealtimeChunk(e.ItemId, e.Delta!, _getWindowHandle());
                return;
            }

            var finalText = e.Text ?? string.Empty;
            _typing.OnRealtimeItemCompleted(e.ItemId, finalText, _settings.Typing, _getWindowHandle());
            AppendAccumulated(finalText.Trim());
            LiveTranscript = _accumulatedTranscription.ToString();
        });
    }

    private void AppendAccumulated(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        if (_accumulatedTranscription.Length > 0)
            _accumulatedTranscription.Append(' ');
        _accumulatedTranscription.Append(text);
    }

    private void OnAudioLevelChanged(object? sender, float level) =>
        _dispatcher.TryEnqueue(() => AudioLevel = level);

    private void OnRequestLogging(object? sender, (string Method, string Url, Dictionary<string, string>? Headers, string? Body) args) =>
        _debugLog.LogRequest(args.Method, args.Url, args.Headers, args.Body);

    private void OnResponseLogging(object? sender, (int StatusCode, string? StatusMessage, Dictionary<string, string>? Headers, string? Body) args) =>
        _debugLog.LogResponse(args.StatusCode, args.StatusMessage, args.Headers, args.Body);

    private void OnErrorLogging(object? sender, (string Message, Exception? Exception) args)
    {
        _debugLog.LogError(args.Message, args.Exception);
        if (args.Message.StartsWith("Transcription API error", StringComparison.OrdinalIgnoreCase)
            || args.Exception != null)
        {
            _toast.ShowError("Transcription Error", args.Message);
        }
    }

    private void UpdateTimerLabel()
    {
        if (_recordingStartTime == DateTime.MinValue)
        {
            TimerText = "00:00";
            return;
        }

        var elapsed = DateTime.UtcNow - _recordingStartTime;
        TimerText = elapsed.TotalHours >= 1
            ? $"{(int)elapsed.TotalHours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}"
            : $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
    }

    [RelayCommand]
    public async Task OpenOutputLanguageSettingsAsync()
    {
        var dialog = new Dialogs.OutputLanguageSettingsDialog(
            _settings.Translation,
            _settings.AzureOpenAI.Endpoint,
            "en");
        if (MainWindowHolder.Instance?.Content.XamlRoot != null)
            dialog.XamlRoot = MainWindowHolder.Instance.Content.XamlRoot;
        if (await dialog.ShowAsync() == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
        {
            _settings.Save(_configPath);
            RefreshLanguageChips();
        }
    }

    [RelayCommand]
    public async Task OpenTranslationSettingsAsync() => await OpenOutputLanguageSettingsAsync();

    public void Dispose()
    {
        _timerTick?.Stop();
    }
}
