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

    /// <summary>
    /// True for <c>gpt-realtime</c> and dated <c>gpt-realtime-*</c> snapshots (not whisper transcription models).
    /// </summary>
    public static bool IsRealtimeSessionModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return false;
        var m = model.Trim();
        if (m.Equals("gpt-realtime", StringComparison.OrdinalIgnoreCase))
            return true;
        return m.StartsWith("gpt-realtime-", StringComparison.OrdinalIgnoreCase)
               && !m.StartsWith("gpt-realtime-whisper", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// WebSocket <c>?model=</c> must be a realtime session model (e.g. <c>gpt-realtime</c>), not <c>gpt-realtime-whisper</c>.
    /// </summary>
    public static string ResolveRealtimeSessionModel(string? configuredModel)
    {
        if (string.IsNullOrWhiteSpace(configuredModel))
            return "gpt-realtime";
        var m = configuredModel.Trim();
        if (IsGptRealtimeWhisperFamily(m))
            return "gpt-realtime";
        if (IsRealtimeSessionModel(m))
            return m;
        return "gpt-realtime";
    }

    /// <summary>
    /// Value for <c>session.audio.input.transcription.model</c> in <c>session.update</c>.
    /// </summary>
    public static string ResolveRealtimeTranscriptionModel(string? configuredModel)
    {
        if (string.IsNullOrWhiteSpace(configuredModel))
            return "gpt-realtime-whisper";
        var m = configuredModel.Trim();
        if (IsGptRealtimeWhisperFamily(m))
            return m;
        return "gpt-realtime-whisper";
    }

    public static bool ShouldUseRealtimeTranscription(AzureOpenAISettings settings) =>
        CanUseOpenAiRealtimeWebSocket(settings) && IsRealtimeTranscriptionModel(settings.Model);

    /// <summary>On-prem Ai Nexus WebSocket <c>/api/ai/realtime/transcribe</c> when live transcription is enabled.</summary>
    public static bool CanUseOnPremRealtimeWebSocket(AzureOpenAISettings settings) =>
        IsOnPremiseStyleEndpoint(settings.Endpoint) && settings.EnableStreamingTranscription;

    /// <summary>
    /// Legacy on-prem HTTP streaming (<c>queryAudio</c> + <c>stream=true</c>) when WebSocket is disabled or fails.
    /// </summary>
    public static bool CanUseOnPremHttpStreamingTranscription(AzureOpenAISettings settings) =>
        IsOnPremiseStyleEndpoint(settings.Endpoint)
        && settings.EnableStreamingTranscription
        && settings.UseQueryAudioHttpStreamingFallback;

    /// <summary>On-prem live transcription (WebSocket preferred, optional HTTP fallback).</summary>
    public static bool CanUseOnPremStreamingTranscription(AzureOpenAISettings settings) =>
        CanUseOnPremRealtimeWebSocket(settings) || CanUseOnPremHttpStreamingTranscription(settings);

    /// <summary>Streaming POST URL; falls back to <see cref="AzureOpenAISettings.Endpoint"/> when unset.</summary>
    public static string ResolveStreamingEndpoint(AzureOpenAISettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.StreamingEndpoint))
            return settings.StreamingEndpoint.TrimEnd('/');
        return settings.Endpoint.TrimEnd('/');
    }

    /// <summary>
    /// Builds <c>wss://…/api/ai/realtime/transcribe?x-api-key=…</c> from the configured HTTP endpoint or
    /// <see cref="AzureOpenAISettings.RealtimeEndpoint"/> when set.
    /// </summary>
    public static string BuildOnPremRealtimeWebSocketUrl(AzureOpenAISettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.RealtimeEndpoint))
            return ToWebSocketUrl(settings.RealtimeEndpoint.Trim());

        var httpBase = !string.IsNullOrWhiteSpace(settings.Endpoint)
            ? settings.Endpoint.Trim()
            : ResolveStreamingEndpoint(settings);

        return ToWebSocketUrl(DeriveRealtimeHttpPath(httpBase));
    }

    internal static string DeriveRealtimeHttpPath(string httpUrl)
    {
        var uri = new Uri(httpUrl.TrimEnd('/'));
        var path = uri.AbsolutePath;
        var apiIdx = path.IndexOf("/api", StringComparison.OrdinalIgnoreCase);
        path = apiIdx >= 0
            ? path[..apiIdx] + "/api/ai/realtime/transcribe"
            : "/api/ai/realtime/transcribe";

        var builder = new UriBuilder(uri) { Path = path };
        return builder.Uri.ToString();
    }

    private static string ToWebSocketUrl(string httpUrl)
    {
        var uri = new Uri(httpUrl.TrimEnd('/'));
        var scheme = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
        var builder = new UriBuilder(uri) { Scheme = scheme, Port = uri.IsDefaultPort ? -1 : uri.Port };
        return builder.Uri.ToString();
    }

    /// <summary>OpenAI Realtime WebSocket or on-prem live transcription.</summary>
    public static bool ShouldUseLiveTranscription(AzureOpenAISettings settings) =>
        ShouldUseRealtimeTranscription(settings) || CanUseOnPremStreamingTranscription(settings);

    /// <summary>
    /// Meeting mode always prefers live transcription: Realtime WebSocket on OpenAI cloud,
    /// on-prem HTTP streaming when enabled, otherwise the configured HTTP model (chunked stream).
    /// </summary>
    public static AzureOpenAISettings CreateMeetingTranscriptionSettings(AzureOpenAISettings source)
    {
        var meeting = CloneSettings(source);

        if (ShouldUseRealtimeTranscription(meeting))
            return meeting;

        if (CanUseOnPremStreamingTranscription(meeting))
            return meeting;

        if (CanUseOpenAiRealtimeWebSocket(meeting))
        {
            meeting.Model = "gpt-realtime-whisper";
            return meeting;
        }

        return meeting;
    }

    public static bool WillMeetingUseRealtimeTranscription(AzureOpenAISettings source) =>
        ShouldUseRealtimeTranscription(CreateMeetingTranscriptionSettings(source));

    public static bool WillMeetingUseLiveTranscription(AzureOpenAISettings source) =>
        ShouldUseLiveTranscription(CreateMeetingTranscriptionSettings(source));

    private static AzureOpenAISettings CloneSettings(AzureOpenAISettings source) => new()
    {
        ApiKey = source.ApiKey,
        Endpoint = source.Endpoint,
        Model = source.Model,
        Language = source.Language,
        Prompt = source.Prompt,
        LanguageMode = source.LanguageMode,
        ManualLanguage = source.ManualLanguage,
        OmitTranscriptionLanguage = source.OmitTranscriptionLanguage,
        ProviderId = source.ProviderId,
        TranscriptionPromptId = source.TranscriptionPromptId,
        FormattingPromptId = source.FormattingPromptId,
        EnableSpeakerDiarization = source.EnableSpeakerDiarization,
        EnableStreamingTranscription = source.EnableStreamingTranscription,
        StreamingEndpoint = source.StreamingEndpoint,
        StreamingProviderId = source.StreamingProviderId,
        RealtimeEndpoint = source.RealtimeEndpoint,
        UseQueryAudioHttpStreamingFallback = source.UseQueryAudioHttpStreamingFallback
    };
}
