namespace Koli.Models;

public sealed class TranscriptHistoryEntry
{
    public DateTime Timestamp { get; init; }
    public string Language { get; init; } = "";
    public string Text { get; init; } = "";
}
