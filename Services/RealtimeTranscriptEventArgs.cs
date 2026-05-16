namespace Koli.Services;

public sealed class RealtimeTranscriptEventArgs : EventArgs
{
    public required string ItemId { get; init; }
    /// <summary>Full text for the utterance (accumulated on deltas; canonical on completion).</summary>
    public required string Text { get; init; }
    public required bool IsFinal { get; init; }
    /// <summary>
    /// Raw incremental text from the latest <c>delta</c> event (only the new chunk, not the accumulation).
    /// Empty/null on <c>completed</c> events. Use this when you want to type/append only the newly arrived
    /// characters and avoid duplicates.
    /// </summary>
    public string? Delta { get; init; }
}
