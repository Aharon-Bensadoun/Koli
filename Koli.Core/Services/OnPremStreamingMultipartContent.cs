using System.Globalization;
using System.Net.Http.Headers;
using Koli.Config;

namespace Koli.Services;

/// <summary>
/// Builds on-prem <c>queryAudio</c> multipart bodies with the same fields as batch mode.
/// API assumption: POST multipart with <c>stream=true</c> plus language, providerId,
/// transcriptionPromptId, formattingPromptId, enableSpeakerDiarization, externalUser, projectId.
/// </summary>
internal static class OnPremStreamingMultipartContent
{
    public static MultipartFormDataContent Create(
        byte[] pcmAudio,
        AzureOpenAISettings settings,
        string language,
        bool stream,
        int? providerIdOverride = null)
    {
        var content = new MultipartFormDataContent();

        using var wavStream = new MemoryStream();
        WavWriter.WriteHeader(wavStream, pcmAudio.Length);
        wavStream.Write(pcmAudio, 0, pcmAudio.Length);
        var wavBytes = wavStream.ToArray();

        var audioContent = new ByteArrayContent(wavBytes);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(audioContent, "file", "audio.wav");

        content.Add(new StringContent(language), "language");
        content.Add(new StringContent(""), "projectId");
        if (settings.TranscriptionPromptId.HasValue)
            content.Add(new StringContent(settings.TranscriptionPromptId.Value.ToString(CultureInfo.InvariantCulture)), "transcriptionPromptId");
        if (settings.FormattingPromptId.HasValue)
            content.Add(new StringContent(settings.FormattingPromptId.Value.ToString(CultureInfo.InvariantCulture)), "formattingPromptId");

        var providerId = providerIdOverride ?? settings.ProviderId;
        content.Add(new StringContent(providerId?.ToString(CultureInfo.InvariantCulture) ?? ""), "providerId");
        content.Add(new StringContent(stream ? "true" : "false"), "stream");
        content.Add(new StringContent(settings.EnableSpeakerDiarization ? "true" : "false"), "enableSpeakerDiarization");
        content.Add(new StringContent(Environment.UserName), "externalUser");

        return content;
    }

    public static string BuildRequestLogBody(int pcmLength, bool stream) =>
        $"Chunk size: {pcmLength} bytes (WAV header added); stream={stream.ToString().ToLowerInvariant()}; on-prem queryAudio";
}
