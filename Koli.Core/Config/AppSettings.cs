using System.Text.Json;
using Koli.Models;

namespace Koli.Config;

public sealed class AppSettings
{
    public AzureOpenAISettings AzureOpenAI { get; set; } = new();
    public AudioSettings Audio { get; set; } = new();
    public TypingSettings Typing { get; set; } = new();
    public RewriteSettings Rewrite { get; set; } = new();
    public MeetingSettings Meeting { get; set; } = new();
    public TranslationSettings Translation { get; set; } = new();

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
    public string Prompt { get; set; } = "Réécris le texte suivant de façon professionnelle et claire. Si le texte mélange le français et l’hébreu, conserve chaque langue dans sa forme parlée : les parties en français restent en français (corrige grammaire et style en français uniquement) ; les parties en hébreu restent en hébreu, écrites en alphabet hébreu (corrige grammaire et style en hébreu uniquement). Si des mots manifestement hébreux (vocabulaire talmudique, biblique, commentaires) ne sont que transcrits en alphabet latin, remplace-les par l’écriture en caractères hébreux (א-ת), sans changer le sens. Ne traduis pas du français vers l’hébreu ni de l’hébreu vers le français, sauf si le texte source demande explicitement une traduction. Ne renvoie que le texte réécrit, sans aucune explication :";
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
/// Optional post-transcription translation step. Completely independent from
/// <see cref="AzureOpenAISettings.LanguageMode"/>: by default the application just
/// transcribes the audio in the spoken language. The user opts-in to translation
/// by enabling it here and choosing a <see cref="TargetLanguage"/>.
/// </summary>
public sealed class TranslationSettings
{
    /// <summary>Master switch for the translation step. Default false (no translation).</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// ISO 639-1 code of the language the transcription should be translated into
    /// (e.g. <c>"en"</c>, <c>"fr"</c>, <c>"he"</c>). Empty string means no translation
    /// (equivalent to <see cref="Enabled"/> = false).
    /// </summary>
    public string TargetLanguage { get; set; } = "";

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
