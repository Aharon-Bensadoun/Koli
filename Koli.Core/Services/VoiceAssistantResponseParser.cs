using System.Text.Json;
using System.Text.RegularExpressions;

namespace Koli.Services;

public static class VoiceAssistantResponseParser
{
    private static readonly Regex UrlPattern = new(
        @"https?://[^\s\)\]\>]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MarkdownLinkPattern = new(
        @"\[[^\]]*\]\([^\)]*\)",
        RegexOptions.Compiled);

    private static readonly Regex CitationPattern = new(
        @"\[\d+\]|\u3010[^\u3011]*\u3011",
        RegexOptions.Compiled);

    public static string? ParseOutputText(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
                return null;

            string? lastMessageText = null;
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("type", out var typeElement))
                    continue;

                if (!string.Equals(typeElement.GetString(), "message", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                    continue;

                var text = ExtractMessageText(content);
                if (!string.IsNullOrWhiteSpace(text))
                    lastMessageText = text;
            }

            return CleanResponseText(lastMessageText);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? CleanResponseText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var cleaned = text.Trim();
        cleaned = MarkdownLinkPattern.Replace(cleaned, string.Empty);
        cleaned = UrlPattern.Replace(cleaned, string.Empty);
        cleaned = CitationPattern.Replace(cleaned, string.Empty);
        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();

        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static string? ExtractMessageText(JsonElement content)
    {
        string? lastText = null;
        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var blockType)
                && string.Equals(blockType.GetString(), "output_text", StringComparison.OrdinalIgnoreCase)
                && block.TryGetProperty("text", out var textElement))
            {
                var value = textElement.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    lastText = value;
            }
            else if (block.TryGetProperty("text", out var fallbackText))
            {
                var value = fallbackText.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    lastText = value;
            }
        }

        return lastText;
    }
}
