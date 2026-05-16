using Koli.Config;

namespace Koli.Services;

/// <summary>
/// Centralizes detection of OpenAI cloud vs on-premise endpoints and Realtime transcription models.
/// </summary>
public static class OpenAiModelProfiles
{
    /// <summary>Hadassah-style or other non-OpenAI HTTP endpoints.</summary>
    public static bool IsOnPremiseStyleEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return false;
        return !endpoint.Contains("openai.com", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when the app should call OpenAI's cloud HTTP APIs (transcriptions, chat).
    /// Empty endpoint defaults to api.openai.com.
    /// </summary>
    public static bool IsOpenAiCloudHttp(AzureOpenAISettings settings) =>
        !IsOnPremiseStyleEndpoint(settings.Endpoint);

    /// <summary>
    /// WebSocket Realtime API is only wired for OpenAI's public <c>api.openai.com</c> host today.
    /// Azure OpenAI and other hosts are excluded until their Realtime URL scheme is configured.
    /// </summary>
    public static bool CanUseOpenAiRealtimeWebSocket(AzureOpenAISettings settings)
    {
        if (!IsOpenAiCloudHttp(settings))
            return false;
        if (string.IsNullOrWhiteSpace(settings.Endpoint))
            return true;
        try
        {
            var host = new Uri(settings.Endpoint.TrimEnd('/') + "/").Host;
            return host.Equals("api.openai.com", StringComparison.OrdinalIgnoreCase);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Models that use the Realtime WebSocket path with a transcription session
    /// (<c>gpt-realtime-whisper</c>, <c>gpt-realtime</c>, and dated snapshots of the latter).
    /// </summary>
    public static bool IsRealtimeTranscriptionModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return false;
        var m = model.Trim();
        if (m.Equals("gpt-realtime-whisper", StringComparison.OrdinalIgnoreCase))
            return true;
        if (m.StartsWith("gpt-realtime-whisper-", StringComparison.OrdinalIgnoreCase))
            return true;
        if (m.Equals("gpt-realtime", StringComparison.OrdinalIgnoreCase))
            return true;
        // Dated snapshots like gpt-realtime-2025-08-28 (not the whisper-* prefix)
        if (m.StartsWith("gpt-realtime-", StringComparison.OrdinalIgnoreCase)
            && !m.StartsWith("gpt-realtime-whisper", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    /// <summary>
    /// <c>gpt-realtime-whisper</c> transcription sessions reject <c>session.audio.input.turn_detection</c>
    /// ("Turn detection is not supported for this transcription model.").
    /// </summary>
    public static bool IsGptRealtimeWhisperFamily(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return false;
        var m = model.Trim();
        return m.Equals("gpt-realtime-whisper", StringComparison.OrdinalIgnoreCase)
               || m.StartsWith("gpt-realtime-whisper-", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldUseRealtimeTranscription(AzureOpenAISettings settings) =>
        CanUseOpenAiRealtimeWebSocket(settings) && IsRealtimeTranscriptionModel(settings.Model);
}
