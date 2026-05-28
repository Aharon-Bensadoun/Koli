using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Koli.Config;

namespace Koli.Services;

public sealed class VoiceAssistantService : IAsyncDisposable
{
    private readonly AssistantSettings _settings;
    private readonly string? _transcriptionEndpoint;
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    public event EventHandler<(string Method, string Url, Dictionary<string, string> Headers, string? Body)>? RequestLogging;
    public event EventHandler<(int StatusCode, string? StatusMessage, Dictionary<string, string> Headers, string? Body)>? ResponseLogging;
    public event EventHandler<(string Message, Exception? Exception)>? ErrorLogging;

    public VoiceAssistantService(AssistantSettings settings, string? transcriptionEndpoint, string apiKey)
    {
        _settings = settings;
        _transcriptionEndpoint = transcriptionEndpoint;
        _apiKey = apiKey;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(90)
        };
    }

    public bool IsSupported => TranscriptionOutputLanguageService.IsOpenAiEndpoint(_transcriptionEndpoint);

    public static bool IsSupportedEndpoint(string? endpoint) =>
        TranscriptionOutputLanguageService.IsOpenAiEndpoint(endpoint);

    public async Task<string?> QueryAsync(string userQuestion, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userQuestion))
        {
            ErrorLogging?.Invoke(this, ("No question to answer", null));
            return null;
        }

        if (!IsSupported)
        {
            ErrorLogging?.Invoke(this, ("Voice assistant requires the public OpenAI endpoint (api.openai.com).", null));
            return null;
        }

        var responsesUri = ResolveResponsesEndpoint();

        if (_settings.WebSearchEnabled)
        {
            var withSearch = await TryResponsesAsync(responsesUri, userQuestion, includeWebSearch: true, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(withSearch))
                return withSearch;
        }

        var withoutSearch = await TryResponsesAsync(responsesUri, userQuestion, includeWebSearch: false, cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(withoutSearch))
            return withoutSearch;

        return await TryChatCompletionFallbackAsync(userQuestion, cancellationToken).ConfigureAwait(false);
    }

    public static string BuildResponsesRequestBody(AssistantSettings settings, string userQuestion, bool includeWebSearch)
    {
        var payload = new ResponsesRequest
        {
            Model = settings.Model,
            Instructions = settings.SystemPrompt,
            Input = userQuestion,
            ToolChoice = includeWebSearch ? "auto" : null,
            Tools = includeWebSearch
                ? new[] { new ResponsesTool { Type = "web_search" } }
                : null
        };

        return JsonSerializer.Serialize(payload, RequestSerializerOptions);
    }

    public static string BuildChatCompletionRequestBody(AssistantSettings settings, string userQuestion)
    {
        var payload = new ChatCompletionRequest
        {
            Model = settings.Model,
            Messages =
            [
                new ChatMessage { Role = "system", Content = settings.SystemPrompt },
                new ChatMessage { Role = "user", Content = userQuestion }
            ],
            Temperature = 0.3,
            MaxTokens = 2000
        };

        return JsonSerializer.Serialize(payload, RequestSerializerOptions);
    }

    private async Task<string?> TryResponsesAsync(string requestUri, string userQuestion, bool includeWebSearch, CancellationToken cancellationToken)
    {
        try
        {
            var jsonBody = BuildResponsesRequestBody(_settings, userQuestion, includeWebSearch);
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            RequestLogging?.Invoke(this, ("POST", requestUri, new Dictionary<string, string>(),
                $"Responses API (web_search={includeWebSearch}), question length: {userQuestion.Length}"));

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            ResponseLogging?.Invoke(this, ((int)response.StatusCode, response.ReasonPhrase, new Dictionary<string, string>(), responseBody));

            if (!response.IsSuccessStatusCode)
            {
                ErrorLogging?.Invoke(this, ($"Responses API error ({(int)response.StatusCode})", null));
                return null;
            }

            var answer = VoiceAssistantResponseParser.ParseOutputText(responseBody);
            if (string.IsNullOrWhiteSpace(answer))
            {
                ErrorLogging?.Invoke(this, ("No assistant text in Responses API payload", null));
                return null;
            }

            return answer;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorLogging?.Invoke(this, ("Error calling Responses API", ex));
            return null;
        }
    }

    private async Task<string?> TryChatCompletionFallbackAsync(string userQuestion, CancellationToken cancellationToken)
    {
        var requestUri = "https://api.openai.com/v1/chat/completions";

        try
        {
            var jsonBody = BuildChatCompletionRequestBody(_settings, userQuestion);
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            RequestLogging?.Invoke(this, ("POST", requestUri, new Dictionary<string, string>(),
                $"Chat completion fallback, question length: {userQuestion.Length}"));

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            ResponseLogging?.Invoke(this, ((int)response.StatusCode, response.ReasonPhrase, new Dictionary<string, string>(), responseBody));

            if (!response.IsSuccessStatusCode)
            {
                ErrorLogging?.Invoke(this, ($"Chat completion fallback error ({(int)response.StatusCode})", null));
                return null;
            }

            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var choice in choices.EnumerateArray())
            {
                if (!choice.TryGetProperty("message", out var message))
                    continue;
                if (!message.TryGetProperty("content", out var content))
                    continue;

                var text = VoiceAssistantResponseParser.CleanResponseText(content.GetString());
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            ErrorLogging?.Invoke(this, ("No assistant text in chat completion fallback", null));
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorLogging?.Invoke(this, ("Error in chat completion fallback", ex));
            return null;
        }
    }

    private static string ResolveResponsesEndpoint() => "https://api.openai.com/v1/responses";

    public async ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        await Task.CompletedTask;
    }

    private static readonly JsonSerializerOptions RequestSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed record ResponsesRequest
    {
        public string Model { get; init; } = string.Empty;
        public string Instructions { get; init; } = string.Empty;
        public string Input { get; init; } = string.Empty;
        public ResponsesTool[]? Tools { get; init; }
        public string? ToolChoice { get; init; }
    }

    private sealed record ResponsesTool
    {
        public string Type { get; init; } = string.Empty;
    }

    private sealed record ChatCompletionRequest
    {
        public string Model { get; init; } = string.Empty;
        public ChatMessage[] Messages { get; init; } = Array.Empty<ChatMessage>();
        public double Temperature { get; init; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; init; }
    }

    private sealed record ChatMessage
    {
        public string Role { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
    }
}
