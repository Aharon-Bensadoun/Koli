using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Koli.Config;

namespace Koli.Services;

public sealed class CustomActionProcessingService : IAsyncDisposable
{
    private readonly CustomActionsSettings _settings;
    private readonly string? _transcriptionEndpoint;
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    public event EventHandler<(string Method, string Url, Dictionary<string, string> Headers, string? Body)>? RequestLogging;
    public event EventHandler<(int StatusCode, string? StatusMessage, Dictionary<string, string> Headers, string? Body)>? ResponseLogging;
    public event EventHandler<(string Message, Exception? Exception)>? ErrorLogging;

    public CustomActionProcessingService(CustomActionsSettings settings, string? transcriptionEndpoint, string apiKey, HttpMessageHandler? handler = null)
    {
        _settings = settings;
        _transcriptionEndpoint = transcriptionEndpoint;
        _apiKey = apiKey;
        _httpClient = handler == null ? new HttpClient() : new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(90);
    }

    public bool IsAiNexus => OpenAiModelProfiles.IsOnPremiseStyleEndpoint(_transcriptionEndpoint);

    public async Task<string?> ProcessAsync(CustomActionProfile profile, string transcription, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transcription))
            return null;

        try
        {
            return IsAiNexus
                ? await ProcessAiNexusAsync(profile, transcription, cancellationToken).ConfigureAwait(false)
                : await ProcessOpenAiAsync(profile, transcription, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorLogging?.Invoke(this, ("Custom action request failed", ex));
            return null;
        }
    }

    public string ResolveAiNexusEndpoint()
    {
        var endpoint = _transcriptionEndpoint?.Trim() ?? "";
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            return endpoint;

        var path = uri.AbsolutePath.TrimEnd('/');
        var slash = path.LastIndexOf('/');
        var basePath = slash >= 0 ? path[..slash] : path;
        return new UriBuilder(uri)
        {
            Path = $"{basePath}/query".Replace("/api/AI/", "/api/ai/", StringComparison.Ordinal)
        }.Uri.ToString();
    }

    public string BuildOpenAiBody(CustomActionProfile profile, string transcription)
    {
        var model = string.IsNullOrWhiteSpace(profile.OpenAiModel)
            ? _settings.DefaultOpenAiModel
            : profile.OpenAiModel.Trim();
        var payload = new OpenAiRequest
        {
            Model = model,
            Messages =
            [
                new OpenAiMessage { Role = "system", Content = profile.SystemPrompt.Trim() },
                new OpenAiMessage { Role = "user", Content = transcription }
            ]
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public string BuildAiNexusBody(CustomActionProfile profile, string transcription)
    {
        var promptIdMode = profile.PromptMode.Equals("AiNexusPromptId", StringComparison.OrdinalIgnoreCase);
        var payload = new AiNexusRequest
        {
            Prompt = transcription,
            PromptId = promptIdMode ? profile.AiNexusPromptId : null,
            ProviderId = profile.AiNexusProviderId ?? _settings.DefaultAiNexusProviderId,
            Stream = false,
            ExternalUser = Environment.UserName,
            Parameters = promptIdMode ? null : new Dictionary<string, object> { ["systemPrompt"] = profile.SystemPrompt.Trim() }
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private async Task<string?> ProcessOpenAiAsync(CustomActionProfile profile, string transcription, CancellationToken cancellationToken)
    {
        if (profile.PromptMode.Equals("AiNexusPromptId", StringComparison.OrdinalIgnoreCase))
        {
            ErrorLogging?.Invoke(this, ("Prompt ID profiles require an AI Nexus endpoint", null));
            return null;
        }

        const string endpoint = "https://api.openai.com/v1/chat/completions";
        var json = BuildOpenAiBody(profile, transcription);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        RequestLogging?.Invoke(this, ("POST", endpoint, new Dictionary<string, string>(), $"customAction={profile.Name}; textLength={transcription.Length}"));
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        ResponseLogging?.Invoke(this, ((int)response.StatusCode, response.ReasonPhrase, new Dictionary<string, string>(), body));
        if (!response.IsSuccessStatusCode)
            return Fail($"OpenAI custom action error ({(int)response.StatusCode})");

        var parsed = JsonSerializer.Deserialize<OpenAiResponse>(body, ReadOptions);
        return Clean(parsed?.Choices?.FirstOrDefault()?.Message?.Content);
    }

    private async Task<string?> ProcessAiNexusAsync(CustomActionProfile profile, string transcription, CancellationToken cancellationToken)
    {
        var endpoint = ResolveAiNexusEndpoint();
        var json = BuildAiNexusBody(profile, transcription);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-api-key", _apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        RequestLogging?.Invoke(this, ("POST", endpoint, new Dictionary<string, string> { ["x-api-key"] = _apiKey }, $"customAction={profile.Name}; textLength={transcription.Length}"));
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        ResponseLogging?.Invoke(this, ((int)response.StatusCode, response.ReasonPhrase, new Dictionary<string, string>(), body));
        if (!response.IsSuccessStatusCode)
            return Fail($"AI Nexus custom action error ({(int)response.StatusCode})");

        var parsed = JsonSerializer.Deserialize<AiNexusResponse>(body, ReadOptions);
        if (parsed?.Success == false)
            return Fail(parsed.ErrorMessage ?? "AI Nexus rejected the custom action");
        return Clean(parsed?.Content);
    }

    private string? Fail(string message)
    {
        ErrorLogging?.Invoke(this, (message, null));
        return null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        return ValueTask.CompletedTask;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record OpenAiRequest
    {
        public string Model { get; init; } = "";
        public OpenAiMessage[] Messages { get; init; } = [];
        public double Temperature { get; init; } = 0.2;
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; init; } = 4000;
    }
    private sealed record OpenAiMessage { public string Role { get; init; } = ""; public string Content { get; init; } = ""; }
    private sealed record OpenAiChoice(OpenAiMessage? Message);
    private sealed record OpenAiResponse(OpenAiChoice[]? Choices);
    private sealed record AiNexusRequest
    {
        public string Prompt { get; init; } = "";
        public int? PromptId { get; init; }
        public int? ProviderId { get; init; }
        public bool Stream { get; init; }
        public string? ExternalUser { get; init; }
        public Dictionary<string, object>? Parameters { get; init; }
    }
    private sealed record AiNexusResponse(bool Success, string? Content, string? ErrorMessage);
}
