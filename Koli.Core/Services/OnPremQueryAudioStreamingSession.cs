using Koli.Config;

namespace Koli.Services;

/// <summary>
/// On-prem meeting live transcription: sends rolling PCM windows as complete WAV files
/// with <c>stream=true</c> and parses SSE/NDJSON streaming responses into
/// <see cref="RealtimeTranscriptEventArgs"/>.
/// </summary>
public sealed class OnPremQueryAudioStreamingSession
{
    private readonly HttpClient _httpClient;
    private readonly AzureOpenAISettings _settings;
    private readonly string _apiKey;
    private readonly string _language;

    public OnPremQueryAudioStreamingSession(HttpClient httpClient, AzureOpenAISettings settings, string apiKey, string language)
    {
        _httpClient = httpClient;
        _settings = settings;
        _apiKey = apiKey;
        _language = language;
    }

    public async Task RunAsync(
        IAsyncEnumerable<byte[]> pcm16kChunks,
        int windowDurationSeconds,
        Action<RealtimeTranscriptEventArgs> onTranscript,
        Action<string, Exception?> onError,
        Action<string, string, Dictionary<string, string>, string?>? requestLogging,
        Action<int, string?, Dictionary<string, string>, string?>? responseLogging,
        CancellationToken cancellationToken)
    {
        var requestUri = OpenAiModelProfiles.ResolveStreamingEndpoint(_settings);
        var providerId = _settings.StreamingProviderId ?? _settings.ProviderId;
        var bytesPerSecond = 16_000 * 2;
        var windowSize = Math.Max(bytesPerSecond, bytesPerSecond * Math.Max(1, windowDurationSeconds));

        var buffer = new MemoryStream();
        var windowIndex = 0;

        try
        {
            await foreach (var chunk in pcm16kChunks.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await buffer.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);

                while (buffer.Length >= windowSize)
                {
                    var audioData = ExtractWindow(buffer, windowSize);
                    await SendWindowAsync(
                        requestUri,
                        providerId,
                        audioData,
                        windowIndex++,
                        onTranscript,
                        onError,
                        requestLogging,
                        responseLogging,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            if (buffer.Length > 1000)
            {
                await SendWindowAsync(
                    requestUri,
                    providerId,
                    buffer.ToArray(),
                    windowIndex,
                    onTranscript,
                    onError,
                    requestLogging,
                    responseLogging,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal stop
        }
        catch (Exception ex)
        {
            onError($"On-prem streaming session error: {ex.Message}", ex);
        }
    }

    private async Task SendWindowAsync(
        string requestUri,
        int? providerId,
        byte[] pcmAudio,
        int windowIndex,
        Action<RealtimeTranscriptEventArgs> onTranscript,
        Action<string, Exception?> onError,
        Action<string, string, Dictionary<string, string>, string?>? requestLogging,
        Action<int, string?, Dictionary<string, string>, string?>? responseLogging,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Add("x-api-key", _apiKey);
        request.Content = OnPremStreamingMultipartContent.Create(pcmAudio, _settings, _language, stream: true, providerId);

        var headers = new Dictionary<string, string> { { "x-api-key", _apiKey } };
        requestLogging?.Invoke(
            "POST",
            requestUri,
            headers,
            OnPremStreamingMultipartContent.BuildRequestLogBody(pcmAudio.Length, stream: true));

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var contentType = response.Content.Headers.ContentType?.MediaType;
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            using var errorReader = new StreamReader(responseStream);
            var errorBody = await errorReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            responseLogging?.Invoke((int)response.StatusCode, response.ReasonPhrase, new Dictionary<string, string>(), errorBody);
            onError($"On-prem streaming HTTP {(int)response.StatusCode}: {errorBody}", null);
            return;
        }

        var itemPrefix = $"onprem-{windowIndex}";
        var eventIndex = 0;
        var accumulated = "";

        await foreach (var evt in OnPremStreamingResponseParser
                           .ReadStreamAsync(responseStream, contentType, cancellationToken)
                           .ConfigureAwait(false))
        {
            if (!evt.Success)
            {
                if (!string.IsNullOrWhiteSpace(evt.ErrorMessage))
                    onError(evt.ErrorMessage, null);
                continue;
            }

            if (string.IsNullOrWhiteSpace(evt.Content))
                continue;

            var itemId = $"{itemPrefix}-{eventIndex++}";
            var delta = evt.Delta;
            if (string.IsNullOrWhiteSpace(delta) && !evt.IsFinal && !string.IsNullOrEmpty(accumulated)
                && evt.Content.StartsWith(accumulated, StringComparison.Ordinal))
            {
                delta = evt.Content[accumulated.Length..];
            }

            accumulated = evt.Content;
            onTranscript(new RealtimeTranscriptEventArgs
            {
                ItemId = itemId,
                Text = evt.Content,
                IsFinal = evt.IsFinal,
                Delta = evt.IsFinal ? null : delta
            });
        }
    }

    private static byte[] ExtractWindow(MemoryStream buffer, int windowSize)
    {
        var all = buffer.ToArray();
        var audioData = all.AsSpan(0, windowSize).ToArray();
        var remaining = all.AsSpan(windowSize).ToArray();
        buffer.SetLength(0);
        if (remaining.Length > 0)
            buffer.Write(remaining, 0, remaining.Length);
        return audioData;
    }
}
