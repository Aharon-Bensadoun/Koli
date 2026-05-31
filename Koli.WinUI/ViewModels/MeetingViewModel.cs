using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koli.Config;
using Koli.Models;
using Koli.Platform;
using Koli.Services;
using Koli.WinUI.Dialogs;
using Koli.WinUI.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Koli.WinUI.ViewModels;

public sealed partial class MeetingViewModel : ObservableObject, IDisposable
{
    private readonly AppSettings _settings;
    private readonly SecureSettingsStore _secureStore;
    private readonly DebugLogService _debugLog;
    private readonly ToastNotificationService _toast;
    private readonly DispatcherQueue _dispatcher;

    private MeetingTranscriptionService? _meetingService;
    private IAudioCaptureService? _audioCapture;
    private IAudioCaptureService? _loopbackCapture;
    private CancellationTokenSource? _cts;
    private DispatcherQueueTimer? _timer;
    private MeetingSession? _session;
    private readonly Dictionary<string, string> _speakerColors = new();

    private static readonly string[] SpeakerPalette =
        ["#7C3AED", "#34D399", "#FBBF24", "#6366F1", "#F87171", "#38BDF8", "#FB923C", "#A855F7"];

    [ObservableProperty] private string _title = $"Meeting {DateTime.Now:yyyy-MM-dd HH:mm}";
    [ObservableProperty] private int _audioSourceIndex;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isRecording;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string _timerText = "00:00:00";
    [ObservableProperty] private float _audioLevel;
    [ObservableProperty] private IList<MeetingSegmentViewModel> _segments = new List<MeetingSegmentViewModel>();
    [ObservableProperty] private IList<ParticipantViewModel> _participants = new List<ParticipantViewModel>();
    [ObservableProperty] private string _outputLanguageBadge = "";
    [ObservableProperty] private bool _showOutputLanguageBadge;

    public IReadOnlyList<string> AudioSources { get; } = ["Microphone", "System Audio", "Mic + System Audio"];

    public MeetingViewModel(
        AppSettings settings,
        SecureSettingsStore secureStore,
        DebugLogService debugLog,
        ToastNotificationService toast,
        DispatcherQueue dispatcher)
    {
        _settings = settings;
        _secureStore = secureStore;
        _debugLog = debugLog;
        _toast = toast;
        _dispatcher = dispatcher;
        RefreshOutputLanguageBadge();
    }

    private void RefreshOutputLanguageBadge()
    {
        ShowOutputLanguageBadge = TranscriptionOutputLanguageService.IsOutputLanguageSupported(_settings);
        if (!ShowOutputLanguageBadge)
        {
            OutputLanguageBadge = "";
            return;
        }

        var label = TranscriptionOutputLanguageService.GetOutputLanguageChipLabel(_settings);
        OutputLanguageBadge = label.Equals("Auto", StringComparison.OrdinalIgnoreCase)
            ? "Output: Auto"
            : $"Output: {label}";
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        var dialog = new ParticipantDialog();
        if (MainWindowHolder.Instance?.Content.XamlRoot != null)
            dialog.XamlRoot = MainWindowHolder.Instance.Content.XamlRoot;

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        var participantNames = dialog.Participants.Count > 0 ? dialog.Participants.ToArray() : null;

        try
        {
            var apiKey = await _secureStore.ResolveApiKeyAsync(_settings.AzureOpenAI.ApiKey, CancellationToken.None);
            _cts = new CancellationTokenSource();

            if (AudioSourceIndex == 2)
            {
                _audioCapture = new AudioCaptureService(_settings.Audio);
                _loopbackCapture = new SystemAudioCaptureService(_settings.Audio);
            }
            else if (AudioSourceIndex == 1)
                _audioCapture = new SystemAudioCaptureService(_settings.Audio);
            else
                _audioCapture = new AudioCaptureService(_settings.Audio);

            _audioCapture.AudioLevelChanged += OnAudioLevelChanged;
            if (_loopbackCapture != null)
                _loopbackCapture.AudioLevelChanged += OnAudioLevelChanged;

            var meetingSttSettings = OpenAiModelProfiles.CreateMeetingTranscriptionSettings(_settings.AzureOpenAI);
            var stt = new SpeechToTextService(meetingSttSettings, apiKey, _settings.Translation);
            stt.RequestLogging += (_, args) => _debugLog.LogRequest(args.Method, args.Url, args.Headers, args.Body);
            stt.ResponseLogging += (_, args) => _debugLog.LogResponse(args.StatusCode, args.StatusMessage, args.Headers, args.Body);
            stt.ErrorLogging += (_, args) => _debugLog.LogError(args.Message, args.Exception);

            var diarization = new SpeakerDiarizationService(_settings.AzureOpenAI, apiKey);
            _meetingService = new MeetingTranscriptionService(stt, diarization, _settings.Meeting);
            _meetingService.SegmentReceived += OnSegmentReceived;
            _meetingService.SegmentSpeakerUpdated += OnSegmentSpeakerUpdated;
            _meetingService.ParticipantDetected += OnParticipantDetected;
            _meetingService.ErrorLogging += (_, args) => _dispatcher.TryEnqueue(() => StatusText = $"Error: {args.Message}");

            var usesLive = OpenAiModelProfiles.WillMeetingUseLiveTranscription(_settings.AzureOpenAI);
            var usesOnPremStreaming = OpenAiModelProfiles.CanUseOnPremStreamingTranscription(meetingSttSettings);
            _debugLog.LogInfo(usesLive
                ? usesOnPremStreaming
                    ? $"Meeting started with on-prem HTTP streaming: {OpenAiModelProfiles.ResolveStreamingEndpoint(meetingSttSettings)}"
                    : $"Meeting started with live Realtime model: {meetingSttSettings.Model}"
                : $"Meeting started with chunked HTTP model: {meetingSttSettings.Model}");

            await _audioCapture.StartAsync(_cts.Token);
            IAsyncEnumerable<byte[]> stream;
            if (_loopbackCapture != null)
            {
                await _loopbackCapture.StartAsync(_cts.Token);
                stream = MergeAudioStreams(_audioCapture.GetAudioStreamAsync(_cts.Token), _loopbackCapture.GetAudioStreamAsync(_cts.Token), _cts.Token);
            }
            else
                stream = _audioCapture.GetAudioStreamAsync(_cts.Token);

            var source = AudioSourceIndex == 1 ? MeetingAudioSource.SystemAudio : MeetingAudioSource.Microphone;
            await _meetingService.StartAsync(stream, Title, source, participantNames, _cts.Token);

            IsRecording = true;
            StatusText = usesLive ? "Recording (live)..." : "Recording...";
            StartTimer();
            StartCommand.NotifyCanExecuteChanged();
            StopCommand.NotifyCanExecuteChanged();
            Participants = participantNames?.Select(n => new ParticipantViewModel(n, GetSpeakerColor(n))).ToList() ?? [];
        }
        catch (Exception ex)
        {
            _debugLog.LogError("Failed to start meeting", ex);
            _toast.ShowError("Meeting error", ex.Message);
            await StopAsync();
        }
    }

    private bool CanStart() => !IsRecording;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync()
    {
        _cts?.Cancel();
        _session = _meetingService?.Stop();

        if (_audioCapture != null)
        {
            await _audioCapture.StopAsync();
            await _audioCapture.DisposeAsync();
            _audioCapture = null;
        }

        if (_loopbackCapture != null)
        {
            await _loopbackCapture.StopAsync();
            await _loopbackCapture.DisposeAsync();
            _loopbackCapture = null;
        }

        if (_meetingService != null)
            await _meetingService.DisposeAsync();

        _meetingService = null;
        _cts?.Dispose();
        _cts = null;
        IsRecording = false;
        StatusText = "Ready";
        StopTimer();
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();

        if (_session != null && _settings.Meeting.AutoSaveTranscript)
            await AutoSaveAsync(_session);
    }

    private bool CanStop() => IsRecording;

    [RelayCommand]
    private async Task ExportAsync()
    {
        var session = _session ?? _meetingService?.CurrentSession ?? BuildSessionFromUi();
        if (session.Segments.Count == 0)
        {
            _toast.ShowWarning("Export", "No transcript to export.");
            return;
        }

        var picker = new FileSavePicker();
        var hwnd = MainWindowHolder.Instance?.WindowHandle ?? IntPtr.Zero;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("Text", [".txt"]);
        picker.FileTypeChoices.Add("Markdown", [".md"]);
        picker.FileTypeChoices.Add("JSON", [".json"]);
        picker.SuggestedFileName = SanitizeFileName(session.Title);

        var file = await picker.PickSaveFileAsync();
        if (file == null)
            return;

        var export = new TranscriptExportService();
        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
        var content = ext switch
        {
            ".md" => export.ExportToMarkdown(session),
            ".json" => export.ExportToJson(session),
            _ => export.ExportToText(session)
        };
        await FileIO.WriteTextAsync(file, content);
        _toast.ShowInfo("Export", $"Saved to {file.Name}");
    }

    private void OnSegmentReceived(object? sender, TranscriptSegment segment) =>
        _dispatcher.TryEnqueue(() =>
        {
            var list = Segments.ToList();
            list.Add(new MeetingSegmentViewModel(segment.SpeakerId, segment.Text, segment.Timestamp, GetSpeakerColor(segment.SpeakerId)));
            Segments = list;
        });

    private void OnSegmentSpeakerUpdated(object? sender, (int SegmentIndex, TranscriptSegment Updated) update) =>
        _dispatcher.TryEnqueue(() =>
        {
            var list = Segments.ToList();
            if (update.SegmentIndex < 0 || update.SegmentIndex >= list.Count)
                return;
            var old = list[update.SegmentIndex];
            list[update.SegmentIndex] = old with { SpeakerId = update.Updated.SpeakerId, ColorHex = GetSpeakerColor(update.Updated.SpeakerId) };
            Segments = list;
        });

    private void OnParticipantDetected(object? sender, Participant participant) =>
        _dispatcher.TryEnqueue(() =>
        {
            if (Participants.Any(p => p.Name == participant.DisplayName))
                return;
            Participants = Participants.Append(new ParticipantViewModel(participant.DisplayName, GetSpeakerColor(participant.Id))).ToList();
        });

    private string GetSpeakerColor(string speakerId)
    {
        if (_speakerColors.TryGetValue(speakerId, out var color))
            return color;
        color = SpeakerPalette[_speakerColors.Count % SpeakerPalette.Length];
        _speakerColors[speakerId] = color;
        return color;
    }

    private void OnAudioLevelChanged(object? sender, float level) =>
        _dispatcher.TryEnqueue(() => AudioLevel = level);

    private void StartTimer()
    {
        _timer = _dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) =>
        {
            if (_meetingService?.IsRunning == true)
            {
                var elapsed = _meetingService.Elapsed;
                TimerText = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            }
        };
        _timer.Start();
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
    }

    private async Task AutoSaveAsync(MeetingSession session)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, _settings.Meeting.TranscriptSavePath);
        Directory.CreateDirectory(folder);
        var export = new TranscriptExportService();
        var baseName = SanitizeFileName(session.Title);
        await File.WriteAllTextAsync(Path.Combine(folder, baseName + ".txt"), export.ExportToText(session));
        await File.WriteAllTextAsync(Path.Combine(folder, baseName + ".json"), export.ExportToJson(session));
    }

    private MeetingSession BuildSessionFromUi() => new()
    {
        Title = Title,
        StartedAt = DateTime.Now,
        Segments = Segments.Select(s => new TranscriptSegment { SpeakerId = s.SpeakerId, Text = s.Text, Timestamp = s.Timestamp }).ToList()
    };

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "meeting" : name;
    }

    private static async IAsyncEnumerable<byte[]> MergeAudioStreams(
        IAsyncEnumerable<byte[]> stream1,
        IAsyncEnumerable<byte[]> stream2,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer1 = System.Threading.Channels.Channel.CreateUnbounded<byte[]>();
        var buffer2 = System.Threading.Channels.Channel.CreateUnbounded<byte[]>();

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var chunk in stream1.WithCancellation(cancellationToken))
                    await buffer1.Writer.WriteAsync(chunk, cancellationToken);
            }
            finally { buffer1.Writer.TryComplete(); }
        }, cancellationToken);

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var chunk in stream2.WithCancellation(cancellationToken))
                    await buffer2.Writer.WriteAsync(chunk, cancellationToken);
            }
            finally { buffer2.Writer.TryComplete(); }
        }, cancellationToken);

        await foreach (var micChunk in buffer1.Reader.ReadAllAsync(cancellationToken))
        {
            if (buffer2.Reader.TryRead(out var loopbackChunk))
                yield return MixAudioChunks(micChunk, loopbackChunk);
            else
                yield return micChunk;
        }
    }

    private static byte[] MixAudioChunks(byte[] chunk1, byte[] chunk2)
    {
        var maxLen = Math.Max(chunk1.Length, chunk2.Length);
        var result = new byte[maxLen];
        for (var i = 0; i < maxLen - 1; i += 2)
        {
            short s1 = 0, s2 = 0;
            if (i + 1 < chunk1.Length) s1 = (short)(chunk1[i] | (chunk1[i + 1] << 8));
            if (i + 1 < chunk2.Length) s2 = (short)(chunk2[i] | (chunk2[i + 1] << 8));
            var mixed = Math.Clamp(s1 + s2, short.MinValue, short.MaxValue);
            result[i] = (byte)(mixed & 0xFF);
            result[i + 1] = (byte)((mixed >> 8) & 0xFF);
        }
        return result;
    }

    public void Dispose() => _ = StopAsync();
}

public sealed record MeetingSegmentViewModel(string SpeakerId, string Text, TimeSpan Timestamp, string ColorHex);
public sealed record ParticipantViewModel(string Name, string ColorHex);
