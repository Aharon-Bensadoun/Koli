using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Koli.Config;

namespace Koli.Services;

/// <summary>
/// Translates a transcribed text to the user-selected target language via a chat-completion call.
/// Post-transcription fallback when OpenAI/Azure STT cannot produce cross-lingual output directly
/// (e.g. whisper-1 to non-English, Realtime). Whisper / gpt-4o-transcribe <c>language</c> is only an input hint.
///
/// Two protocols are supported, chosen automatically from the configured endpoint:
///   - <b>OpenAI / Azure OpenAI</b> (URL contains <c>openai.com</c>): standard
///     <c>/v1/chat/completions</c> payload with <c>model</c> + <c>messages</c>.
///   - <b>Ai Nexus</b> (any other URL, e.g. <c>https://&lt;host&gt;/api/AI/queryAudio</c>):
///     sibling <c>/api/ai/query</c> endpoint documented by Hadassah, with a single
///     <c>prompt</c> field; optional <c>providerId</c> from <see cref="TranslationSettings.ProviderId"/> only (never from STT settings).
/// </summary>
public sealed class TextTranslationService : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly TranslationSettings _translation;
    /// <summary>Transcription API base URL only (e.g. <c>.../queryAudio</c>), used when <see cref="TranslationSettings.Endpoint"/> is empty. Provider id for translation is never read from STT config.</summary>
    private readonly string? _transcriptionEndpointForDerivation;
    private readonly string _apiKey;

    public event EventHandler<(string Method, string Url, Dictionary<string, string> Headers, string? Body)>? RequestLogging;
    public event EventHandler<(int StatusCode, string? StatusMessage, Dictionary<string, string> Headers, string? Body)>? ResponseLogging;
    public event EventHandler<(string Message, Exception? Exception)>? ErrorLogging;

    public TextTranslationService(TranslationSettings translation, string? transcriptionEndpointForDerivation, string apiKey)
    {
        _translation = translation;
        _transcriptionEndpointForDerivation = transcriptionEndpointForDerivation;
        _apiKey = apiKey;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    /// <summary>
    /// Translates <paramref name="text"/> into <paramref name="targetLanguage"/> (ISO 639-1).
    /// Returns the translated text, or <c>null</c> if the call fails or the API returns no usable content.
    /// </summary>
    public async Task<string?> TranslateAsync(string text, string targetLanguage, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var requestUri = ResolveEndpoint();
        if (string.IsNullOrWhiteSpace(requestUri))
        {
            ErrorLogging?.Invoke(this, ("Translation endpoint is not configured and could not be derived", null));
            return null;
        }

        var useOpenAi = requestUri.Contains("openai.com", StringComparison.OrdinalIgnoreCase);
        var languageName = GetLanguageName(targetLanguage);
        var systemPrompt = BuildSystemPrompt(languageName);

        try
        {
            return useOpenAi
                ? await TranslateViaOpenAiAsync(requestUri, systemPrompt, text, cancellationToken).ConfigureAwait(false)
                : await TranslateViaAirNexusAsync(requestUri, systemPrompt, text, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            ErrorLogging?.Invoke(this, ("Error translating text", ex));
            return null;
        }
    }

    private string ResolveEndpoint()
    {
        if (!string.IsNullOrWhiteSpace(_translation.Endpoint))
            return _translation.Endpoint.Trim();

        var sttEndpoint = _transcriptionEndpointForDerivation?.Trim() ?? string.Empty;

        // Empty or OpenAI-style transcription endpoint → use OpenAI cloud chat.
        if (string.IsNullOrWhiteSpace(sttEndpoint) ||
            sttEndpoint.Contains("openai.com", StringComparison.OrdinalIgnoreCase))
        {
            return "https://api.openai.com/v1/chat/completions";
        }

        // Ai Nexus / on-premise: replace the final "queryAudio" (or similar) segment with "query".
        try
        {
            var uri = new Uri(sttEndpoint);
            var path = uri.AbsolutePath.TrimEnd('/');
            var lastSlash = path.LastIndexOf('/');
            if (lastSlash >= 0)
            {
                var basePath = path.Substring(0, lastSlash);
                // Normalise to the documented lowercase "/api/ai/query".
                var derived = new UriBuilder(uri)
                {
                    Path = $"{basePath}/query".Replace("/api/AI/", "/api/ai/", StringComparison.Ordinal)
                };
                return derived.Uri.ToString();
            }
        }
        catch
        {
            // Fall through to returning the original endpoint unchanged.
        }

        return sttEndpoint;
    }

    private static string GetLanguageName(string code) => (code ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "en" => "English",
        "fr" => "French",
        "he" or "iw" => "Hebrew",
        "es" => "Spanish",
        "de" => "German",
        "it" => "Italian",
        "pt" => "Portuguese",
        "ar" => "Arabic",
        "ru" => "Russian",
        "zh" => "Chinese",
        "ja" => "Japanese",
        "ko" => "Korean",
        "nl" => "Dutch",
        _ => "English"
    };

    private static string BuildSystemPrompt(string languageName) =>
        $"You are a translation engine. Translate the user's text to {languageName}. " +
        "Preserve the original meaning, tone, punctuation and paragraph breaks. " +
        $"If the text is already in {languageName}, return it unchanged. " +
        "Do not add explanations, notes or quotation marks — output the translated text only.";

    private async Task<string?> TranslateViaOpenAiAsync(string requestUri, string systemPrompt, string text, CancellationToken cancellationToken)
    {
        var requestBody = new OpenAiChatRequest
        {
            Model = _translation.Model,
            Messages = new[]
            {
                new OpenAiChatMessage { Role = "system", Content = systemPrompt },
                new OpenAiChatMessage { Role = "user", Content = text }
            },
            Temperature = 0.0,
            MaxTokens = 2000
        };

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        RequestLogging?.Invoke(this, ("POST", requestUri, new Dictionary<string, string>(),
            $"protocol=openai; model={_translation.Model}; textLength={text.Length}"));

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        ResponseLogging?.Invoke(this, ((int)response.StatusCode, response.ReasonPhrase, new Dictionary<string, string>(), responseBody));

        if (!response.IsSuccessStatusCode)
        {
            ErrorLogging?.Invoke(this, ($"Translation API error: {(int)response.StatusCode} {response.ReasonPhrase}", null));
            return null;
        }

        var parsed = JsonSerializer.Deserialize<OpenAiChatResponse>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        var content = parsed?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
        return string.IsNullOrWhiteSpace(content) ? null : content;
    }

    private async Task<string?> TranslateViaAirNexusAsync(string requestUri, string systemPrompt, string text, CancellationToken cancellationToken)
    {
        // Ai Nexus: providerId comes only from Translation configuration (not STT).
        var combinedPrompt = $"{systemPrompt}\n\n---\n{text}";
        var requestBody = new AirNexusQueryRequest
        {
            Prompt = combinedPrompt,
            Stream = false,
            ExternalUser = Environment.UserName,
            ProviderId = _translation.ProviderId
        };

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Add("x-api-key", _apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var headers = new Dictionary<string, string> { { "x-api-key", _apiKey } };
        RequestLogging?.Invoke(this, ("POST", requestUri, headers,
            $"protocol=air-nexus; providerId={_translation.ProviderId?.ToString() ?? "<omitted>"}; textLength={text.Length}"));

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        ResponseLogging?.Invoke(this, ((int)response.StatusCode, response.ReasonPhrase ?? "", new Dictionary<string, string>(), responseBody));

        if (!response.IsSuccessStatusCode)
        {
            ErrorLogging?.Invoke(this, ($"Translation API error: {(int)response.StatusCode} {response.ReasonPhrase}", null));
            return null;
        }

        var parsed = JsonSerializer.Deserialize<AirNexusQueryResponse>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (parsed?.Success == false && !string.IsNullOrWhiteSpace(parsed.ErrorMessage))
        {
            ErrorLogging?.Invoke(this, ($"Translation server error: {parsed.ErrorMessage}", null));
            return null;
        }
        var content = parsed?.Content?.Trim();
        return string.IsNullOrWhiteSpace(content) ? null : content;
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        await Task.CompletedTask;
    }

    private sealed record OpenAiChatRequest
    {
        public string Model { get; init; } = string.Empty;
        public OpenAiChatMessage[] Messages { get; init; } = Array.Empty<OpenAiChatMessage>();
        public double Temperature { get; init; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; init; }
    }

    private sealed record OpenAiChatMessage
    {
        public string Role { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
    }

    private sealed record OpenAiChatResponse(OpenAiChatChoice[]? Choices);
    private sealed record OpenAiChatChoice(OpenAiChatMessage? Message);

    private sealed record AirNexusQueryRequest
    {
        public string Prompt { get; init; } = string.Empty;
        public bool Stream { get; init; }
        public string? ExternalUser { get; init; }
        [JsonPropertyName("providerId")]
        public int? ProviderId { get; init; }
    }

    private sealed record AirNexusQueryResponse(
        bool Success,
        string? Content,
        string? ErrorMessage,
        int? RequestId);
}
