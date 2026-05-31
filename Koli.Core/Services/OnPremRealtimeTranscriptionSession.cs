using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Koli.Config;

namespace Koli.Services;

/// <summary>
/// Ai Nexus live STT over <c>wss://…/api/ai/realtime/transcribe</c> (PCM16 mono 24 kHz, JSON envelope).
/// Expects capture at 16 kHz; audio is resampled before send (same as OpenAI Realtime).
/// </summary>
public sealed class OnPremRealtimeTranscriptionSession : IAsyncDisposable
{
    private const int TargetChunkBytes16k = 3200; // ~100 ms at 16 kHz mono PCM16

    private readonly AzureOpenAISettings _settings;
    private readonly string _apiKey;
    private readonly string _language;
    private ClientWebSocket? _webSocket;
    private readonly ConcurrentDictionary<string, StringBuilder> _partialByItem = new(StringComparer.Ordinal);
    private volatile bool _sessionReady;

    public OnPremRealtimeTranscriptionSession(AzureOpenAISettings settings, string apiKey, string language)
    {
        _settings = settings;
        _apiKey = apiKey;
        _language = language;
    }

    /// <summary>True when the WebSocket connected (session may still fail later).</summary>
    public bool Connected { get; private set; }

    public async Task<bool> RunAsync(
        IAsyncEnumerable<byte[]> pcm16kChunks,
        Action<RealtimeTranscriptEventArgs> onTranscript,
        Action<string, Exception?> onError,
        Action<string, string?>? traceSend,
        Action<string, string?>? traceRecv,
        CancellationToken cancellationToken)
    {
        var wsUrl = OpenAiModelProfiles.BuildOnPremRealtimeWebSocketUrl(_settings);
        _webSocket = new ClientWebSocket();
        _webSocket.Options.SetRequestHeader("x-api-key", _apiKey);

        traceSend?.Invoke("CONNECT", wsUrl);

        try
        {
            await _webSocket.ConnectAsync(new Uri(wsUrl), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            onError($"On-prem realtime WebSocket connect failed: {ex.Message}", ex);
            return false;
        }

        Connected = true;

        var startJson = BuildStartMessage(_settings, _language);
        await SendJsonAsync(startJson, traceSend, cancellationToken).ConfigureAwait(false);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var readBuffer = new byte[64 * 1024];
        var receiveTask = ReceiveLoopAsync(readBuffer, onTranscript, onError, traceRecv, linked.Token);

        var pcmBuffer = new MemoryStream();
        try
        {
            await foreach (var chunk in pcm16kChunks.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (chunk.Length == 0)
                    continue;

                pcmBuffer.Write(chunk, 0, chunk.Length);

                while (pcmBuffer.Length >= TargetChunkBytes16k)
                {
                    if (!_sessionReady)
                    {
                        await WaitForSessionReadyAsync(linked.Token).ConfigureAwait(false);
                        if (!_sessionReady)
                            break;
                    }

                    var window = ExtractWindow(pcmBuffer, TargetChunkBytes16k);
                    await SendAudioChunkAsync(window, traceSend, cancellationToken).ConfigureAwait(false);
                }
            }

            if (pcmBuffer.Length > 0 && _sessionReady)
            {
                var tail = pcmBuffer.ToArray();
                await SendAudioChunkAsync(tail, traceSend, cancellationToken).ConfigureAwait(false);
            }

            if (_sessionReady)
                await SendJsonAsync("""{"type":"stop"}""", traceSend, cancellationToken).ConfigureAwait(false);

            await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal stop
        }
        catch (Exception ex)
        {
            onError($"On-prem realtime send loop error: {ex.Message}", ex);
        }
        finally
        {
            linked.Cancel();
            try
            {
                await receiveTask.ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            if (_webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "done",
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }
        }

        return Connected;
    }

    public static string BuildStartMessage(AzureOpenAISettings settings, string language)
    {
        var start = new JsonObject { ["type"] = "start" };

        var providerId = settings.StreamingProviderId ?? settings.ProviderId;
        if (providerId.HasValue)
            start["providerId"] = providerId.Value;

        var lang = ResolveStartLanguage(settings, language);
        if (lang != null)
            start["language"] = lang;

        start["externalUser"] = Environment.UserName;

        return start.ToJsonString();
    }

    public static string? ResolveStartLanguage(AzureOpenAISettings settings, string currentLanguage)
    {
        if (settings.OmitTranscriptionLanguage)
            return "auto";

        var lang = !string.IsNullOrWhiteSpace(currentLanguage)
            ? currentLanguage.Trim()
            : settings.Language?.Trim();

        if (string.IsNullOrWhiteSpace(lang))
            return "auto";

        return lang;
    }

    private async Task WaitForSessionReadyAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!_sessionReady && DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
                return;
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendAudioChunkAsync(byte[] pcm16k, Action<string, string?>? traceSend, CancellationToken cancellationToken)
    {
        var pcm24 = OpenAiRealtimeTranscriptionSession.ResamplePcm16Mono16kTo24k(pcm16k);
        if (pcm24.Length == 0)
            return;

        var audio = new JsonObject
        {
            ["type"] = "audio",
            ["data"] = Convert.ToBase64String(pcm24)
        };
        await SendJsonAsync(audio.ToJsonString(), traceSend, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendJsonAsync(string json, Action<string, string?>? traceSend, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            return;

        traceSend?.Invoke("SEND", json);
        var bytes = Encoding.UTF8.GetBytes(json);
        try
        {
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (WebSocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task ReceiveLoopAsync(
        byte[] buffer,
        Action<RealtimeTranscriptEventArgs> onTranscript,
        Action<string, Exception?> onError,
        Action<string, string?>? traceRecv,
        CancellationToken cancellationToken)
    {
        if (_webSocket == null)
            return;

        try
        {
            while (!cancellationToken.IsCancellationRequested
                   && _webSocket.State == WebSocketState.Open)
            {
                var message = await ReceiveFullTextAsync(_webSocket, buffer, cancellationToken).ConfigureAwait(false);
                if (message == null)
                    break;

                traceRecv?.Invoke("RECV", message);
                HandleServerMessage(message, onTranscript, onError);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            onError($"On-prem realtime receive loop error: {ex.Message}", ex);
        }
    }

    private void HandleServerMessage(
        string message,
        Action<RealtimeTranscriptEventArgs> onTranscript,
        Action<string, Exception?> onError)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(message);
        }
        catch (JsonException)
        {
            return;
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("type", out var typeEl))
                return;

            var type = typeEl.GetString();
            if (string.IsNullOrEmpty(type))
                return;

            switch (type)
            {
                case "session_ready":
                    _sessionReady = true;
                    break;

                case "partial":
                    EmitPartial(doc.RootElement, onTranscript);
                    break;

                case "final":
                    EmitFinal(doc.RootElement, onTranscript);
                    break;

                case "error":
                    var err = doc.RootElement.TryGetProperty("message", out var msgEl)
                        ? msgEl.GetString()
                        : message;
                    onError($"On-prem realtime error: {err ?? "unknown"}", null);
                    break;

                case "done":
                    break;
            }
        }
    }

    private void EmitPartial(JsonElement root, Action<RealtimeTranscriptEventArgs> onTranscript)
    {
        if (!root.TryGetProperty("text", out var textEl))
            return;

        var delta = textEl.GetString() ?? "";
        if (string.IsNullOrEmpty(delta))
            return;

        var itemKey = root.TryGetProperty("itemId", out var idEl) && idEl.ValueKind == JsonValueKind.String
            ? idEl.GetString() ?? "_"
            : "_";

        var sb = _partialByItem.GetOrAdd(itemKey, _ => new StringBuilder());
        sb.Append(delta);
        var text = sb.ToString();
        onTranscript(new RealtimeTranscriptEventArgs
        {
            ItemId = itemKey,
            Text = text,
            IsFinal = false,
            Delta = delta
        });
    }

    private void EmitFinal(JsonElement root, Action<RealtimeTranscriptEventArgs> onTranscript)
    {
        if (!root.TryGetProperty("text", out var textEl))
            return;

        var finalText = textEl.GetString() ?? "";
        if (string.IsNullOrWhiteSpace(finalText))
            return;

        var itemKey = root.TryGetProperty("itemId", out var idEl) && idEl.ValueKind == JsonValueKind.String
            ? idEl.GetString() ?? "_"
            : "_";

        _partialByItem.TryRemove(itemKey, out _);
        onTranscript(new RealtimeTranscriptEventArgs
        {
            ItemId = itemKey,
            Text = finalText,
            IsFinal = true,
            Delta = null
        });
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

    private static async Task<string?> ReceiveFullTextAsync(ClientWebSocket ws, byte[] buffer, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        var isText = false;
        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;
            if (result.MessageType == WebSocketMessageType.Text)
            {
                isText = true;
                ms.Write(buffer, 0, result.Count);
            }
        } while (!result.EndOfMessage);

        if (!isText || ms.Length == 0)
            return "";
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public async ValueTask DisposeAsync()
    {
        if (_webSocket != null)
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "dispose",
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch
            {
            }

            _webSocket.Dispose();
            _webSocket = null;
        }

        _partialByItem.Clear();
    }
}
