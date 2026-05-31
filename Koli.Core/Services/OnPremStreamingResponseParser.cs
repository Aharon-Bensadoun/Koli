using System.Text;
using System.Text.Json;

namespace Koli.Services;

/// <summary>
/// Parses on-prem <c>queryAudio</c> streaming responses.
/// API assumptions (not validated with server team):
/// - Response may be SSE (<c>text/event-stream</c>, lines prefixed with <c>data: </c>) or NDJSON (one JSON object per line).
/// - JSON objects follow the batch <c>PersonalApiTranscriptionResponse</c> shape with optional partial/final flags:
///   <c>Success</c>, <c>Content</c>, and <c>IsFinal</c> / <c>final</c> / <c>isFinal</c>.
/// </summary>
public static class OnPremStreamingResponseParser
{
    public static bool TryParseJsonPayload(string json, out OnPremStreamingTranscriptEvent parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("Success", out var successProp) && successProp.ValueKind == JsonValueKind.False)
            {
                var error = root.TryGetProperty("ErrorMessage", out var err) ? err.GetString() : null;
                parsed = new OnPremStreamingTranscriptEvent(false, null, false, error);
                return true;
            }

            var content = ReadString(root, "Content", "content", "text", "Transcript", "transcript");
            if (string.IsNullOrWhiteSpace(content))
                return false;

            var isFinal = ReadBool(root, "IsFinal", "isFinal", "final", "IsComplete", "isComplete") ?? true;
            var delta = ReadString(root, "Delta", "delta");

            parsed = new OnPremStreamingTranscriptEvent(true, content, isFinal, null, delta);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Reads an SSE or NDJSON response stream and yields transcript events.</summary>
    public static async IAsyncEnumerable<OnPremStreamingTranscriptEvent> ReadStreamAsync(
        Stream stream,
        string? contentType,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var isSse = contentType?.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase) == true;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        if (isSse)
        {
            var dataBuilder = new StringBuilder();
            while (!reader.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null)
                    break;

                if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = line.Length > 5 ? line[5..].TrimStart() : "";
                    if (payload.Length == 0)
                        continue;

                    if (payload.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
                        yield break;

                    dataBuilder.Append(payload);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    if (dataBuilder.Length == 0)
                        continue;

                    var json = dataBuilder.ToString();
                    dataBuilder.Clear();
                    if (TryParseJsonPayload(json, out var parsed))
                        yield return parsed;
                    continue;
                }

                // Some servers send bare JSON lines even with event-stream content type.
                if (TryParseJsonPayload(line, out var inline))
                    yield return inline;
            }

            if (dataBuilder.Length > 0 && TryParseJsonPayload(dataBuilder.ToString(), out var trailing))
                yield return trailing;
        }
        else
        {
            while (!reader.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (TryParseJsonPayload(line, out var parsed))
                    yield return parsed;
            }
        }
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
        }
        return null;
    }

    private static bool? ReadBool(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
                continue;
            if (prop.ValueKind == JsonValueKind.True)
                return true;
            if (prop.ValueKind == JsonValueKind.False)
                return false;
        }
        return null;
    }
}

public readonly record struct OnPremStreamingTranscriptEvent(
    bool Success,
    string? Content,
    bool IsFinal,
    string? ErrorMessage,
    string? Delta = null);
