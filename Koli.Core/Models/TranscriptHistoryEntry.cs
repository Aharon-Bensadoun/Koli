namespace Koli.Models;

public enum TranscriptHistoryKind
{
    Legacy,
    Dictation,
    Assistant,
    CustomAction
}

public sealed class TranscriptHistoryEntry
{
    public DateTime Timestamp { get; init; }
    public string Language { get; init; } = "";
    public TranscriptHistoryKind Kind { get; init; } = TranscriptHistoryKind.Legacy;
    public string? ProfileName { get; init; }
    public string? SourceText { get; init; }
    public string Text { get; init; } = "";
}
