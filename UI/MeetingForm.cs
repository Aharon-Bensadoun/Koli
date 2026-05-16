using System.Text.Json;
using Koli.Config;
using Koli.Models;
using Koli.Services;

namespace Koli.UI;

/// <summary>
/// Meeting mode window for live multi-speaker transcription.
/// </summary>
internal sealed class MeetingForm : Form
{
    // Services
    private readonly AppSettings _settings;
    private readonly string _apiKey;
    private MeetingTranscriptionService? _meetingService;
    private IAudioCaptureService? _audioCaptureService;
    private IAudioCaptureService? _loopbackCaptureService; // for combined mode

    // UI Controls
    private readonly RichTextBox _transcriptBox;
    private readonly Panel _participantPanel;
    private readonly ComboBox _audioSourceCombo;
    private readonly Button _startButton;
    private readonly Button _stopButton;
    private readonly Button _exportButton;
    private readonly TextBox _titleInput;
    private readonly Label _timerLabel;
    private readonly Label _statusLabel;
    private readonly Label _audioLevelLabel;
    private readonly Panel _toolbarPanel;
    private readonly Panel _statusPanel;

    // State
    private bool _isRecording;
    private CancellationTokenSource? _cts;
    private readonly System.Windows.Forms.Timer _timerTick;
    private readonly Dictionary<string, Color> _speakerColors = new();
    private readonly Dictionary<string, TextBox> _participantNameBoxes = new();
    private string[]? _participantHints;

    // Segment tracking for background speaker updates
    private readonly List<(int startPos, int length, string speakerId)> _segmentPositions = new();

    // Speaker colors palette
    private static readonly Color[] SpeakerPalette = new[]
    {
        Color.FromArgb(124, 58, 237),   // Violet
        Color.FromArgb(52, 211, 153),   // Emerald
        Color.FromArgb(251, 191, 36),   // Amber
        Color.FromArgb(99, 102, 241),   // Indigo
        Color.FromArgb(248, 113, 113),  // Red
        Color.FromArgb(56, 189, 248),   // Sky
        Color.FromArgb(251, 146, 60),   // Orange
        Color.FromArgb(168, 85, 247),   // Purple
    };

    public MeetingForm(AppSettings settings, string apiKey)
    {
        _settings = settings;
        _apiKey = apiKey;

        // Form setup
        Text = "Meeting Transcription";
        Size = new Size(950, 650);
        MinimumSize = new Size(750, 500);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = FluentColors.Background;
        ForeColor = FluentColors.TextPrimary;
        Font = FluentFonts.Body;

        // Toolbar panel
        _toolbarPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 55,
            BackColor = FluentColors.Surface,
            Padding = new Padding(10, 8, 10, 8)
        };

        _titleInput = new TextBox
        {
            Text = $"Meeting {DateTime.Now:yyyy-MM-dd HH:mm}",
            Location = new Point(10, 14),
            Size = new Size(200, 28),
            Font = FluentFonts.Subtitle,
            BackColor = FluentColors.SurfaceElevated,
            ForeColor = FluentColors.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };

        var sourceLabel = new Label
        {
            Text = "Source:",
            Location = new Point(225, 18),
            Size = new Size(50, 20),
            Font = FluentFonts.Caption,
            ForeColor = FluentColors.TextSecondary
        };

        _audioSourceCombo = new ComboBox
        {
            Location = new Point(275, 14),
            Size = new Size(150, 28),
            Font = FluentFonts.Caption,
            BackColor = FluentColors.SurfaceElevated,
            ForeColor = FluentColors.TextPrimary,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat
        };
        _audioSourceCombo.Items.AddRange(new object[] { "Microphone", "System Audio", "Mic + System Audio" });
        _audioSourceCombo.SelectedIndex = _settings.Meeting.DefaultAudioSource == "SystemAudio" ? 1 : 0;

        _startButton = CreateToolbarButton("\u25B6 Start", FluentColors.AccentPrimary, 440);
        _startButton.Click += StartButton_Click;

        _stopButton = CreateToolbarButton("\u25A0 Stop", FluentColors.RecordingPrimary, 540);
        _stopButton.Enabled = false;
        _stopButton.Click += StopButton_Click;

        _exportButton = CreateToolbarButton("Export", FluentColors.SurfaceHover, 640);
        _exportButton.Enabled = false;
        _exportButton.Click += ExportButton_Click;

        _toolbarPanel.Controls.AddRange(new Control[] { _titleInput, sourceLabel, _audioSourceCombo, _startButton, _stopButton, _exportButton });

        // Status panel
        _statusPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 35,
            BackColor = FluentColors.Surface,
            Padding = new Padding(10, 5, 10, 5)
        };

        _statusLabel = new Label
        {
            Text = "\u2022 Ready",
            Location = new Point(10, 8),
            Size = new Size(300, 20),
            Font = FluentFonts.StatusBadge,
            ForeColor = FluentColors.TextSecondary
        };

        _audioLevelLabel = new Label
        {
            Text = "",
            Location = new Point(320, 8),
            Size = new Size(200, 20),
            Font = FluentFonts.CaptionSmall,
            ForeColor = FluentColors.TextTertiary
        };

        _timerLabel = new Label
        {
            Text = "00:00:00",
            Dock = DockStyle.Right,
            Size = new Size(80, 20),
            Font = FluentFonts.StatusBadge,
            ForeColor = FluentColors.TextSecondary,
            TextAlign = ContentAlignment.MiddleRight
        };

        _statusPanel.Controls.AddRange(new Control[] { _statusLabel, _audioLevelLabel, _timerLabel });

        // Participant panel (right sidebar)
        _participantPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 200,
            BackColor = FluentColors.Surface,
            Padding = new Padding(10),
            AutoScroll = true
        };

        var participantHeader = new Label
        {
            Text = "Participants",
            Dock = DockStyle.Top,
            Height = 30,
            Font = FluentFonts.Subtitle,
            ForeColor = FluentColors.TextPrimary,
            Padding = new Padding(0, 5, 0, 5)
        };
        _participantPanel.Controls.Add(participantHeader);

        // Splitter between transcript and participant panel
        var splitter = new Splitter
        {
            Dock = DockStyle.Right,
            Width = 3,
            BackColor = FluentColors.Border
        };

        // Transcript panel (main area)
        _transcriptBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = FluentColors.Background,
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Body,
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            WordWrap = true
        };

        // Add controls in order (reverse dock order)
        Controls.Add(_transcriptBox);
        Controls.Add(splitter);
        Controls.Add(_participantPanel);
        Controls.Add(_statusPanel);
        Controls.Add(_toolbarPanel);

        // Timer for elapsed time display
        _timerTick = new System.Windows.Forms.Timer { Interval = 1000 };
        _timerTick.Tick += (s, e) =>
        {
            if (_meetingService != null && _meetingService.IsRunning)
            {
                var elapsed = _meetingService.Elapsed;
                _timerLabel.Text = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            }
        };

        FormClosing += MeetingForm_FormClosing;
    }

    private static Button CreateToolbarButton(string text, Color backColor, int x)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, 10),
            Size = new Size(90, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.ButtonText,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private async void StartButton_Click(object? sender, EventArgs e)
    {
        if (_isRecording) return;

        // Show participant dialog
        using var dialog = new ParticipantDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _participantHints = dialog.Participants.Count > 0 ? dialog.Participants.ToArray() : null;

        var sourceIndex = _audioSourceCombo.SelectedIndex;
        var audioSource = sourceIndex == 1 ? MeetingAudioSource.SystemAudio : MeetingAudioSource.Microphone;

        _cts = new CancellationTokenSource();

        // Create audio capture service(s)
        if (sourceIndex == 2)
        {
            // Combined: Mic + System Audio
            _audioCaptureService = new AudioCaptureService(_settings.Audio);
            _loopbackCaptureService = new SystemAudioCaptureService(_settings.Audio);
        }
        else if (sourceIndex == 1)
        {
            _audioCaptureService = new SystemAudioCaptureService(_settings.Audio);
        }
        else
        {
            _audioCaptureService = new AudioCaptureService(_settings.Audio);
        }

        // Audio level display
        _audioCaptureService.AudioLevelChanged += OnAudioLevelChanged;
        if (_loopbackCaptureService != null)
            _loopbackCaptureService.AudioLevelChanged += OnAudioLevelChanged;

        // Create meeting transcription service
        var sttService = new SpeechToTextService(_settings.AzureOpenAI, _apiKey);
        var diarizationService = new SpeakerDiarizationService(_settings.AzureOpenAI, _apiKey);

        _meetingService = new MeetingTranscriptionService(sttService, diarizationService, _settings.Meeting);
        _meetingService.SegmentReceived += OnSegmentReceived;
        _meetingService.SegmentSpeakerUpdated += OnSegmentSpeakerUpdated;
        _meetingService.ParticipantDetected += OnParticipantDetected;
        _meetingService.ErrorLogging += (s, args) =>
        {
            if (InvokeRequired)
                BeginInvoke(() => UpdateStatus($"Error: {args.Message}", FluentColors.Error));
            else
                UpdateStatus($"Error: {args.Message}", FluentColors.Error);
        };

        try
        {
            await _audioCaptureService.StartAsync(_cts.Token);

            IAsyncEnumerable<byte[]> audioStream;

            if (_loopbackCaptureService != null)
            {
                // Combined mode: merge mic + loopback streams
                await _loopbackCaptureService.StartAsync(_cts.Token);
                audioStream = MergeAudioStreams(
                    _audioCaptureService.GetAudioStreamAsync(_cts.Token),
                    _loopbackCaptureService.GetAudioStreamAsync(_cts.Token),
                    _cts.Token);
            }
            else
            {
                audioStream = _audioCaptureService.GetAudioStreamAsync(_cts.Token);
            }

            await _meetingService.StartAsync(
                audioStream,
                _titleInput.Text,
                audioSource,
                _participantHints,
                _cts.Token);

            _isRecording = true;
            _timerTick.Start();
            UpdateUIForRecording(true);
            UpdateStatus("\u25CF Recording...", FluentColors.RecordingPrimary);

            if (_participantHints != null)
            {
                foreach (var name in _participantHints)
                    AddParticipantToSidebar(name, name);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to start meeting: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Merges two audio streams by mixing samples (16-bit PCM addition with clipping).
    /// This allows capturing both microphone (user) and system audio (remote participants).
    /// </summary>
    private static async IAsyncEnumerable<byte[]> MergeAudioStreams(
        IAsyncEnumerable<byte[]> stream1,
        IAsyncEnumerable<byte[]> stream2,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer1 = System.Threading.Channels.Channel.CreateUnbounded<byte[]>();
        var buffer2 = System.Threading.Channels.Channel.CreateUnbounded<byte[]>();

        // Pump both streams into channels
        var pump1 = Task.Run(async () =>
        {
            try
            {
                await foreach (var chunk in stream1.WithCancellation(cancellationToken))
                    await buffer1.Writer.WriteAsync(chunk, cancellationToken);
            }
            finally { buffer1.Writer.TryComplete(); }
        }, cancellationToken);

        var pump2 = Task.Run(async () =>
        {
            try
            {
                await foreach (var chunk in stream2.WithCancellation(cancellationToken))
                    await buffer2.Writer.WriteAsync(chunk, cancellationToken);
            }
            finally { buffer2.Writer.TryComplete(); }
        }, cancellationToken);

        // Read from primary stream (mic) and mix with whatever is available from loopback
        await foreach (var micChunk in buffer1.Reader.ReadAllAsync(cancellationToken))
        {
            if (buffer2.Reader.TryRead(out var loopbackChunk))
            {
                // Mix: add samples and clip
                var mixed = MixAudioChunks(micChunk, loopbackChunk);
                yield return mixed;
            }
            else
            {
                yield return micChunk;
            }
        }
    }

    private static byte[] MixAudioChunks(byte[] chunk1, byte[] chunk2)
    {
        var maxLen = Math.Max(chunk1.Length, chunk2.Length);
        var result = new byte[maxLen];

        for (int i = 0; i < maxLen - 1; i += 2)
        {
            short s1 = 0, s2 = 0;
            if (i + 1 < chunk1.Length)
                s1 = (short)(chunk1[i] | (chunk1[i + 1] << 8));
            if (i + 1 < chunk2.Length)
                s2 = (short)(chunk2[i] | (chunk2[i + 1] << 8));

            // Mix with clipping
            int mixed = s1 + s2;
            mixed = Math.Clamp(mixed, short.MinValue, short.MaxValue);

            result[i] = (byte)(mixed & 0xFF);
            result[i + 1] = (byte)((mixed >> 8) & 0xFF);
        }

        return result;
    }

    private void OnAudioLevelChanged(object? sender, float level)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => UpdateAudioLevel(level));
            return;
        }
        UpdateAudioLevel(level);
    }

    private void UpdateAudioLevel(float level)
    {
        var bars = (int)(level * 20);
        var barStr = new string('\u2588', bars).PadRight(20, '\u2591');
        _audioLevelLabel.Text = $"Audio: {barStr}";
        _audioLevelLabel.ForeColor = level > 0.05f ? FluentColors.Success : FluentColors.TextTertiary;
    }

    private async void StopButton_Click(object? sender, EventArgs e)
    {
        if (!_isRecording) return;

        _timerTick.Stop();
        _cts?.Cancel();
        var session = _meetingService?.Stop();

        if (_audioCaptureService != null)
        {
            await _audioCaptureService.StopAsync();
            await _audioCaptureService.DisposeAsync();
            _audioCaptureService = null;
        }

        if (_loopbackCaptureService != null)
        {
            await _loopbackCaptureService.StopAsync();
            await _loopbackCaptureService.DisposeAsync();
            _loopbackCaptureService = null;
        }

        _isRecording = false;
        UpdateUIForRecording(false);
        UpdateStatus("\u2022 Meeting ended", FluentColors.Success);
        _audioLevelLabel.Text = "";

        // Apply participant renames to session
        if (session != null)
        {
            foreach (var kvp in _participantNameBoxes)
            {
                var newName = kvp.Value.Text.Trim();
                if (!string.IsNullOrEmpty(newName) && newName != kvp.Key)
                    _meetingService?.RenameParticipant(kvp.Key, newName);
            }

            if (_settings.Meeting.AutoSaveTranscript)
                AutoSaveTranscript(session);
        }
    }

    private void ExportButton_Click(object? sender, EventArgs e)
    {
        var session = _meetingService?.CurrentSession;
        if (session == null || session.Segments.Count == 0)
        {
            MessageBox.Show(this, "No transcript to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Export Transcript",
            Filter = "Text file (*.txt)|*.txt|Markdown (*.md)|*.md|JSON (*.json)|*.json",
            FileName = $"{session.Title.Replace(" ", "_")}_{session.StartedAt:yyyyMMdd_HHmm}"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var exportService = new TranscriptExportService();
            var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            var content = ext switch
            {
                ".md" => exportService.ExportToMarkdown(session),
                ".json" => exportService.ExportToJson(session),
                _ => exportService.ExportToText(session)
            };

            File.WriteAllText(dialog.FileName, content);
            UpdateStatus($"Exported to {Path.GetFileName(dialog.FileName)}", FluentColors.Success);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnSegmentReceived(object? sender, TranscriptSegment segment)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendSegmentToTranscript(segment));
            return;
        }
        AppendSegmentToTranscript(segment);
    }

    private void OnSegmentSpeakerUpdated(object? sender, (int SegmentIndex, TranscriptSegment Updated) update)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => UpdateSegmentSpeaker(update.SegmentIndex, update.Updated));
            return;
        }
        UpdateSegmentSpeaker(update.SegmentIndex, update.Updated);
    }

    private void AppendSegmentToTranscript(TranscriptSegment segment)
    {
        var color = GetSpeakerColor(segment.SpeakerId);
        var displayName = GetDisplayName(segment.SpeakerId);
        var timeStr = $"[{segment.Timestamp.Hours:D2}:{segment.Timestamp.Minutes:D2}:{segment.Timestamp.Seconds:D2}]";

        // Track start position for this segment's speaker label
        int speakerLabelStart = _transcriptBox.TextLength;

        // Timestamp
        _transcriptBox.SelectionStart = _transcriptBox.TextLength;
        _transcriptBox.SelectionColor = FluentColors.TextTertiary;
        _transcriptBox.SelectionFont = FluentFonts.Caption;
        _transcriptBox.AppendText($"{timeStr} ");

        // Speaker name — track position for later updates
        int speakerNameStart = _transcriptBox.TextLength;
        _transcriptBox.SelectionStart = _transcriptBox.TextLength;
        _transcriptBox.SelectionColor = color;
        _transcriptBox.SelectionFont = FluentFonts.Subtitle;
        _transcriptBox.AppendText($"{displayName}\n");
        int speakerNameEnd = _transcriptBox.TextLength;

        // Text
        _transcriptBox.SelectionStart = _transcriptBox.TextLength;
        _transcriptBox.SelectionColor = FluentColors.TextPrimary;
        _transcriptBox.SelectionFont = FluentFonts.Body;
        _transcriptBox.AppendText($"{segment.Text}\n\n");

        // Track segment position for background speaker updates
        _segmentPositions.Add((speakerNameStart, speakerNameEnd - speakerNameStart, segment.SpeakerId));

        // Ensure speaker is in sidebar
        AddParticipantToSidebar(segment.SpeakerId, displayName);

        // Auto-scroll
        _transcriptBox.SelectionStart = _transcriptBox.TextLength;
        _transcriptBox.ScrollToCaret();
    }

    private void UpdateSegmentSpeaker(int segmentIndex, TranscriptSegment updated)
    {
        if (segmentIndex < 0 || segmentIndex >= _segmentPositions.Count)
            return;

        var (startPos, length, oldSpeakerId) = _segmentPositions[segmentIndex];
        if (oldSpeakerId == updated.SpeakerId)
            return;

        // Update the speaker name in the RichTextBox
        var newDisplayName = GetDisplayName(updated.SpeakerId);
        var newColor = GetSpeakerColor(updated.SpeakerId);

        try
        {
            _transcriptBox.Select(startPos, length);
            _transcriptBox.SelectionColor = newColor;
            _transcriptBox.SelectedText = $"{newDisplayName}\n";

            // Update tracked position
            var newLength = newDisplayName.Length + 1;
            var lengthDiff = newLength - length;
            _segmentPositions[segmentIndex] = (startPos, newLength, updated.SpeakerId);

            // Adjust subsequent segment positions
            for (int i = segmentIndex + 1; i < _segmentPositions.Count; i++)
            {
                var (pos, len, sid) = _segmentPositions[i];
                _segmentPositions[i] = (pos + lengthDiff, len, sid);
            }
        }
        catch
        {
            // RichTextBox position manipulation can fail; swallow gracefully
        }

        AddParticipantToSidebar(updated.SpeakerId, newDisplayName);

        // Restore caret at end
        _transcriptBox.SelectionStart = _transcriptBox.TextLength;
        _transcriptBox.SelectionLength = 0;
    }

    private void OnParticipantDetected(object? sender, Participant participant)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AddParticipantToSidebar(participant.Id, participant.DisplayName));
            return;
        }
        AddParticipantToSidebar(participant.Id, participant.DisplayName);
    }

    private void AddParticipantToSidebar(string speakerId, string displayName)
    {
        if (_participantNameBoxes.ContainsKey(speakerId))
            return;

        var color = GetSpeakerColor(speakerId);

        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            Padding = new Padding(0, 5, 0, 5)
        };

        var colorIndicator = new Panel
        {
            Location = new Point(0, 10),
            Size = new Size(4, 30),
            BackColor = color
        };

        var label = new Label
        {
            Text = speakerId,
            Location = new Point(10, 8),
            Size = new Size(170, 16),
            Font = FluentFonts.CaptionSmall,
            ForeColor = FluentColors.TextTertiary
        };

        var nameBox = new TextBox
        {
            Text = displayName,
            Location = new Point(10, 25),
            Size = new Size(170, 22),
            Font = FluentFonts.Caption,
            BackColor = FluentColors.SurfaceElevated,
            ForeColor = FluentColors.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };

        _participantNameBoxes[speakerId] = nameBox;

        panel.Controls.AddRange(new Control[] { colorIndicator, label, nameBox });
        _participantPanel.Controls.Add(panel);
        panel.BringToFront();
    }

    private string GetDisplayName(string speakerId)
    {
        if (_participantNameBoxes.TryGetValue(speakerId, out var nameBox))
        {
            var name = nameBox.Text.Trim();
            if (!string.IsNullOrEmpty(name))
                return name;
        }

        var participant = _meetingService?.CurrentSession?.Participants.FirstOrDefault(p => p.Id == speakerId);
        return participant?.DisplayName ?? speakerId;
    }

    private Color GetSpeakerColor(string speakerId)
    {
        if (_speakerColors.TryGetValue(speakerId, out var color))
            return color;

        var index = _speakerColors.Count % SpeakerPalette.Length;
        color = SpeakerPalette[index];
        _speakerColors[speakerId] = color;
        return color;
    }

    private void UpdateUIForRecording(bool recording)
    {
        _startButton.Enabled = !recording;
        _stopButton.Enabled = recording;
        _exportButton.Enabled = !recording && (_meetingService?.CurrentSession?.Segments.Count > 0);
        _audioSourceCombo.Enabled = !recording;
        _titleInput.ReadOnly = recording;
    }

    private void UpdateStatus(string text, Color color)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = color;
    }

    private void AutoSaveTranscript(MeetingSession session)
    {
        try
        {
            var savePath = Path.Combine(AppContext.BaseDirectory, _settings.Meeting.TranscriptSavePath);
            Directory.CreateDirectory(savePath);

            var fileName = $"{session.StartedAt:yyyyMMdd_HHmm}_{session.Title.Replace(" ", "_")}.json";
            foreach (var c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');

            var filePath = Path.Combine(savePath, fileName);
            var exportService = new TranscriptExportService();
            File.WriteAllText(filePath, exportService.ExportToJson(session));

            UpdateStatus($"Auto-saved to {fileName}", FluentColors.Success);
        }
        catch (Exception ex)
        {
            UpdateStatus($"Auto-save failed: {ex.Message}", FluentColors.Error);
        }
    }

    private async void MeetingForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_isRecording)
        {
            var result = MessageBox.Show(this,
                "A meeting is still recording. Stop and close?",
                "Meeting in progress",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.No)
            {
                e.Cancel = true;
                return;
            }

            StopButton_Click(sender, e);
        }

        if (_meetingService != null)
        {
            await _meetingService.DisposeAsync();
            _meetingService = null;
        }

        _timerTick.Dispose();
    }
}
