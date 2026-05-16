using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Koli.Config;

namespace Koli.Services;

/// <summary>
/// OpenAI Realtime WebSocket client for live transcription over <c>wss://api.openai.com/v1/realtime?model=...</c>.
/// The socket opens a <c>realtime</c> session; the first <c>session.update</c> must use <c>session.type = realtime</c>
/// and configure <c>session.audio.input.transcription</c> (a <c>transcription</c>-typed update is rejected on this connection).
/// Expects mono PCM16 LE chunks at 16 kHz (same as app capture); audio is resampled to 24 kHz before sending.
/// </summary>
public sealed class OpenAiRealtimeTranscriptionSession : IAsyncDisposable
{
    private readonly AzureOpenAISettings _settings;
    private readonly string _apiKey;
    private ClientWebSocket? _webSocket;
    private readonly ConcurrentDictionary<string, StringBuilder> _partialByItem = new(StringComparer.Ordinal);
    private int _completedCount;
    private int _speechStartedCount;
    private int _committedCount;
    /// <summary>Maximum time to wait after the final <c>commit</c> for outstanding <c>completed</c> events.</summary>
    private static readonly TimeSpan FinalCompletionTimeout = TimeSpan.FromSeconds(10);

    public OpenAiRealtimeTranscriptionSession(AzureOpenAISettings settings, string apiKey)
    {
        _settings = settings;
        _apiKey = apiKey;
    }

    public static string BuildWebSocketUrl(AzureOpenAISettings settings)
    {
        var model = Uri.EscapeDataString(settings.Model.Trim());
        return $"wss://api.openai.com/v1/realtime?model={model}";
    }

    /// <summary>
    /// Linear PCM16 mono: 16 kHz → 24 kHz (duration-preserving).
    /// </summary>
    public static byte[] ResamplePcm16Mono16kTo24k(ReadOnlySpan<byte> pcm16k)
    {
        if (pcm16k.Length < 2 || pcm16k.Length % 2 != 0)
            return Array.Empty<byte>();

        var sampleCount16 = pcm16k.Length / 2;
        var sampleCount24 = (int)Math.Round(sampleCount16 * 24000.0 / 16000.0);
        if (sampleCount24 <= 0)
            return Array.Empty<byte>();

        var output = new byte[sampleCount24 * 2];
        for (var j = 0; j < sampleCount24; j++)
        {
            var srcPos = j * (16000.0 / 24000.0);
            var i0 = (int)Math.Floor(srcPos);
            var i1 = Math.Min(i0 + 1, sampleCount16 - 1);
            var frac = srcPos - i0;
            var s0 = ReadLeI16(pcm16k, i0 * 2);
            var s1 = ReadLeI16(pcm16k, i1 * 2);
            var v = (short)(s0 + frac * (s1 - s0));
            var o = j * 2;
            output[o] = (byte)(v & 0xff);
            output[o + 1] = (byte)((v >> 8) & 0xff);
        }

        return output;
    }

    private static short ReadLeI16(ReadOnlySpan<byte> buf, int offset) =>
        (short)(buf[offset] | (buf[offset + 1] << 8));

    public async Task RunAsync(
        IAsyncEnumerable<byte[]> pcm16kChunks,
        Action<RealtimeTranscriptEventArgs> onTranscript,
        Action<string, Exception?> onError,
        Action<string, string?>? traceSend,
        Action<string, string?>? traceRecv,
        CancellationToken cancellationToken)
    {
        _webSocket = new ClientWebSocket();
        _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");

        var url = BuildWebSocketUrl(_settings);
        traceSend?.Invoke("CONNECT", url);

        try
        {
            await _webSocket.ConnectAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            onError($"Realtime WebSocket connect failed: {ex.Message}", ex);
            return;
        }

        var sessionJson = BuildSessionUpdateJson(_settings);
        await SendJsonAsync(sessionJson, traceSend, cancellationToken).ConfigureAwait(false);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var readBuffer = new byte[64 * 1024];
        var receiveTask = ReceiveLoopAsync(
            readBuffer,
            onTranscript,
            onError,
            traceRecv,
            linked.Token);

        try
        {
            await foreach (var chunk in pcm16kChunks.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (chunk.Length == 0)
                    continue;
                var pcm24 = ResamplePcm16Mono16kTo24k(chunk);
                if (pcm24.Length == 0)
                    continue;

                var append = new JsonObject
                {
                    ["type"] = "input_audio_buffer.append",
                    ["audio"] = Convert.ToBase64String(pcm24)
                };
                await SendJsonAsync(append.ToJsonString(), traceSend, cancellationToken).ConfigureAwait(false);
            }

            await SendJsonAsync(
                """{"type":"input_audio_buffer.commit"}""",
                traceSend,
                cancellationToken).ConfigureAwait(false);

            // Wait for all outstanding completed events (one per detected utterance / commit).
            // Whisper Realtime only emits transcripts at commit time, so a fixed 2.5 s window
            // was racing the close and dropping the final transcript.
            await WaitForOutstandingCompletionsAsync(FinalCompletionTimeout).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // StopRealtimeTranscriptionAsync, audio stream ended, or linked shutdown.
        }
        catch (Exception ex)
        {
            onError($"Realtime send loop error: {ex.Message}", ex);
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
    }

    /// <summary>
    /// Builds the initial <c>session.update</c> for <c>/v1/realtime</c> WebSocket connections.
    /// </summary>
    public static string BuildSessionUpdateJson(AzureOpenAISettings settings)
    {
        var format = new JsonObject
        {
            ["type"] = "audio/pcm",
            ["rate"] = 24000
        };

        var transcription = new JsonObject
        {
            ["model"] = settings.Model.Trim()
        };
        if (!string.IsNullOrWhiteSpace(settings.Language) && !settings.OmitTranscriptionLanguage)
            transcription["language"] = settings.Language.Trim();

        var input = new JsonObject
        {
            ["format"] = format,
            ["transcription"] = transcription
        };

        // Whisper Realtime: GA realtime sessions do not allow turn_detection on this input (see API errors).
        // Other Realtime models: server VAD matches the transcription guide defaults.
        if (!OpenAiModelProfiles.IsGptRealtimeWhisperFamily(settings.Model))
        {
            input["turn_detection"] = new JsonObject
            {
                ["type"] = "server_vad",
                ["threshold"] = 0.5,
                ["prefix_padding_ms"] = 300,
                ["silence_duration_ms"] = 500
            };
        }

        var audio = new JsonObject { ["input"] = input };
        var session = new JsonObject
        {
            ["type"] = "realtime",
            ["audio"] = audio
        };

        var root = new JsonObject
        {
            ["type"] = "session.update",
            ["session"] = session
        };

        return root.ToJsonString();
    }

    /// <summary>
    /// Polls the per-item buffers and the speech_started/completed counters until either every
    /// detected utterance has produced a <c>completed</c> transcription event, or <paramref name="maxWait"/>
    /// elapses. Avoids a fixed sleep that would either truncate the last transcript or stall the UI.
    /// </summary>
    private async Task WaitForOutstandingCompletionsAsync(TimeSpan maxWait)
    {
        var deadline = DateTime.UtcNow + maxWait;
        // Always give the server a small window to start emitting (some models send completed
        // synchronously after commit, others need a few hundred ms).
        await Task.Delay(300, CancellationToken.None).ConfigureAwait(false);

        while (DateTime.UtcNow < deadline)
        {
            var pendingPartials = !_partialByItem.IsEmpty;
            var started = Volatile.Read(ref _speechStartedCount);
            var completed = Volatile.Read(ref _completedCount);
            var committed = Volatile.Read(ref _committedCount);

            // No speech ever started AND the server already acknowledged at least one commit:
            // user pressed stop without saying anything — no completed event will ever come.
            if (started == 0 && committed >= 1)
                return;

            // Otherwise, every detected utterance must produce a completed event.
            if (started > 0 && completed >= started && !pendingPartials)
                return;

            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
                return;

            await Task.Delay(150, CancellationToken.None).ConfigureAwait(false);
        }
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
            // Use None: a cancelled recording token was aborting mid-send and leaving the socket in Aborted state.
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (WebSocketException)
        {
            // Closed/aborted by peer or concurrent shutdown — not actionable for the caller.
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

                if (RealtimeTranscriptEventParser.TryParseErrorMessage(message, out var errMsg))
                {
                    onError($"Realtime API error: {errMsg}", null);
                    continue;
                }

                // Lightweight envelope sniff so we can count utterances without a full parser.
                // Used by WaitForOutstandingCompletionsAsync to know when it is safe to close
                // (committed model that hasn't yet sent its transcript).
                if (message.Contains("\"input_audio_buffer.speech_started\"", StringComparison.Ordinal))
                    Interlocked.Increment(ref _speechStartedCount);
                else if (message.Contains("\"input_audio_buffer.committed\"", StringComparison.Ordinal))
                    Interlocked.Increment(ref _committedCount);

                if (!RealtimeTranscriptEventParser.TryParseTranscriptionEvent(message, out var ev))
                    continue;

                if (ev.Type == RealtimeTranscriptEventParser.DeltaType)
                {
                    var itemKey = string.IsNullOrEmpty(ev.ItemId) ? "_" : ev.ItemId;
                    var sb = _partialByItem.GetOrAdd(itemKey, _ => new StringBuilder());
                    var delta = ev.Delta ?? "";
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
                else if (ev.Type == RealtimeTranscriptEventParser.CompletedType)
                {
                    var itemKey = string.IsNullOrEmpty(ev.ItemId) ? "_" : ev.ItemId;
                    var finalText = ev.Transcript ?? "";
                    _partialByItem.TryRemove(itemKey, out _);
                    Interlocked.Increment(ref _completedCount);
                    onTranscript(new RealtimeTranscriptEventArgs
                    {
                        ItemId = itemKey,
                        Text = finalText,
                        IsFinal = true,
                        Delta = null
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal
        }
        catch (Exception ex)
        {
            onError($"Realtime receive loop error: {ex.Message}", ex);
        }
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
                // ignored
            }

            _webSocket.Dispose();
            _webSocket = null;
        }

        _partialByItem.Clear();
    }
}
