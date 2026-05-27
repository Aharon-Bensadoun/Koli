using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Koli.Config;

namespace Koli.Services;

public sealed class SpeechToTextService : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AzureOpenAISettings _settings;
    private readonly TranslationSettings _translationSettings;
    private readonly string _apiKey;
    private CancellationTokenSource? _cts;
    private string _currentLanguage = "fr";
    private TextTranslationService? _fallbackTranslation;

    public event EventHandler<string>? TranscriptionReceived;

    /// <summary>True when the last HTTP or Realtime segment already applied cross-lingual output (skip post-STT LLM).</summary>
    public bool OutputAlreadyApplied { get; private set; }

    /// <summary>Strategy used for the most recent transcription request.</summary>
    public TranscriptionApiMode LastTranscriptionMode { get; private set; } = TranscriptionApiMode.Transcribe;

    public TranscriptionRequestPlan? LastRequestPlan { get; private set; }

    /// <summary>Language code sent to the transcription API (used for on-premise; for Azure, Language from settings is used).</summary>
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set => _currentLanguage = value ?? "fr";
    }
    public event EventHandler<(string Method, string Url, Dictionary<string, string> Headers, string? Body)>? RequestLogging;
    public event EventHandler<(int StatusCode, string? StatusMessage, Dictionary<string, string> Headers, string? Body)>? ResponseLogging;
    public event EventHandler<(string Message, Exception? Exception)>? ErrorLogging;

    /// <summary>Incremental/final transcripts when using OpenAI Realtime transcription models.</summary>
    public event EventHandler<RealtimeTranscriptEventArgs>? RealtimeTranscript;

    /// <summary>True when <see cref="AzureOpenAISettings.Model"/> is a Realtime model and the endpoint supports <c>wss://api.openai.com/v1/realtime</c>.</summary>
    public bool UsesRealtimeTranscription => OpenAiModelProfiles.ShouldUseRealtimeTranscription(_settings);

    private CancellationTokenSource? _realtimeCts;
    private Task? _realtimeTask;

    public SpeechToTextService(AzureOpenAISettings settings, string apiKey, TranslationSettings? translationSettings = null)
    {
        _settings = settings;
        _translationSettings = translationSettings ?? new TranslationSettings();
        _apiKey = apiKey;
        _currentLanguage = !string.IsNullOrWhiteSpace(settings.Language) ? settings.Language : "fr";
        _httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    private string ResolveInputLanguage()
    {
        var lang = TranscriptionOutputLanguageService.GetInputLanguage(_settings);
        if (!string.IsNullOrWhiteSpace(_currentLanguage))
            lang = _currentLanguage;
        return lang;
    }

    private TranscriptionRequestPlan ResolveHttpPlan()
    {
        var plan = TranscriptionStrategyResolver.ResolvePlan(
            _settings, _translationSettings, ResolveInputLanguage(), isRealtime: false);
        LastRequestPlan = plan;
        LastTranscriptionMode = plan.Mode;
        return plan;
    }

    private TranscriptionRequestPlan ResolveRealtimePlan()
    {
        var plan = TranscriptionStrategyResolver.ResolvePlan(
            _settings, _translationSettings, ResolveInputLanguage(), isRealtime: true);
        LastRequestPlan = plan;
        return plan;
    }

    public Task StartAsync(IAsyncEnumerable<byte[]> audioStream, CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(async () =>
        {
            try
            {
                await StreamAsync(audioStream, _cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ErrorLogging?.Invoke(this, ($"Error in StreamAsync task: {ex.Message}", ex));
            }
        }, cancellationToken);
        return Task.CompletedTask;
    }

    private string _lastTranscription = "";

    private async Task StreamAsync(IAsyncEnumerable<byte[]> audioStream, CancellationToken cancellationToken)
    {
        if (UsesRealtimeTranscription)
        {
            ErrorLogging?.Invoke(this, ("Realtime models use RunRealtimeTranscriptionAsync instead of chunk HTTP mode.", null));
            return;
        }

        ErrorLogging?.Invoke(this, ("Starting continuous transcription (chunking 4s)", null));
        
        var buffer = new MemoryStream();
        var chunkDuration = TimeSpan.FromSeconds(4);
        var bytesPerSecond = 16000 * 2; // 16kHz * 16bit
        var chunkSize = (int)(bytesPerSecond * chunkDuration.TotalSeconds);
        _lastTranscription = ""; // Reset last transcription

        try
        {
            await foreach (var chunk in audioStream.WithCancellation(cancellationToken))
            {
                await buffer.WriteAsync(chunk, cancellationToken);

                if (buffer.Length >= chunkSize)
                {
                    var audioData = buffer.ToArray();
                    buffer.SetLength(0);
                    
                    // Process chunk sequentially
                    try 
                    {
                        await ProcessChunkAsync(audioData, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        ErrorLogging?.Invoke(this, ("Error processing chunk", ex));
                    }
                }
            }
        }
        catch (TaskCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            ErrorLogging?.Invoke(this, ("Error reading audio stream", ex));
        }

        // Process remaining audio if enough data
        if (buffer.Length > 1000) // Minimum 1kb to avoid empty errors
        {
            var audioData = buffer.ToArray();
            try 
            {
                await ProcessChunkAsync(audioData, cancellationToken);
            }
            catch (Exception ex)
            {
                ErrorLogging?.Invoke(this, ("Error processing final chunk", ex));
            }
        }
    }

    public async Task TranscribeAudioAsync(byte[] audioData, CancellationToken cancellationToken)
    {
        if (audioData == null || audioData.Length == 0)
        {
            ErrorLogging?.Invoke(this, ("No audio data to transcribe", null));
            return;
        }

        if (UsesRealtimeTranscription)
        {
            ErrorLogging?.Invoke(this, ("TranscribeAudioAsync is not used for Realtime models; transcription runs during capture.", null));
            return;
        }

        await ProcessChunkAsync(audioData, cancellationToken);
    }

    /// <summary>
    /// Streams mono PCM16 LE audio (16 kHz) through OpenAI Realtime transcription until the enumerable completes.
    /// Raises <see cref="RealtimeTranscript"/> for partial and final utterances.
    /// </summary>
    /// <remarks>
    /// The WebSocket session uses its own cancellation source (stopped via <see cref="StopRealtimeTranscriptionAsync"/> /
    /// dispose). The <paramref name="cancellationToken"/> is not linked: tying it to UI recording cancellation aborts the
    /// socket before <c>input_audio_buffer.commit</c> and races with in-flight sends.
    /// </remarks>
    public Task RunRealtimeTranscriptionAsync(
        IAsyncEnumerable<byte[]> pcm16kChunks,
        CancellationToken cancellationToken,
        bool periodicBufferCommits = false,
        int periodicCommitIntervalSeconds = 6)
    {
        if (!UsesRealtimeTranscription)
        {
            ErrorLogging?.Invoke(this, ("RunRealtimeTranscriptionAsync requires a Realtime transcription model and api.openai.com.", null));
            return Task.CompletedTask;
        }

        _ = cancellationToken; // Reserved for future cooperative shutdown without unlink side effects.

        _realtimeCts?.Cancel();
        _realtimeCts?.Dispose();
        _realtimeCts = new CancellationTokenSource();
        var token = _realtimeCts.Token;

        var wssUrl = OpenAiRealtimeTranscriptionSession.BuildWebSocketUrl(_settings);
        var realtimePlan = ResolveRealtimePlan();
        LastTranscriptionMode = realtimePlan.Mode;
        OutputAlreadyApplied = false;

        if (realtimePlan.Mode == TranscriptionApiMode.PostTranslationFallback)
            _fallbackTranslation = new TextTranslationService(_translationSettings, _settings.Endpoint, _apiKey);

        _realtimeTask = Task.Run(async () =>
        {
            await using var session = new OpenAiRealtimeTranscriptionSession(_settings, _apiKey);
            await session.RunAsync(
                pcm16kChunks,
                e => OnRealtimeTranscriptReceived(e, realtimePlan),
                (msg, ex) => ErrorLogging?.Invoke(this, (msg, ex)),
                (method, body) =>
                {
                    try
                    {
                        var url = method == "CONNECT" ? (body ?? wssUrl) : wssUrl;
                        RequestLogging?.Invoke(this, (method, url, new Dictionary<string, string>(), body));
                    }
                    catch
                    {
                        // Debug logging must never break the realtime session.
                    }
                },
                (method, body) =>
                {
                    try
                    {
                        ResponseLogging?.Invoke(this, (200, method, new Dictionary<string, string>(), body));
                    }
                    catch
                    {
                        // Debug logging must never break the realtime session.
                    }
                },
                token,
                periodicBufferCommits,
                periodicCommitIntervalSeconds,
                realtimePlan).ConfigureAwait(false);

            if (_fallbackTranslation != null)
            {
                await _fallbackTranslation.DisposeAsync().ConfigureAwait(false);
                _fallbackTranslation = null;
            }
        }, CancellationToken.None);

        return _realtimeTask;
    }

    /// <summary>Cancels an in-flight <see cref="RunRealtimeTranscriptionAsync"/> session.</summary>
    public Task StopRealtimeTranscriptionAsync()
    {
        try
        {
            _realtimeCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // ignored
        }

        return Task.CompletedTask;
    }

    private void OnRealtimeTranscriptReceived(RealtimeTranscriptEventArgs e, TranscriptionRequestPlan plan)
    {
        if (e.IsFinal)
        {
            var t = e.Text.Trim();
            if (string.IsNullOrEmpty(t))
                return;
            if (IsHallucination(t))
            {
                ErrorLogging?.Invoke(this, ($"Hallucination detected and filtered: {t}", null));
                return;
            }

            if (plan.Mode == TranscriptionApiMode.PostTranslationFallback
                && !string.IsNullOrWhiteSpace(plan.OutputLanguage)
                && _fallbackTranslation != null)
            {
                _ = TranslateRealtimeSegmentAsync(e, plan);
                return;
            }

            if (TranscriptionStrategyResolver.OutputAppliedByStt(plan.Mode))
                OutputAlreadyApplied = true;
        }

        RealtimeTranscript?.Invoke(this, e);
    }

    private async Task TranslateRealtimeSegmentAsync(RealtimeTranscriptEventArgs e, TranscriptionRequestPlan plan)
    {
        try
        {
            var translated = await _fallbackTranslation!
                .TranslateAsync(e.Text, plan.OutputLanguage!, CancellationToken.None)
                .ConfigureAwait(false);

            var finalText = string.IsNullOrWhiteSpace(translated) ? e.Text : translated;
            OutputAlreadyApplied = true;
            RealtimeTranscript?.Invoke(this, new RealtimeTranscriptEventArgs
            {
                ItemId = e.ItemId,
                Text = finalText,
                IsFinal = true,
                Delta = null
            });
        }
        catch (Exception ex)
        {
            ErrorLogging?.Invoke(this, ("Realtime segment translation failed", ex));
            RealtimeTranscript?.Invoke(this, e);
        }
    }

    private bool UseOnPremiseApi => !string.IsNullOrWhiteSpace(_settings.Endpoint) && !_settings.Endpoint.Contains("openai.com", StringComparison.OrdinalIgnoreCase);

    private async Task ProcessChunkAsync(byte[] audioData, CancellationToken cancellationToken)
    {
        if (UsesRealtimeTranscription)
            return;

        if (UseOnPremiseApi)
        {
            await ProcessChunkOnPremiseAsync(audioData, cancellationToken).ConfigureAwait(false);
            return;
        }

        var plan = ResolveHttpPlan();
        LastTranscriptionMode = plan.Mode;
        OutputAlreadyApplied = false;

        var text = await SendOpenAiAudioRequestAsync(audioData, plan, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (plan.Mode == TranscriptionApiMode.PostTranslationFallback
            && !string.IsNullOrWhiteSpace(plan.OutputLanguage))
        {
            text = await ApplyPostTranslationFallbackAsync(text, plan.OutputLanguage!, cancellationToken).ConfigureAwait(false);
            OutputAlreadyApplied = !string.IsNullOrWhiteSpace(text);
        }
        else
        {
            OutputAlreadyApplied = TranscriptionStrategyResolver.OutputAppliedByStt(plan.Mode);
        }

        EmitTranscriptionIfValid(text);
    }

    private async Task<string?> SendOpenAiAudioRequestAsync(byte[] audioData, TranscriptionRequestPlan plan, CancellationToken cancellationToken)
    {
        var baseUri = !string.IsNullOrWhiteSpace(_settings.Endpoint) && _settings.Endpoint.Contains("openai.com", StringComparison.OrdinalIgnoreCase)
            ? _settings.Endpoint.TrimEnd('/')
            : "https://api.openai.com";
        var requestUri = $"{baseUri}/v1/audio/{plan.EndpointPath}";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var content = new MultipartFormDataContent();
        using var wavStream = new MemoryStream();
        WriteWavHeader(wavStream, audioData.Length);
        await wavStream.WriteAsync(audioData, cancellationToken);
        wavStream.Position = 0;

        var audioContent = new StreamContent(wavStream);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(audioContent, "file", "audio.wav");
        content.Add(new StringContent(_settings.Model), "model");

        if (!string.IsNullOrWhiteSpace(plan.InputLanguageHint))
            content.Add(new StringContent(plan.InputLanguageHint), "language");

        if (!string.IsNullOrWhiteSpace(plan.EffectivePrompt))
            content.Add(new StringContent(plan.EffectivePrompt), "prompt");

        request.Content = content;
        RequestLogging?.Invoke(this, ("POST", requestUri, new Dictionary<string, string>(), BuildRequestLogBody(audioData.Length, plan.EffectivePrompt, plan)));

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            ResponseLogging?.Invoke(this, ((int)response.StatusCode, response.ReasonPhrase, new Dictionary<string, string>(), responseBody));

            if (!response.IsSuccessStatusCode)
            {
                var apiMessage = TryParseApiErrorMessage(responseBody);
                var statusInfo = $"{(int)response.StatusCode} {response.ReasonPhrase}";
                var userMessage = string.IsNullOrEmpty(apiMessage)
                    ? $"Transcription API error: {statusInfo}."
                    : $"Transcription API error ({statusInfo}): {apiMessage}";
                ErrorLogging?.Invoke(this, (userMessage, null));
                return null;
            }

            var transcriptionResponse = JsonSerializer.Deserialize<OpenAITranscriptionResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return transcriptionResponse?.Text?.Trim();
        }
        catch (Exception ex)
        {
            if (ex is TaskCanceledException) return null;
            ErrorLogging?.Invoke(this, ("Error sending/parsing chunk", ex));
            return null;
        }
    }

    private async Task<string?> ApplyPostTranslationFallbackAsync(string text, string targetLanguage, CancellationToken cancellationToken)
    {
        await using var translation = new TextTranslationService(_translationSettings, _settings.Endpoint, _apiKey);
        return await translation.TranslateAsync(text, targetLanguage, cancellationToken).ConfigureAwait(false) ?? text;
    }

    private async Task ProcessChunkOnPremiseAsync(byte[] audioData, CancellationToken cancellationToken)
    {
        var requestUri = _settings.Endpoint!.TrimEnd('/');
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Add("x-api-key", _apiKey);

        using var content = new MultipartFormDataContent();
        using var wavStream = new MemoryStream();
        WriteWavHeader(wavStream, audioData.Length);
        await wavStream.WriteAsync(audioData, cancellationToken);
        wavStream.Position = 0;

        var audioContent = new StreamContent(wavStream);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(audioContent, "file", "audio.wav");

        content.Add(new StringContent(_currentLanguage), "language");
        content.Add(new StringContent(""), "projectId");
        if (_settings.TranscriptionPromptId.HasValue)
            content.Add(new StringContent(_settings.TranscriptionPromptId.Value.ToString()), "transcriptionPromptId");
        if (_settings.FormattingPromptId.HasValue)
            content.Add(new StringContent(_settings.FormattingPromptId.Value.ToString()), "formattingPromptId");
        content.Add(new StringContent(_settings.ProviderId?.ToString(CultureInfo.InvariantCulture) ?? ""), "providerId");
        content.Add(new StringContent("false"), "stream");
        content.Add(new StringContent(_settings.EnableSpeakerDiarization ? "true" : "false"), "enableSpeakerDiarization");
        content.Add(new StringContent(Environment.UserName), "externalUser");

        request.Content = content;
        var headers = new Dictionary<string, string> { { "x-api-key", _apiKey } };
        RequestLogging?.Invoke(this, ("POST", requestUri, headers, BuildRequestLogBody(audioData.Length, prompt: null)));

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            ResponseLogging?.Invoke(this, ((int)response.StatusCode, response.ReasonPhrase ?? "", new Dictionary<string, string>(), responseBody));

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = $"Server error {(int)response.StatusCode}: {response.ReasonPhrase}";
                try
                {
                    var err = JsonSerializer.Deserialize<PersonalApiTranscriptionResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (!string.IsNullOrWhiteSpace(err?.ErrorMessage))
                        errorMsg = $"Server error: {err.ErrorMessage}";
                }
                catch
                {
                    if (!string.IsNullOrWhiteSpace(responseBody) && responseBody.Length < 200)
                        errorMsg = $"Server error: {responseBody}";
                }
                ErrorLogging?.Invoke(this, (errorMsg, null));
                return;
            }

            var transcriptionResponse = JsonSerializer.Deserialize<PersonalApiTranscriptionResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (transcriptionResponse?.Success == true && !string.IsNullOrWhiteSpace(transcriptionResponse.Content))
                EmitTranscriptionIfValid(transcriptionResponse.Content);
            else if (transcriptionResponse?.Success == false)
                ErrorLogging?.Invoke(this, (transcriptionResponse.ErrorMessage ?? "Unknown error", null));
        }
        catch (Exception ex)
        {
            if (ex is TaskCanceledException) return;
            ErrorLogging?.Invoke(this, ("Error sending/parsing chunk", ex));
        }
    }

    private void EmitTranscriptionIfValid(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var trimmed = text.Trim();
        if (IsHallucination(trimmed))
        {
            ErrorLogging?.Invoke(this, ($"Hallucination detected and filtered: {trimmed}", null));
            return;
        }
        if (trimmed.Equals(_lastTranscription, StringComparison.OrdinalIgnoreCase))
        {
            ErrorLogging?.Invoke(this, ($"Repetition filtered: {trimmed}", null));
            return;
        }
        _lastTranscription = trimmed;
        TranscriptionReceived?.Invoke(this, trimmed);
    }

    private static string? TryParseApiErrorMessage(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return null;
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
                return message.GetString();
        }
        catch { /* ignore parse errors */ }
        return null;
    }

    private bool IsHallucination(string text)
    {
        var hallucinations = new[]
        {
            "Sous-titres réalisés para la communauté d'Amara.org",
            "Sous-titres réalisés par la communauté d'Amara.org",
            "Sous-titrage ST'",
            "Amara.org",
            "MBC"
        };

        if (string.IsNullOrWhiteSpace(text)) return true;

        foreach (var h in hallucinations)
        {
            if (text.Contains(h, StringComparison.OrdinalIgnoreCase)) return true;
        }

        if (HasMixedScripts(text)) return true;
        if (HasExcessiveSpecialChars(text)) return true;

        return false;
    }

    private static bool HasMixedScripts(string text)
    {
        bool hasLatin = false, hasHebrew = false, hasArabic = false, hasCJK = false, hasCyrillic = false, hasOther = false;
        foreach (char c in text)
        {
            if (!char.IsLetter(c)) continue;
            var codePoint = (int)c;
            if ((codePoint >= 0x0000 && codePoint <= 0x024F) || (codePoint >= 0x1E00 && codePoint <= 0x1EFF)) hasLatin = true;
            else if (codePoint >= 0x0590 && codePoint <= 0x05FF) hasHebrew = true;
            else if (codePoint >= 0x0600 && codePoint <= 0x06FF) hasArabic = true;
            else if ((codePoint >= 0x4E00 && codePoint <= 0x9FFF) || (codePoint >= 0x3400 && codePoint <= 0x4DBF) || (codePoint >= 0x3040 && codePoint <= 0x309F) || (codePoint >= 0x30A0 && codePoint <= 0x30FF)) hasCJK = true;
            else if (codePoint >= 0x0400 && codePoint <= 0x04FF) hasCyrillic = true;
            else hasOther = true;
        }
        int n = (hasLatin ? 1 : 0) + (hasHebrew ? 1 : 0) + (hasArabic ? 1 : 0) + (hasCJK ? 1 : 0) + (hasCyrillic ? 1 : 0) + (hasOther ? 1 : 0);
        return n >= 3;
    }

    private static bool HasExcessiveSpecialChars(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        int special = 0, letters = 0;
        foreach (char c in text)
        {
            if (char.IsLetter(c)) letters++;
            else if (!char.IsWhiteSpace(c) && !char.IsPunctuation(c) && !char.IsDigit(c)) special++;
        }
        int total = text.Length;
        if (total == 0) return false;
        double specialRatio = (double)special / total;
        double letterRatio = (double)letters / total;
        return specialRatio > 0.3 && letterRatio < 0.4;
    }

    /// <summary>
    /// Formats a diagnostic body for <see cref="RequestLogging"/> that surfaces the
    /// language actually sent to the API and whether a prompt was included. Useful
    /// when debugging why a given transcription came back in an unexpected language.
    /// </summary>
    private string BuildRequestLogBody(int audioLength, string? prompt, TranscriptionRequestPlan? plan = null)
    {
        var languageSent = plan?.InputLanguageHint
            ?? (_settings.OmitTranscriptionLanguage
                ? "<omitted>"
                : (!string.IsNullOrWhiteSpace(_currentLanguage) ? _currentLanguage : _settings.Language));
        var promptInfo = string.IsNullOrWhiteSpace(prompt)
            ? "<none>"
            : $"{prompt!.Length} chars";
        var strategy = plan?.Mode.ToString() ?? "Transcribe";
        var endpointPath = plan?.EndpointPath ?? "transcriptions";
        return $"Chunk size: {audioLength} bytes (WAV header added); language={languageSent}; mode={_settings.LanguageMode}; strategy={strategy}; endpoint={endpointPath}; prompt={promptInfo}";
    }

    private static void WriteWavHeader(Stream stream, int dataLength)
        => WavWriter.WriteHeader(stream, dataLength);

    /// <summary>
    /// Transcribes a single audio chunk and returns the raw text (no event emission).
    /// Used by the meeting transcription pipeline.
    /// </summary>
    public async Task<string?> TranscribeChunkAsync(byte[] audioData, CancellationToken cancellationToken)
    {
        if (audioData == null || audioData.Length < 1000)
            return null;

        if (UsesRealtimeTranscription)
            return null;

        if (UseOnPremiseApi)
            return await TranscribeChunkOnPremiseDirectAsync(audioData, cancellationToken).ConfigureAwait(false);

        var plan = ResolveHttpPlan();
        LastTranscriptionMode = plan.Mode;
        OutputAlreadyApplied = false;

        var text = await SendOpenAiAudioRequestAsync(audioData, plan, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text) || IsHallucination(text))
            return null;

        if (plan.Mode == TranscriptionApiMode.PostTranslationFallback
            && !string.IsNullOrWhiteSpace(plan.OutputLanguage))
        {
            text = await ApplyPostTranslationFallbackAsync(text, plan.OutputLanguage!, cancellationToken).ConfigureAwait(false);
            OutputAlreadyApplied = !string.IsNullOrWhiteSpace(text);
        }
        else
        {
            OutputAlreadyApplied = TranscriptionStrategyResolver.OutputAppliedByStt(plan.Mode);
        }

        return text;
    }

    private async Task<string?> TranscribeChunkOnPremiseDirectAsync(byte[] audioData, CancellationToken cancellationToken)
    {
        var requestUri = _settings.Endpoint!.TrimEnd('/');
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Add("x-api-key", _apiKey);

        using var content = new MultipartFormDataContent();
        using var wavStream = new MemoryStream();
        WriteWavHeader(wavStream, audioData.Length);
        await wavStream.WriteAsync(audioData, cancellationToken);
        wavStream.Position = 0;

        var audioContent = new StreamContent(wavStream);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(audioContent, "file", "audio.wav");
        content.Add(new StringContent(_currentLanguage), "language");
        content.Add(new StringContent(""), "projectId");
        if (_settings.TranscriptionPromptId.HasValue)
            content.Add(new StringContent(_settings.TranscriptionPromptId.Value.ToString()), "transcriptionPromptId");
        if (_settings.FormattingPromptId.HasValue)
            content.Add(new StringContent(_settings.FormattingPromptId.Value.ToString()), "formattingPromptId");
        content.Add(new StringContent(_settings.ProviderId?.ToString(CultureInfo.InvariantCulture) ?? ""), "providerId");
        content.Add(new StringContent("false"), "stream");
        content.Add(new StringContent("true"), "enableSpeakerDiarization");
        content.Add(new StringContent(Environment.UserName), "externalUser");

        request.Content = content;
        var headers = new Dictionary<string, string> { { "x-api-key", _apiKey } };
        RequestLogging?.Invoke(this, ("POST", requestUri, headers, BuildRequestLogBody(audioData.Length, prompt: null)));

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            return null;

        var result = JsonSerializer.Deserialize<PersonalApiTranscriptionResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (result?.Success == true && !string.IsNullOrWhiteSpace(result.Content))
            return result.Content;

        return null;
    }

    public Task StopAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        try
        {
            _realtimeCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // ignored
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        try
        {
            _realtimeCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // ignored
        }

        _realtimeCts?.Dispose();
        _realtimeCts = null;
        if (_realtimeTask != null)
        {
            try
            {
                await Task.WhenAny(_realtimeTask, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            _realtimeTask = null;
        }

        _httpClient.Dispose();
    }

    private sealed record OpenAITranscriptionResponse(string? Text);
    private sealed record PersonalApiTranscriptionResponse(
        bool Success,
        string? Content,
        string? ErrorMessage,
        int? Provider,
        string? ExecutionDuration,
        object? Metadata,
        int? RequestId,
        int? InputTokens,
        int? OutputTokens,
        double? AudioDuration
    );
}
