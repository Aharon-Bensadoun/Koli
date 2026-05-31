using System.Diagnostics;
using Koli.Config;
using Koli.Models;

namespace Koli.Services;

/// <summary>
/// Orchestrates meeting transcription: buffers audio into chunks (HTTP STT) or a single OpenAI Realtime stream,
/// then runs GPT diarization in background. Text is emitted after each transcript segment; speaker labels are updated asynchronously.
/// </summary>
public sealed class MeetingTranscriptionService : IAsyncDisposable
{
    private readonly SpeechToTextService _sttService;
    private readonly SpeakerDiarizationService _diarizationService;
    private readonly MeetingSettings _meetingSettings;
    private readonly Stopwatch _meetingTimer = new();
    private readonly object _realtimeTimingLock = new();
    private TimeSpan _realtimeLastUtteranceEnd;
    private CancellationTokenSource? _cts;
    private MeetingSession? _currentSession;
    private string _lastActiveSpeaker = "Speaker 1";

    /// <summary>Fired immediately when Whisper returns text (fast path).</summary>
    public event EventHandler<TranscriptSegment>? SegmentReceived;

    /// <summary>Fired when GPT diarization updates a segment's speaker label.</summary>
    public event EventHandler<(int SegmentIndex, TranscriptSegment Updated)>? SegmentSpeakerUpdated;

    public event EventHandler<Participant>? ParticipantDetected;
    public event EventHandler<(string Message, Exception? Exception)>? ErrorLogging;

    public MeetingSession? CurrentSession => _currentSession;
    public TimeSpan Elapsed => _meetingTimer.Elapsed;
    public bool IsRunning => _meetingTimer.IsRunning;

    public MeetingTranscriptionService(
        SpeechToTextService sttService,
        SpeakerDiarizationService diarizationService,
        MeetingSettings meetingSettings)
    {
        _sttService = sttService;
        _diarizationService = diarizationService;
        _meetingSettings = meetingSettings;
    }

    public Task StartAsync(
        IAsyncEnumerable<byte[]> audioStream,
        string title,
        MeetingAudioSource audioSource,
        string[]? participantHints,
        CancellationToken cancellationToken)
    {
        _diarizationService.Reset();
        _lastActiveSpeaker = "Speaker 1";
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _currentSession = new MeetingSession
        {
            Title = title,
            StartedAt = DateTime.Now,
            AudioSource = audioSource
        };

        if (participantHints != null)
        {
            for (int i = 0; i < participantHints.Length; i++)
            {
                _currentSession.Participants.Add(new Participant
                {
                    Id = participantHints[i],
                    DisplayName = participantHints[i]
                });
            }
        }

        _meetingTimer.Restart();

        _ = Task.Run(async () =>
        {
            try
            {
                if (_sttService.UsesLiveTranscription)
                    await ProcessRealtimeMeetingStreamAsync(audioStream, participantHints, _cts.Token).ConfigureAwait(false);
                else
                    await ProcessAudioStreamAsync(audioStream, participantHints, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ErrorLogging?.Invoke(this, ($"Meeting transcription error: {ex.Message}", ex));
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    private async Task ProcessRealtimeMeetingStreamAsync(
        IAsyncEnumerable<byte[]> audioStream,
        string[]? participantHints,
        CancellationToken cancellationToken)
    {
        lock (_realtimeTimingLock)
            _realtimeLastUtteranceEnd = TimeSpan.Zero;

        void OnRealtime(object? sender, RealtimeTranscriptEventArgs e)
        {
            if (!e.IsFinal || string.IsNullOrWhiteSpace(e.Text))
                return;

            TimeSpan timestamp;
            TimeSpan duration;
            lock (_realtimeTimingLock)
            {
                var now = _meetingTimer.Elapsed;
                timestamp = _realtimeLastUtteranceEnd;
                duration = now > timestamp ? now - timestamp : TimeSpan.FromMilliseconds(500);
                _realtimeLastUtteranceEnd = now;
            }

            var text = e.Text.Trim();
            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessMeetingTranscriptTextAsync(text, timestamp, duration, participantHints, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ErrorLogging?.Invoke(this, ($"Realtime meeting segment error: {ex.Message}", ex));
                }
            }, cancellationToken);
        }

        _sttService.RealtimeTranscript += OnRealtime;
        try
        {
            await _sttService.RunRealtimeTranscriptionAsync(
                audioStream,
                cancellationToken,
                periodicBufferCommits: true,
                periodicCommitIntervalSeconds: _meetingSettings.ChunkDurationSeconds).ConfigureAwait(false);
        }
        finally
        {
            _sttService.RealtimeTranscript -= OnRealtime;
            await _sttService.StopRealtimeTranscriptionAsync().ConfigureAwait(false);
        }
    }

    private async Task ProcessAudioStreamAsync(
        IAsyncEnumerable<byte[]> audioStream,
        string[]? participantHints,
        CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        var bytesPerSecond = 16000 * 2;
        var chunkSize = bytesPerSecond * _meetingSettings.ChunkDurationSeconds;
        var chunkDuration = TimeSpan.FromSeconds(_meetingSettings.ChunkDurationSeconds);

        try
        {
            await foreach (var chunk in audioStream.WithCancellation(cancellationToken))
            {
                await buffer.WriteAsync(chunk, cancellationToken);

                if (buffer.Length >= chunkSize)
                {
                    var audioData = buffer.ToArray();
                    buffer.SetLength(0);

                    var chunkTimestamp = _meetingTimer.Elapsed - chunkDuration;
                    if (chunkTimestamp < TimeSpan.Zero)
                        chunkTimestamp = TimeSpan.Zero;

                    await ProcessMeetingChunkAsync(audioData, chunkTimestamp, chunkDuration, participantHints, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) { }

        // Process remaining audio
        if (buffer.Length > 2000)
        {
            var audioData = buffer.ToArray();
            var remainingDuration = TimeSpan.FromSeconds((double)audioData.Length / bytesPerSecond);
            var chunkTimestamp = _meetingTimer.Elapsed - remainingDuration;
            if (chunkTimestamp < TimeSpan.Zero)
                chunkTimestamp = TimeSpan.Zero;

            try
            {
                await ProcessMeetingChunkAsync(audioData, chunkTimestamp, remainingDuration, participantHints, cancellationToken);
            }
            catch (Exception ex)
            {
                ErrorLogging?.Invoke(this, ($"Error processing final meeting chunk: {ex.Message}", ex));
            }
        }
    }

    private async Task ProcessMeetingChunkAsync(
        byte[] audioData,
        TimeSpan timestamp,
        TimeSpan chunkDuration,
        string[]? participantHints,
        CancellationToken cancellationToken)
    {
        var transcribedText = await _sttService.TranscribeChunkAsync(audioData, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(transcribedText))
            return;

        await ProcessMeetingTranscriptTextAsync(transcribedText, timestamp, chunkDuration, participantHints, cancellationToken)
            .ConfigureAwait(false);
    }

    private Task ProcessMeetingTranscriptTextAsync(
        string transcribedText,
        TimeSpan timestamp,
        TimeSpan chunkDuration,
        string[]? participantHints,
        CancellationToken cancellationToken)
    {
        var immediateSegment = new TranscriptSegment
        {
            SpeakerId = _lastActiveSpeaker,
            Text = transcribedText,
            Timestamp = timestamp,
            Duration = chunkDuration
        };

        int segmentIndex;
        if (_currentSession != null)
        {
            segmentIndex = _currentSession.Segments.Count;
            _currentSession.Segments.Add(immediateSegment);
        }
        else
        {
            segmentIndex = 0;
        }

        SegmentReceived?.Invoke(this, immediateSegment);

        var capturedIndex = segmentIndex;
        var capturedTimestamp = timestamp;
        var capturedDuration = chunkDuration;
        _ = Task.Run(async () =>
        {
            try
            {
                var segments = await _diarizationService.IdentifySpeakersAsync(
                    transcribedText, capturedTimestamp, capturedDuration, participantHints, cancellationToken)
                    .ConfigureAwait(false);

                if (segments.Count > 0)
                {
                    _lastActiveSpeaker = segments[^1].SpeakerId;

                    var firstSpeaker = segments[0].SpeakerId;
                    if (firstSpeaker != immediateSegment.SpeakerId || segments.Count > 1)
                    {
                        if (_currentSession != null && capturedIndex < _currentSession.Segments.Count)
                        {
                            _currentSession.Segments[capturedIndex] = segments[0];
                            SegmentSpeakerUpdated?.Invoke(this, (capturedIndex, segments[0]));

                            for (int i = 1; i < segments.Count; i++)
                            {
                                _currentSession.Segments.Add(segments[i]);
                                TrackParticipant(segments[i].SpeakerId);
                                SegmentReceived?.Invoke(this, segments[i]);
                            }
                        }

                        TrackParticipant(firstSpeaker);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging?.Invoke(this, ($"Background diarization error: {ex.Message}", ex));
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    private void TrackParticipant(string speakerId)
    {
        if (_currentSession == null) return;
        if (_currentSession.Participants.Any(p => p.Id == speakerId)) return;

        var newParticipant = new Participant
        {
            Id = speakerId,
            DisplayName = speakerId
        };
        _currentSession.Participants.Add(newParticipant);
        ParticipantDetected?.Invoke(this, newParticipant);
    }

    public MeetingSession? Stop()
    {
        _meetingTimer.Stop();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_currentSession != null)
            _currentSession.EndedAt = DateTime.Now;

        return _currentSession;
    }

    public void RenameParticipant(string speakerId, string newName)
    {
        if (_currentSession == null) return;
        var participant = _currentSession.Participants.FirstOrDefault(p => p.Id == speakerId);
        if (participant != null)
            participant.DisplayName = newName;
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        _diarizationService.Dispose();
        await Task.CompletedTask;
    }
}
