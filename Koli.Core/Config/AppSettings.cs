using System.Text.Json;
using Koli.Models;
using Koli.Services;

namespace Koli.Config;

public sealed class AppSettings
{
    public AzureOpenAISettings AzureOpenAI { get; set; } = new();
    public AudioSettings Audio { get; set; } = new();
    public TypingSettings Typing { get; set; } = new();
    public RewriteSettings Rewrite { get; set; } = new();
    public MeetingSettings Meeting { get; set; } = new();
    public TranslationSettings Translation { get; set; } = new();
    public AssistantSettings Assistant { get; set; } = new();

    public static AppSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Configuration file not found at {path}");
        }

        var json = File.ReadAllText(path);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (settings == null)
        {
            throw new InvalidOperationException("Unable to parse configuration.");
        }

        // Ensure nested objects are initialized
        settings.AzureOpenAI ??= new AzureOpenAISettings();
        settings.Audio ??= new AudioSettings();
        settings.Typing ??= new TypingSettings();
        settings.Rewrite ??= new RewriteSettings();
        settings.Meeting ??= new MeetingSettings();
        settings.Translation ??= new TranslationSettings();
        settings.Assistant ??= new AssistantSettings();

        TranscriptionOutputLanguageService.MigrateTranslationSettings(settings.Translation);
        TranscriptionOutputLanguageService.SyncLegacyEnabledFlag(settings.Translation);
        
        // Ensure ProfessionalismLevel has a default value
        if (string.IsNullOrWhiteSpace(settings.Rewrite.ProfessionalismLevel))
        {
            settings.Rewrite.ProfessionalismLevel = "Professional";
        }
        
        // Ensure StreamingMode has a default value if not explicitly set in JSON
        // System.Text.Json may not always apply default values for missing properties
        // when the parent object exists in JSON. Check if StreamingMode exists in the JSON string.
        if (settings.Typing != null)
        {
            // Check if StreamingMode property exists in the JSON
            // If it doesn't exist, ensure it's set to false (the default)
            var jsonLower = json.ToLowerInvariant();
            var hasStreamingMode = jsonLower.Contains("\"streamingmode\"") || jsonLower.Contains("'streamingmode'");
            
            if (!hasStreamingMode)
            {
                // StreamingMode was not in JSON, ensure default value
                settings.Typing.StreamingMode = false;
            }
        }

        return settings;
    }

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(path, json);
    }
}

public sealed class AzureOpenAISettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string Model { get; set; } = "whisper-1";
    public string Language { get; set; } = "en"; // Default to English, ISO-639-1 format (en, fr, es, etc.)
    public string Prompt { get; set; } = "This is a voice dictation transcription. Ignore noise and silence."; // Optional prompt to guide the model
    public string LanguageMode { get; set; } = "Auto"; // "Auto" for keyboard detection or "Manual" for user selection
    public string ManualLanguage { get; set; } = "en"; // Language to use when LanguageMode is "Manual"
    /// <summary>When true, the OpenAI/Azure audio API is called without the <c>language</c> field so the model can mix scripts (e.g. French + Hebrew). When false, <see cref="Language"/> is sent as a hint (can force Latin-only output for code-switching speech).</summary>
    public bool OmitTranscriptionLanguage { get; set; }
    /// <summary>Ai Nexus on-prem <c>queryAudio</c>: multipart field <c>providerId</c> when set.</summary>
    public int? ProviderId { get; set; }
    // On-premise Whisper API (e.g. Hadassah): optional prompt template IDs and diarization
    public int? TranscriptionPromptId { get; set; }
    public int? FormattingPromptId { get; set; }
    public bool EnableSpeakerDiarization { get; set; } = false;
    /// <summary>On-prem: when true, use live transcription (Ai Nexus WebSocket <c>/api/ai/realtime/transcribe</c>, or HTTP fallback if enabled).</summary>
    public bool EnableStreamingTranscription { get; set; } = false;
    /// <summary>On-prem HTTP streaming URL (<c>queryAudio</c> + <c>stream=true</c>); empty uses <see cref="Endpoint"/>.</summary>
    public string StreamingEndpoint { get; set; } = "";
    /// <summary>On-prem streaming multipart <c>providerId</c>; null uses <see cref="ProviderId"/>.</summary>
    public int? StreamingProviderId { get; set; }
    /// <summary>On-prem WebSocket URL; empty derives <c>wss://…/api/ai/realtime/transcribe</c> from <see cref="Endpoint"/>.</summary>
    public string RealtimeEndpoint { get; set; } = "";
    /// <summary>When true and WebSocket connect fails, fall back to rolling <c>queryAudio</c> HTTP streaming.</summary>
    public bool UseQueryAudioHttpStreamingFallback { get; set; } = false;
}

public sealed class AudioSettings
{
    public int SampleRate { get; set; } = 16_000;
    public string Device { get; set; } = "default";
}

public sealed class TypingSettings
{
    public bool AutoSpace { get; set; } = true;
    public int ChunkDelayMs { get; set; } = 20;
    public bool TypeInActiveWindow { get; set; } = true; // Default to true to type in any app
    public bool StreamingMode { get; set; } = false; // Default to false (all at once)
}

public sealed class RewriteSettings
{
    public bool Enabled { get; set; } = false;
    public string Model { get; set; } = "gpt-3.5-turbo";
    public string Prompt { get; set; } = "Rewrite the following text in a professional and clear manner. If the text mixes French and Hebrew, keep each language in its spoken form: French parts stay in French (correct grammar and style in French only); Hebrew parts stay in Hebrew, written in Hebrew script (correct grammar and style in Hebrew only). If words that are clearly Hebrew (Talmudic, Biblical, commentary vocabulary) appear only as Latin transliteration, replace them with Hebrew script (א-ת) without changing meaning. Do not translate from French to Hebrew or Hebrew to French unless the source text explicitly requests a translation. Only return the rewritten text, with no explanation:";
    public string ProfessionalismLevel { get; set; } = "Professional"; // Casual, Polished, Professional, Formal, Executive
    
    public string GetPromptForLevel(string language = "en")
    {
        // Map ISO-639-1 codes to full language names for clarity
        var languageName = language.ToLower() switch
        {
            "fr" => "French",
            "es" => "Spanish",
            "de" => "German",
            "it" => "Italian",
            "pt" => "Portuguese",
            "nl" => "Dutch",
            "ru" => "Russian",
            "zh" => "Chinese",
            "ja" => "Japanese",
            "ko" => "Korean",
            "ar" => "Arabic",
            "he" => "Hebrew",
            "iw" => "Hebrew", // Legacy ISO 639-1 code for Hebrew
            "en" => "English",
            _ => "English" // Default to English for unknown codes
        };

        const string bilingualFrHe =
            " If the text mixes French and Hebrew, keep each in its original language and script: polish French in French only, Hebrew in Hebrew script only; do not translate between them unless the source explicitly asks for a translation. If clearly Hebrew words appear only as Latin transliteration (e.g. religious or Talmudic vocabulary), write them in Hebrew script instead.";
        var languageInstruction =
            $" If the text is in a single language only, write the entire output in {languageName}.";

        return ProfessionalismLevel switch
        {
            "Casual" => $"Rewrite the following text to be slightly more polished while keeping a conversational tone. Fix grammar and spelling errors but maintain a casual, friendly style.{bilingualFrHe}{languageInstruction} Only return the rewritten text without explanations:",
            "Polished" => $"Rewrite the following text in a polished and clear manner. Improve grammar and clarity while keeping a natural, approachable tone.{bilingualFrHe}{languageInstruction} Only return the rewritten text without explanations:",
            "Professional" => $"Rewrite the following text in a professional and formal manner. Use business-appropriate language while remaining clear and direct.{bilingualFrHe}{languageInstruction} Only return the rewritten text without explanations:",
            "Formal" => $"Rewrite the following text in a highly formal and professional style. Use sophisticated vocabulary and formal sentence structures appropriate for official documents.{bilingualFrHe}{languageInstruction} Only return the rewritten text without explanations:",
            "Executive" => $"Rewrite the following text in an executive, corporate style. Use precise, authoritative language appropriate for high-level business communication. Maintain gravitas and professionalism.{bilingualFrHe}{languageInstruction} Only return the rewritten text without explanations:",
            _ => Prompt + languageInstruction // Fallback: full custom Prompt from JSON (already bilingual if configured)
        };
    }
}

public sealed class MeetingSettings
{
    public string DefaultAudioSource { get; set; } = "Microphone"; // "Microphone" or "SystemAudio"
    public int ChunkDurationSeconds { get; set; } = 6;
    public string TranscriptSavePath { get; set; } = "Meetings";
    public bool AutoSaveTranscript { get; set; } = true;
}

/// <summary>
/// Output language settings (UI: "Output language"). For OpenAI/Azure endpoints the app
/// routes STT to produce text directly in the target language when possible; otherwise a
/// post-transcription LLM step applies. On-prem endpoints keep legacy behaviour only.
/// </summary>
public sealed class TranslationSettings
{
    /// <summary>Legacy master switch; kept in sync with <see cref="Mode"/> for JSON compatibility.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// ISO 639-1 code of the desired output language
    /// (e.g. <c>"en"</c>, <c>"fr"</c>, <c>"he"</c>). Used when <see cref="Mode"/> is <c>Fixed</c>.
    /// </summary>
    public string TargetLanguage { get; set; } = "";

    /// <summary><c>SameAsSpoken</c> (default) or <c>Fixed</c> for a fixed output language.</summary>
    public string Mode { get; set; } = "SameAsSpoken";

    /// <summary>
    /// Chat completion URL. Leave empty to auto-derive from <see cref="AzureOpenAISettings.Endpoint"/>:
    ///   - OpenAI / Azure OpenAI endpoint → <c>https://api.openai.com/v1/chat/completions</c>
    ///   - Ai Nexus / on-premise endpoint (e.g. <c>.../api/AI/queryAudio</c>) → sibling <c>.../api/ai/query</c>
    /// </summary>
    public string Endpoint { get; set; } = "";

    /// <summary>
    /// Model name. Only used for the OpenAI / Azure path. Ignored for Ai Nexus (server chooses the default provider/model).
    /// </summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>Ai Nexus <c>/api/ai/query</c>: JSON <c>providerId</c> when set.</summary>
    public int? ProviderId { get; set; }
}

/// <summary>
/// Voice assistant (Alt Gr): transcribe a spoken question, query OpenAI Responses API, paste the answer.
/// </summary>
public sealed class AssistantSettings
{
    public bool Enabled { get; set; } = true;
    public string Model { get; set; } = "gpt-4.1";
    public bool WebSearchEnabled { get; set; } = true;
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;

    public const string DefaultSystemPrompt =
        "Tu es un assistant vocal. Réponds uniquement avec la réponse finale, sans salutation, sans « Voici la réponse », sans mise en forme conversationnelle. " +
        "Réponds dans la langue de la question. " +
        "Si l'information est introuvable ou incertaine, réponds uniquement « Je ne sais pas. » ou « Je n'ai pas trouvé de réponse. » " +
        "N'inclus aucune citation, URL, lien ou source dans le texte.";
}
