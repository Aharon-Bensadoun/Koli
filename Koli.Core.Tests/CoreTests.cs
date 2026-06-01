using System.Security.Cryptography;
using System.Text;
using Koli.Config;
using Koli.Platform;
using Koli.Services;
using Xunit;

namespace Koli.Core.Tests;

public class InputLanguageServiceTests
{
    [Theory]
    [InlineData(0x0409, "en")]
    [InlineData(0x040C, "fr")]
    [InlineData(0x040D, "he")]
    [InlineData(0x0411, "en")]
    public void MapLanguageIdToCode_MapsCommonLayouts(ushort langId, string expected)
    {
        Assert.Equal(expected, InputLanguageService.MapLanguageIdToCode(langId));
    }
}

public class SecureSettingsStoreTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "koli-tests-" + Guid.NewGuid());

    public SecureSettingsStoreTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("sk-proj-yourapikey", true)]
    [InlineData("YOUR_API_KEY_HERE", true)]
    [InlineData("sk-live-real-key", false)]
    public void IsPlaceholderApiKey_DetectsTemplateValues(string? apiKey, bool expected)
    {
        Assert.Equal(expected, SecureSettingsStore.IsPlaceholderApiKey(apiKey));
    }

    [Fact]
    public void IsApiKeyConfigured_ReturnsTrueWhenSecretFileContainsValidKey()
    {
        var store = new SecureSettingsStore(_tempDirectory);
        WriteEncryptedSecret(_tempDirectory, "sk-live-real-key");

        Assert.True(store.IsApiKeyConfigured(null));
        Assert.True(store.IsApiKeyConfigured("sk-proj-yourapikey"));
    }

    [Fact]
    public void IsApiKeyConfigured_IgnoresPlaceholderInJsonAndSecret()
    {
        var store = new SecureSettingsStore(_tempDirectory);
        WriteEncryptedSecret(_tempDirectory, "sk-proj-yourapikey");

        Assert.False(store.IsApiKeyConfigured(null));
        Assert.False(store.IsApiKeyConfigured("sk-proj-yourapikey"));
        Assert.False(store.IsApiKeyConfigured("YOUR_API_KEY_HERE"));
    }

    [Fact]
    public async Task ResolveApiKeyAsync_OverwritesPlaceholderSecretWithConfiguredKey()
    {
        var store = new SecureSettingsStore(_tempDirectory);
        WriteEncryptedSecret(_tempDirectory, "sk-proj-yourapikey");

        var key = await store.ResolveApiKeyAsync("sk-live-real-key", CancellationToken.None);

        Assert.Equal("sk-live-real-key", key);
        Assert.True(store.IsApiKeyConfigured(null));
        var resolvedFromSecret = await store.ResolveApiKeyAsync(null, CancellationToken.None);
        Assert.Equal("sk-live-real-key", resolvedFromSecret);
    }

    private static void WriteEncryptedSecret(string baseDirectory, string apiKey)
    {
        var secretPath = Path.Combine(baseDirectory, "Config", "api.secret");
        Directory.CreateDirectory(Path.GetDirectoryName(secretPath)!);
        var payload = ProtectedData.Protect(Encoding.UTF8.GetBytes(apiKey), optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(secretPath, payload);
    }

    [Fact]
    public void IsApiKeyConfigured_ReturnsFalseForMissingKeyAndSecret()
    {
        var store = new SecureSettingsStore(_tempDirectory);

        Assert.False(store.IsApiKeyConfigured(null));
        Assert.False(store.IsApiKeyConfigured(""));
        Assert.False(store.IsApiKeyConfigured("sk-proj-yourapikey"));
    }

    [Fact]
    public void IsApiKeyConfigured_ReturnsTrueForConfiguredKey()
    {
        var store = new SecureSettingsStore(_tempDirectory);

        Assert.True(store.IsApiKeyConfigured("sk-live-real-key"));
    }

    [Fact]
    public async Task TryResolveDisplayKeyAsync_ReturnsConfiguredKeyFromJson()
    {
        var store = new SecureSettingsStore(_tempDirectory);

        var key = await store.TryResolveDisplayKeyAsync("sk-live-real-key");

        Assert.Equal("sk-live-real-key", key);
    }

    [Fact]
    public async Task TryResolveDisplayKeyAsync_ReturnsNullWhenNotConfigured()
    {
        var store = new SecureSettingsStore(_tempDirectory);

        var key = await store.TryResolveDisplayKeyAsync(null);

        Assert.Null(key);
    }
}

public class OpenAiModelProfilesTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("https://api.openai.com", false)]
    [InlineData("https://corp.example.com/api/AI/queryAudio", true)]
    public void IsOnPremiseStyleEndpoint_DetectsEndpointStyle(string endpoint, bool expected)
    {
        Assert.Equal(expected, OpenAiModelProfiles.IsOnPremiseStyleEndpoint(endpoint));
    }

    [Theory]
    [InlineData("gpt-realtime-whisper", true)]
    [InlineData("whisper-1", false)]
    public void IsRealtimeTranscriptionModel_DetectsModel(string model, bool expected)
    {
        Assert.Equal(expected, OpenAiModelProfiles.IsRealtimeTranscriptionModel(model));
    }

    [Fact]
    public void CreateMeetingTranscriptionSettings_ForcesRealtimeOnOpenAiCloud()
    {
        var source = new AzureOpenAISettings { Endpoint = "", Model = "whisper-1" };
        var meeting = OpenAiModelProfiles.CreateMeetingTranscriptionSettings(source);
        Assert.Equal("gpt-realtime-whisper", meeting.Model);
        Assert.True(OpenAiModelProfiles.ShouldUseRealtimeTranscription(meeting));
    }

    [Fact]
    public void CreateMeetingTranscriptionSettings_KeepsExistingRealtimeModel()
    {
        var source = new AzureOpenAISettings { Endpoint = "", Model = "gpt-realtime" };
        var meeting = OpenAiModelProfiles.CreateMeetingTranscriptionSettings(source);
        Assert.Equal("gpt-realtime", meeting.Model);
    }

    [Theory]
    [InlineData("gpt-realtime-whisper", "gpt-realtime")]
    [InlineData("gpt-realtime", "gpt-realtime")]
    [InlineData("whisper-1", "gpt-realtime")]
    public void ResolveRealtimeSessionModel_MapsWhisperToRealtimeSession(string configured, string expected)
    {
        Assert.Equal(expected, OpenAiModelProfiles.ResolveRealtimeSessionModel(configured));
    }

    [Theory]
    [InlineData("gpt-realtime-whisper", "gpt-realtime-whisper")]
    [InlineData("gpt-realtime", "gpt-realtime-whisper")]
    public void ResolveRealtimeTranscriptionModel_MapsSessionToWhisperTranscription(string configured, string expected)
    {
        Assert.Equal(expected, OpenAiModelProfiles.ResolveRealtimeTranscriptionModel(configured));
    }

    [Fact]
    public void BuildWebSocketUrl_UsesSessionModelNotWhisperTranscriptionModel()
    {
        var settings = new AzureOpenAISettings { Model = "gpt-realtime-whisper" };
        var url = OpenAiRealtimeTranscriptionSession.BuildWebSocketUrl(settings);
        Assert.Contains("model=gpt-realtime", url, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gpt-realtime-whisper", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSessionUpdateJson_PutsWhisperInTranscriptionSubModel()
    {
        var settings = new AzureOpenAISettings { Model = "gpt-realtime-whisper" };
        var json = OpenAiRealtimeTranscriptionSession.BuildSessionUpdateJson(settings);
        Assert.Contains("\"model\":\"gpt-realtime-whisper\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://corp.example.com/api/AI/queryAudio", false, false)]
    [InlineData("https://corp.example.com/api/AI/queryAudio", true, true)]
    [InlineData("https://api.openai.com", true, false)]
    [InlineData("", true, false)]
    public void CanUseOnPremStreamingTranscription_RequiresOnPremAndToggle(string endpoint, bool enabled, bool expected)
    {
        var settings = new AzureOpenAISettings { Endpoint = endpoint, EnableStreamingTranscription = enabled };
        Assert.Equal(expected, OpenAiModelProfiles.CanUseOnPremStreamingTranscription(settings));
    }

    [Theory]
    [InlineData("", false, false, false)]
    [InlineData("", false, true, true)]
    [InlineData("https://corp.example.com/api/AI/queryAudio", true, false, true)]
    [InlineData("https://corp.example.com/api/AI/queryAudio", false, false, false)]
    public void ShouldUseLiveTranscription_CombinesOpenAiAndOnPrem(
        string endpoint, bool streamingEnabled, bool realtimeModel, bool expected)
    {
        var settings = new AzureOpenAISettings
        {
            Endpoint = endpoint,
            EnableStreamingTranscription = streamingEnabled,
            Model = realtimeModel ? "gpt-realtime-whisper" : "whisper-1"
        };
        Assert.Equal(expected, OpenAiModelProfiles.ShouldUseLiveTranscription(settings));
    }

    [Fact]
    public void CreateMeetingTranscriptionSettings_KeepsOnPremStreamingSettings()
    {
        var source = new AzureOpenAISettings
        {
            Endpoint = "https://corp.example.com/api/AI/queryAudio",
            Model = "whisper-1",
            EnableStreamingTranscription = true,
            StreamingEndpoint = "https://corp.example.com/api/AI/queryAudio/stream"
        };
        var meeting = OpenAiModelProfiles.CreateMeetingTranscriptionSettings(source);
        Assert.Equal("whisper-1", meeting.Model);
        Assert.True(meeting.EnableStreamingTranscription);
        Assert.True(OpenAiModelProfiles.WillMeetingUseLiveTranscription(source));
    }

    [Fact]
    public void ResolveStreamingEndpoint_FallsBackToEndpoint()
    {
        var settings = new AzureOpenAISettings
        {
            Endpoint = "https://corp.example.com/api/AI/queryAudio",
            StreamingEndpoint = ""
        };
        Assert.Equal("https://corp.example.com/api/AI/queryAudio", OpenAiModelProfiles.ResolveStreamingEndpoint(settings));

        settings.StreamingEndpoint = "https://corp.example.com/api/AI/queryAudio/live";
        Assert.Equal("https://corp.example.com/api/AI/queryAudio/live", OpenAiModelProfiles.ResolveStreamingEndpoint(settings));
    }

    [Theory]
    [InlineData("https://ai-connector.hadassah.org.il/api/ai/queryAudio", "wss://ai-connector.hadassah.org.il/api/ai/realtime/transcribe")]
    [InlineData("https://corp.example.com/api/AI/queryAudio", "wss://corp.example.com/api/ai/realtime/transcribe")]
    public void BuildOnPremRealtimeWebSocketUrl_DerivesFromQueryAudioEndpoint(string endpoint, string expected)
    {
        var settings = new AzureOpenAISettings { Endpoint = endpoint };
        Assert.Equal(expected, OpenAiModelProfiles.BuildOnPremRealtimeWebSocketUrl(settings));
    }

    [Fact]
    public void BuildOnPremRealtimeWebSocketUrl_UsesExplicitRealtimeEndpoint()
    {
        var settings = new AzureOpenAISettings
        {
            Endpoint = "https://corp.example.com/api/ai/queryAudio",
            RealtimeEndpoint = "https://custom.example.com/api/ai/realtime/transcribe"
        };
        Assert.Equal(
            "wss://custom.example.com/api/ai/realtime/transcribe",
            OpenAiModelProfiles.BuildOnPremRealtimeWebSocketUrl(settings));
    }

    [Theory]
    [InlineData("wss://custom.example.com/api/ai/realtime/transcribe", "wss://custom.example.com/api/ai/realtime/transcribe")]
    [InlineData("ws://custom.example.com/api/ai/realtime/transcribe", "wss://custom.example.com/api/ai/realtime/transcribe")]
    public void BuildOnPremRealtimeWebSocketUrl_AlwaysUsesSecureWebSocket(string realtimeEndpoint, string expected)
    {
        var settings = new AzureOpenAISettings { RealtimeEndpoint = realtimeEndpoint };
        Assert.Equal(expected, OpenAiModelProfiles.BuildOnPremRealtimeWebSocketUrl(settings));
    }

    [Fact]
    public void BuildOnPremRealtimeWebSocketUrl_UpgradesHttpEndpointToWss()
    {
        var settings = new AzureOpenAISettings { Endpoint = "http://corp.example.com/api/ai/queryAudio" };
        Assert.Equal(
            "wss://corp.example.com/api/ai/realtime/transcribe",
            OpenAiModelProfiles.BuildOnPremRealtimeWebSocketUrl(settings));
    }

    [Theory]
    [InlineData("https://corp.example.com/api/AI/queryAudio", true, false, false)]
    [InlineData("https://corp.example.com/api/AI/queryAudio", true, true, true)]
    [InlineData("https://corp.example.com/api/AI/queryAudio", false, true, false)]
    public void CanUseOnPremHttpStreamingTranscription_RequiresFallbackFlag(
        string endpoint, bool streamingEnabled, bool httpFallback, bool expected)
    {
        var settings = new AzureOpenAISettings
        {
            Endpoint = endpoint,
            EnableStreamingTranscription = streamingEnabled,
            UseQueryAudioHttpStreamingFallback = httpFallback
        };
        Assert.Equal(expected, OpenAiModelProfiles.CanUseOnPremHttpStreamingTranscription(settings));
    }

    [Fact]
    public void OnPremRealtime_BuildStartMessage_IncludesProviderAndAutoLanguage()
    {
        var settings = new AzureOpenAISettings
        {
            ProviderId = 12,
            OmitTranscriptionLanguage = true
        };
        var json = OnPremRealtimeTranscriptionSession.BuildStartMessage(settings, "fr");
        Assert.Contains("\"type\":\"start\"", json, StringComparison.Ordinal);
        Assert.Contains("\"providerId\":12", json, StringComparison.Ordinal);
        Assert.Contains("\"language\":\"auto\"", json, StringComparison.Ordinal);
    }
}

public class OnPremStreamingResponseParserTests
{
    [Fact]
    public void TryParseJsonPayload_ParsesNdJsonPartialAndFinal()
    {
        Assert.True(OnPremStreamingResponseParser.TryParseJsonPayload(
            """{"Success":true,"Content":"Hello","final":false}""", out var partial));
        Assert.True(partial.Success);
        Assert.Equal("Hello", partial.Content);
        Assert.False(partial.IsFinal);

        Assert.True(OnPremStreamingResponseParser.TryParseJsonPayload(
            """{"Success":true,"Content":"Hello world","isFinal":true}""", out var final));
        Assert.True(final.IsFinal);
        Assert.Equal("Hello world", final.Content);
    }

    [Fact]
    public async Task ReadStreamAsync_ParsesSseEvents()
    {
        const string sse = """
            data: {"Success":true,"Content":"Bonjour","IsFinal":false}

            data: {"Success":true,"Content":"Bonjour monde","IsFinal":true}

            """;
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(sse));
        var events = new List<OnPremStreamingTranscriptEvent>();
        await foreach (var evt in OnPremStreamingResponseParser.ReadStreamAsync(stream, "text/event-stream", CancellationToken.None))
            events.Add(evt);

        Assert.Equal(2, events.Count);
        Assert.False(events[0].IsFinal);
        Assert.True(events[1].IsFinal);
        Assert.Equal("Bonjour monde", events[1].Content);
    }

    [Fact]
    public async Task ReadStreamAsync_ParsesNdJsonLines()
    {
        const string ndjson = """
            {"Success":true,"Content":"Line one","IsFinal":false}
            {"Success":true,"Content":"Line one done","IsFinal":true}

            """;
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ndjson));
        var events = new List<OnPremStreamingTranscriptEvent>();
        await foreach (var evt in OnPremStreamingResponseParser.ReadStreamAsync(stream, "application/x-ndjson", CancellationToken.None))
            events.Add(evt);

        Assert.Equal(2, events.Count);
        Assert.Equal("Line one done", events[1].Content);
    }
}

public class TranscriptionOutputLanguageServiceTests
{
    [Theory]
    [InlineData("", true)]
    [InlineData("https://api.openai.com", true)]
    [InlineData("https://myopenai.openai.com/", true)]
    [InlineData("https://corp.example.com/api/AI/queryAudio", false)]
    public void IsOpenAiEndpoint_DetectsEndpoint(string endpoint, bool expected)
    {
        Assert.Equal(expected, TranscriptionOutputLanguageService.IsOpenAiEndpoint(endpoint));
    }

    [Theory]
    [InlineData("https://corp.example.com/api/AI/queryAudio", false)]
    [InlineData("https://api.openai.com", true)]
    public void IsOutputLanguageSupported_MatchesOpenAi(string endpoint, bool expected)
    {
        var settings = new AppSettings { AzureOpenAI = new AzureOpenAISettings { Endpoint = endpoint } };
        Assert.Equal(expected, TranscriptionOutputLanguageService.IsOutputLanguageSupported(settings));
    }

    [Fact]
    public void GetOutputLanguage_ReturnsNullForSameAsSpoken()
    {
        var t = new TranslationSettings { Mode = "SameAsSpoken", TargetLanguage = "en" };
        Assert.Null(TranscriptionOutputLanguageService.GetOutputLanguage(t));
    }

    [Fact]
    public void GetOutputLanguage_ReturnsTargetForFixedMode()
    {
        var t = new TranslationSettings { Mode = "Fixed", TargetLanguage = "EN" };
        Assert.Equal("en", TranscriptionOutputLanguageService.GetOutputLanguage(t));
    }

    [Theory]
    [InlineData("fr", "fr", false)]
    [InlineData("fr", "en", true)]
    [InlineData("fr", null, false)]
    public void RequiresCrossLingualOutput_DetectsMismatch(string input, string? output, bool expected)
    {
        Assert.Equal(expected, TranscriptionOutputLanguageService.RequiresCrossLingualOutput(input, output));
    }

    [Theory]
    [InlineData("he", "Hebrew")]
    [InlineData("fr", "French")]
    [InlineData("xx", "English")]
    public void MapIsoToLanguageName_MapsCodes(string iso, string expected)
    {
        Assert.Equal(expected, TranscriptionOutputLanguageService.MapIsoToLanguageName(iso));
    }

    [Theory]
    [InlineData("en", "en", "English")]
    [InlineData("fr", "en", "French")]
    [InlineData("he", "en", "Hebrew")]
    [InlineData("en", "he", "אנגלית")]
    [InlineData("fr", "he", "צרפתית")]
    public void OutputLanguageCatalog_GetDisplayName_Localizes(string code, string locale, string expected)
    {
        Assert.Equal(expected, OutputLanguageCatalog.GetDisplayName(code, locale));
    }

    [Fact]
    public void GetInputLanguage_UsesManualLanguageInManualMode()
    {
        var azure = new AzureOpenAISettings
        {
            LanguageMode = "Manual",
            ManualLanguage = "HE",
            Language = "fr"
        };
        Assert.Equal("he", TranscriptionOutputLanguageService.GetInputLanguage(azure));
    }
}

public class TranscriptionStrategyResolverTests
{
    private static AzureOpenAISettings OpenAi(string model = "whisper-1") =>
        new() { Endpoint = "", Model = model };

    private static TranslationSettings FixedOutput(string lang) =>
        new() { Mode = "Fixed", TargetLanguage = lang, Enabled = true };

    [Fact]
    public void Resolve_OnPrem_AlwaysTranscribe()
    {
        var azure = new AzureOpenAISettings
        {
            Endpoint = "https://corp.example.com/api/AI/queryAudio",
            Model = "whisper-1",
            Prompt = "Custom prompt"
        };
        var plan = TranscriptionStrategyResolver.ResolvePlan(azure, FixedOutput("en"), "fr", isRealtime: false);
        Assert.Equal(TranscriptionApiMode.Transcribe, plan.Mode);
        Assert.Equal("transcriptions", plan.EndpointPath);
        Assert.Equal("Custom prompt", plan.EffectivePrompt);
        Assert.Null(plan.OutputLanguage);
    }

    [Fact]
    public void Resolve_WhisperToEnglish_UsesTranslationsEndpoint()
    {
        var plan = TranscriptionStrategyResolver.ResolvePlan(OpenAi(), FixedOutput("en"), "fr", isRealtime: false);
        Assert.Equal(TranscriptionApiMode.TranslateToEnglish, plan.Mode);
        Assert.Equal("translations", plan.EndpointPath);
        Assert.True(TranscriptionStrategyResolver.OutputAppliedByStt(plan.Mode));
    }

    [Fact]
    public void Resolve_Gpt4oCrossLingual_UsesOutputPrompt()
    {
        var azure = OpenAi("gpt-4o-transcribe");
        azure.Prompt = "Bilingual FR+HE dictation.";
        var plan = TranscriptionStrategyResolver.ResolvePlan(azure, FixedOutput("fr"), "he", isRealtime: false);
        Assert.Equal(TranscriptionApiMode.TranscribeWithOutputPrompt, plan.Mode);
        Assert.Contains("Bilingual FR+HE dictation.", plan.EffectivePrompt, StringComparison.Ordinal);
        Assert.Contains("French", plan.EffectivePrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_SameLanguage_TranscribeOnly()
    {
        var plan = TranscriptionStrategyResolver.ResolvePlan(OpenAi(), FixedOutput("fr"), "fr", isRealtime: false);
        Assert.Equal(TranscriptionApiMode.Transcribe, plan.Mode);
        Assert.Equal("transcriptions", plan.EndpointPath);
    }

    [Fact]
    public void Resolve_RealtimeCrossLingual_PostTranslationFallback()
    {
        var plan = TranscriptionStrategyResolver.ResolvePlan(OpenAi(), FixedOutput("en"), "fr", isRealtime: true);
        Assert.Equal(TranscriptionApiMode.PostTranslationFallback, plan.Mode);
    }

    [Fact]
    public void Resolve_WhisperNonEnglish_Fallback()
    {
        var plan = TranscriptionStrategyResolver.ResolvePlan(OpenAi(), FixedOutput("fr"), "he", isRealtime: false);
        Assert.Equal(TranscriptionApiMode.PostTranslationFallback, plan.Mode);
        Assert.False(TranscriptionStrategyResolver.OutputAppliedByStt(plan.Mode));
    }

    [Fact]
    public void BuildOutputPrompt_PreservesUserPrompt()
    {
        var prompt = TranscriptionStrategyResolver.BuildOutputPrompt("Keep talmudic terms.", "he");
        Assert.Contains("Write all output text in Hebrew", prompt, StringComparison.Ordinal);
        Assert.StartsWith("Keep talmudic terms.", prompt, StringComparison.Ordinal);
        Assert.Contains("Hebrew", prompt, StringComparison.Ordinal);
    }
}

public class AppSettingsMigrationTests
{
    [Fact]
    public void Load_MigratesEnabledTranslationToFixedMode()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koli-migrate-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "appsettings.json");
        try
        {
            File.WriteAllText(path, """
                {
                  "Translation": {
                    "Enabled": true,
                    "TargetLanguage": "en"
                  }
                }
                """);
            var settings = AppSettings.Load(path);
            Assert.Equal("Fixed", settings.Translation.Mode);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_InitializesAssistantDefaultsWhenMissing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koli-assistant-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "appsettings.json");
        try
        {
            File.WriteAllText(path, """{ "AzureOpenAI": { "ApiKey": "" } }""");
            var settings = AppSettings.Load(path);
            Assert.NotNull(settings.Assistant);
            Assert.True(settings.Assistant.Enabled);
            Assert.Equal("gpt-4.1", settings.Assistant.Model);
            Assert.True(settings.Assistant.WebSearchEnabled);
            Assert.False(string.IsNullOrWhiteSpace(settings.Assistant.SystemPrompt));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}

public class VoiceAssistantResponseParserTests
{
    [Fact]
    public void ParseOutputText_ExtractsLastMessageOutputText()
    {
        const string json = """
            {
              "output": [
                { "type": "web_search_call", "status": "completed" },
                {
                  "type": "message",
                  "content": [
                    { "type": "output_text", "text": "Draft answer" },
                    { "type": "output_text", "text": "Final answer" }
                  ]
                }
              ]
            }
            """;

        var text = VoiceAssistantResponseParser.ParseOutputText(json);
        Assert.Equal("Final answer", text);
    }

    [Fact]
    public void ParseOutputText_ReturnsNullForEmptyOutput()
    {
        Assert.Null(VoiceAssistantResponseParser.ParseOutputText("""{ "output": [] }"""));
        Assert.Null(VoiceAssistantResponseParser.ParseOutputText(""));
    }

    [Theory]
    [InlineData("See https://example.com/doc for details.", "See for details.")]
    [InlineData("Answer with citation [1] here.", "Answer with citation here.")]
    public void CleanResponseText_StripsUrlsAndCitations(string input, string expected)
    {
        var cleaned = VoiceAssistantResponseParser.CleanResponseText(input);
        Assert.Equal(expected, cleaned);
    }

    [Fact]
    public void CleanResponseText_StripsMarkdownLinksToNull()
    {
        Assert.Null(VoiceAssistantResponseParser.CleanResponseText("[link text](https://example.com)"));
    }

    [Fact]
    public void CleanResponseText_ReturnsNullForWhitespace()
    {
        Assert.Null(VoiceAssistantResponseParser.CleanResponseText("   "));
        Assert.Null(VoiceAssistantResponseParser.CleanResponseText(null));
    }
}

public class VoiceAssistantServiceTests
{
    [Fact]
    public void BuildResponsesRequestBody_IncludesWebSearchWhenEnabled()
    {
        var settings = new AssistantSettings
        {
            Model = "gpt-4.1",
            SystemPrompt = "Be concise."
        };

        var json = VoiceAssistantService.BuildResponsesRequestBody(settings, "What is Paris?", includeWebSearch: true);

        Assert.Contains("\"model\":\"gpt-4.1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"input\":\"What is Paris?\"", json, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"web_search\"", json, StringComparison.Ordinal);
        Assert.Contains("\"toolChoice\":\"auto\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildResponsesRequestBody_OmitsToolsWhenWebSearchDisabled()
    {
        var settings = new AssistantSettings { Model = "gpt-4.1", SystemPrompt = "Be concise." };
        var json = VoiceAssistantService.BuildResponsesRequestBody(settings, "Hello?", includeWebSearch: false);

        Assert.DoesNotContain("web_search", json, StringComparison.Ordinal);
        Assert.DoesNotContain("toolChoice", json, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildChatCompletionRequestBody_IncludesSystemAndUserMessages()
    {
        var settings = new AssistantSettings { Model = "gpt-4.1", SystemPrompt = "Reply briefly." };
        var json = VoiceAssistantService.BuildChatCompletionRequestBody(settings, "Bonjour?");

        Assert.Contains("\"role\":\"system\"", json, StringComparison.Ordinal);
        Assert.Contains("\"role\":\"user\"", json, StringComparison.Ordinal);
        Assert.Contains("Bonjour?", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("https://api.openai.com", true)]
    [InlineData("https://corp.example.com/api/AI/queryAudio", false)]
    public void IsSupportedEndpoint_DetectsOpenAiHost(string endpoint, bool expected)
    {
        Assert.Equal(expected, VoiceAssistantService.IsSupportedEndpoint(endpoint));
    }
}

public class AltGrToggleTrackerTests
{
    private const uint VkRMenu = 0xA5;
    private const uint VkE = 0x45;
    private const uint VkLControl = 0xA2;

    [Fact]
    public void LoneAltGrRelease_TogglesAssistant()
    {
        var tracker = new AltGrToggleTracker();

        Assert.Null(tracker.ProcessKey(VkLControl, isKeyDown: true));
        Assert.Null(tracker.ProcessKey(VkRMenu, isKeyDown: true));
        Assert.Equal(true, tracker.ProcessKey(VkRMenu, isKeyDown: false));
    }

    [Fact]
    public void AltGrWithCharacter_DoesNotToggle()
    {
        var tracker = new AltGrToggleTracker();

        Assert.Null(tracker.ProcessKey(VkLControl, isKeyDown: true));
        Assert.Null(tracker.ProcessKey(VkRMenu, isKeyDown: true));
        Assert.Null(tracker.ProcessKey(VkE, isKeyDown: true));
        Assert.Null(tracker.ProcessKey(VkE, isKeyDown: false));
        Assert.Null(tracker.ProcessKey(VkRMenu, isKeyDown: false));
    }

    [Fact]
    public void PlainRightAltRelease_TogglesAssistant()
    {
        var tracker = new AltGrToggleTracker();

        Assert.Null(tracker.ProcessKey(VkRMenu, isKeyDown: true));
        Assert.Equal(true, tracker.ProcessKey(VkRMenu, isKeyDown: false));
    }
}
