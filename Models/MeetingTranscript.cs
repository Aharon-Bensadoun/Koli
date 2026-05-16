using System.Text.Json.Serialization;

namespace Koli.Models;

public enum MeetingAudioSource
{
    Microphone,
    SystemAudio
}

public sealed class MeetingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public MeetingAudioSource AudioSource { get; set; } = MeetingAudioSource.Microphone;
    public List<Participant> Participants { get; set; } = new();
    public List<TranscriptSegment> Segments { get; set; } = new();
}

public sealed class Participant
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    [JsonIgnore]
    public Color? Color { get; set; }

    /// <summary>Hex color string for JSON serialization (e.g. "#7C3AED").</summary>
    public string? ColorHex
    {
        get => Color.HasValue ? $"#{Color.Value.R:X2}{Color.Value.G:X2}{Color.Value.B:X2}" : null;
        set
        {
            if (!string.IsNullOrEmpty(value))
                Color = ColorTranslator.FromHtml(value);
        }
    }
}

public sealed class TranscriptSegment
{
    public string SpeakerId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public TimeSpan Timestamp { get; set; }
    public TimeSpan Duration { get; set; }
}
