using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Koli.Config;

namespace Koli.Services;

public sealed class TextRewriteService : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly RewriteSettings _settings;
    private readonly string _apiKey;

    public event EventHandler<(string Method, string Url, Dictionary<string, string> Headers, string? Body)>? RequestLogging;
    public event EventHandler<(int StatusCode, string? StatusMessage, Dictionary<string, string> Headers, string? Body)>? ResponseLogging;
    public event EventHandler<(string Message, Exception? Exception)>? ErrorLogging;

    public TextRewriteService(RewriteSettings settings, string apiKey)
    {
        _settings = settings;
        _apiKey = apiKey;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60) // 60 second timeout for rewrite operations
        };
    }

    public async Task<string?> RewriteTextAsync(string text, string language, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            ErrorLogging?.Invoke(this, ("No text to rewrite", null));
            return null;
        }

        var requestUri = "https://api.openai.com/v1/chat/completions";

        try
        {
            var requestBody = new ChatCompletionRequest
            {
                Model = _settings.Model,
                Messages = new[]
                {
                    new ChatMessage
                    {
                        Role = "system",
                        Content = _settings.GetPromptForLevel(language)
                    },
                    new ChatMessage
                    {
                        Role = "user",
                        Content = text
                    }
                },
                Temperature = 0.7,
                MaxTokens = 2000
            };

            var jsonBody = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            // Log request
            RequestLogging?.Invoke(this, ("POST", requestUri, new Dictionary<string, string>(), $"Text length: {text.Length} characters"));

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            ResponseLogging?.Invoke(this, ((int)response.StatusCode, response.ReasonPhrase, new Dictionary<string, string>(), responseBody));

            response.EnsureSuccessStatusCode();

            var completionResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (completionResponse?.Choices != null && completionResponse.Choices.Length > 0)
            {
                var rewrittenText = completionResponse.Choices[0].Message?.Content?.Trim();
                if (!string.IsNullOrWhiteSpace(rewrittenText))
                {
                    return rewrittenText;
                }
            }

            ErrorLogging?.Invoke(this, ("No rewritten text in response", null));
            return null;
        }
        catch (Exception ex)
        {
            ErrorLogging?.Invoke(this, ("Error rewriting text", ex));
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        await Task.CompletedTask;
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

    private sealed record ChatCompletionResponse
    {
        public string? Id { get; init; }
        public string? Object { get; init; }
        public long Created { get; init; }
        public string? Model { get; init; }
        public ChatChoice[]? Choices { get; init; }
        public Usage? Usage { get; init; }
    }

    private sealed record ChatChoice
    {
        public int Index { get; init; }
        public ChatMessage? Message { get; init; }
        public string? FinishReason { get; init; }
    }

    private sealed record Usage
    {
        public int PromptTokens { get; init; }
        public int CompletionTokens { get; init; }
        public int TotalTokens { get; init; }
    }
}
