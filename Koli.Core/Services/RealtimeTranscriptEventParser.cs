using System.Text.Json;

namespace Koli.Services;

/// <summary>Parses OpenAI Realtime server JSON events relevant to input audio transcription.</summary>
public static class RealtimeTranscriptEventParser
{
    public const string DeltaType = "conversation.item.input_audio_transcription.delta";
    public const string CompletedType = "conversation.item.input_audio_transcription.completed";

    /// <summary>
    /// Returns true when <paramref name="json"/> is a delta or completed transcription event.
    /// </summary>
    public static bool TryParseTranscriptionEvent(string json, out RealtimeParsedTranscription parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeProp))
                return false;
            var type = typeProp.GetString();
            if (type == null)
                return false;

            var itemId = root.TryGetProperty("item_id", out var idProp)
                ? idProp.GetString() ?? ""
                : "";

            if (type == DeltaType)
            {
                var delta = root.TryGetProperty("delta", out var d) ? d.GetString() ?? "" : "";
                parsed = new RealtimeParsedTranscription(type, itemId, delta, null);
                return true;
            }

            if (type == CompletedType)
            {
                var transcript = root.TryGetProperty("transcript", out var t) ? t.GetString() ?? "" : "";
                parsed = new RealtimeParsedTranscription(type, itemId, null, transcript);
                return true;
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts a user-visible message from a generic Realtime <c>error</c> event, if present.
    /// </summary>
    public static bool TryParseErrorMessage(string json, out string message)
    {
        message = "";
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "error")
                return false;
            if (root.TryGetProperty("error", out var err))
            {
                if (err.TryGetProperty("message", out var msg))
                    message = msg.GetString() ?? "";
                else
                    message = err.ToString();
            }
            return !string.IsNullOrEmpty(message);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public readonly record struct RealtimeParsedTranscription(
    string Type,
    string ItemId,
    string? Delta,
    string? Transcript);
