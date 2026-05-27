using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Koli.Config;
using Koli.Models;

namespace Koli.Services;

/// <summary>
/// Uses GPT-4o to identify speaker changes in transcribed meeting text.
/// Maintains rolling context across chunks for speaker consistency.
/// </summary>
public sealed class SpeakerDiarizationService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly List<TranscriptSegment> _recentSegments = new();
    private string _lastActiveSpeaker = "Speaker 1";

    public event EventHandler<(string Message, Exception? Exception)>? ErrorLogging;

    public SpeakerDiarizationService(AzureOpenAISettings settings, string apiKey)
    {
        _apiKey = apiKey;
        _model = "gpt-4o";
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>
    /// Analyzes transcribed text and identifies speaker segments.
    /// </summary>
    /// <param name="transcribedText">Raw transcription from Whisper.</param>
    /// <param name="timestamp">Offset from meeting start.</param>
    /// <param name="chunkDuration">Duration of the audio chunk.</param>
    /// <param name="participantHints">Pre-registered participant names (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of transcript segments with speaker labels.</returns>
    public async Task<List<TranscriptSegment>> IdentifySpeakersAsync(
        string transcribedText,
        TimeSpan timestamp,
        TimeSpan chunkDuration,
        string[]? participantHints,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transcribedText))
            return new List<TranscriptSegment>();

        try
        {
            var systemPrompt = BuildSystemPrompt(participantHints);
            var userPrompt = BuildUserPrompt(transcribedText);

            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.1,
                max_tokens = 2000,
                response_format = new { type = "json_object" }
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                ErrorLogging?.Invoke(this, ($"Diarization GPT error: {(int)response.StatusCode}", null));
                return FallbackSingleSpeaker(transcribedText, timestamp, chunkDuration);
            }

            var segments = ParseGptResponse(responseBody, timestamp, chunkDuration);

            // Update rolling context
            foreach (var seg in segments)
            {
                _recentSegments.Add(seg);
                _lastActiveSpeaker = seg.SpeakerId;
            }

            // Keep only last 4 segments for context
            while (_recentSegments.Count > 4)
                _recentSegments.RemoveAt(0);

            return segments;
        }
        catch (Exception ex)
        {
            ErrorLogging?.Invoke(this, ($"Diarization error: {ex.Message}", ex));
            return FallbackSingleSpeaker(transcribedText, timestamp, chunkDuration);
        }
    }

    private string BuildSystemPrompt(string[]? participantHints)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a meeting transcription analyst. Your task is to identify speaker changes in transcribed text.");
        sb.AppendLine("Return ONLY valid JSON with a \"segments\" array. Each segment has \"speaker\" and \"text\" fields.");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Assign speaker labels as \"Speaker 1\", \"Speaker 2\", etc.");
        sb.AppendLine("- Only create a new speaker when there is clear evidence of a speaker change (turn-taking cues, greetings, topic shifts, conversational markers like 'thank you', 'I agree', addressing someone).");
        sb.AppendLine("- If no speaker change is detected, assign the entire text to the last active speaker.");
        sb.AppendLine("- Maintain consistency with previous context provided.");
        sb.AppendLine("- Do not split text that belongs to the same speaker into multiple segments.");

        if (participantHints != null && participantHints.Length > 0)
        {
            sb.AppendLine();
            sb.Append("Known participants: ");
            sb.AppendLine(string.Join(", ", participantHints));
            sb.AppendLine("If you can identify a participant by name from the conversation context, use their name instead of \"Speaker N\".");
        }

        sb.AppendLine();
        sb.AppendLine("Response format: {\"segments\": [{\"speaker\": \"Speaker 1\", \"text\": \"...\"}]}");

        return sb.ToString();
    }

    private string BuildUserPrompt(string transcribedText)
    {
        var sb = new StringBuilder();

        if (_recentSegments.Count > 0)
        {
            sb.AppendLine("Previous context:");
            foreach (var seg in _recentSegments)
            {
                sb.AppendLine($"[{seg.SpeakerId}]: {seg.Text}");
            }
            sb.AppendLine();
            sb.AppendLine($"Last active speaker: {_lastActiveSpeaker}");
            sb.AppendLine();
        }

        sb.AppendLine("New transcription to analyze:");
        sb.AppendLine(transcribedText);

        return sb.ToString();
    }

    private List<TranscriptSegment> ParseGptResponse(string responseBody, TimeSpan timestamp, TimeSpan chunkDuration)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
                return new List<TranscriptSegment>();

            using var resultDoc = JsonDocument.Parse(content);
            var segmentsArray = resultDoc.RootElement.GetProperty("segments");

            var segments = new List<TranscriptSegment>();
            int segmentCount = segmentsArray.GetArrayLength();
            var segmentDuration = segmentCount > 0 ? TimeSpan.FromTicks(chunkDuration.Ticks / segmentCount) : chunkDuration;

            int index = 0;
            foreach (var seg in segmentsArray.EnumerateArray())
            {
                var speaker = seg.GetProperty("speaker").GetString() ?? _lastActiveSpeaker;
                var text = seg.GetProperty("text").GetString() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(text))
                {
                    segments.Add(new TranscriptSegment
                    {
                        SpeakerId = speaker,
                        Text = text.Trim(),
                        Timestamp = timestamp + TimeSpan.FromTicks(segmentDuration.Ticks * index),
                        Duration = segmentDuration
                    });
                }
                index++;
            }

            return segments.Count > 0 ? segments : FallbackSingleSpeaker(string.Join(" ", segments.Select(s => s.Text)), timestamp, chunkDuration);
        }
        catch (Exception ex)
        {
            ErrorLogging?.Invoke(this, ($"Error parsing GPT diarization response: {ex.Message}", ex));
            return new List<TranscriptSegment>();
        }
    }

    private List<TranscriptSegment> FallbackSingleSpeaker(string text, TimeSpan timestamp, TimeSpan duration)
    {
        return new List<TranscriptSegment>
        {
            new TranscriptSegment
            {
                SpeakerId = _lastActiveSpeaker,
                Text = text.Trim(),
                Timestamp = timestamp,
                Duration = duration
            }
        };
    }

    /// <summary>Resets the rolling context (call when starting a new meeting).</summary>
    public void Reset()
    {
        _recentSegments.Clear();
        _lastActiveSpeaker = "Speaker 1";
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
