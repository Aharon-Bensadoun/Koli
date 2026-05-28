using Koli.Config;

namespace Koli.Services;

/// <summary>
/// Resolves spoken (input) vs desired written (output) language for OpenAI/Azure STT routing.
/// On-premise endpoints are excluded from output-language features.
/// </summary>
public static class TranscriptionOutputLanguageService
{
    public static bool IsOpenAiEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return true;
        return endpoint.Contains("openai.com", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsOutputLanguageSupported(AppSettings settings) =>
        IsOpenAiEndpoint(settings.AzureOpenAI.Endpoint);

    public static string GetInputLanguage(AppSettings settings) =>
        GetInputLanguage(settings.AzureOpenAI);

    public static string GetInputLanguage(AzureOpenAISettings azureSettings)
    {
        if (azureSettings.LanguageMode == "Manual"
            && !string.IsNullOrWhiteSpace(azureSettings.ManualLanguage))
            return azureSettings.ManualLanguage.Trim().ToLowerInvariant();

        return (azureSettings.Language ?? "en").Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Returns the desired output ISO code, or <c>null</c> when output should match spoken language.
    /// </summary>
    public static string? GetOutputLanguage(TranslationSettings translation)
    {
        var mode = NormalizeMode(translation);
        if (mode == "SameAsSpoken" || string.IsNullOrWhiteSpace(translation.TargetLanguage))
            return null;

        return translation.TargetLanguage.Trim().ToLowerInvariant();
    }

    public static bool RequiresCrossLingualOutput(string? inputLanguage, string? outputLanguage)
    {
        if (string.IsNullOrWhiteSpace(outputLanguage))
            return false;

        if (string.IsNullOrWhiteSpace(inputLanguage))
            return true;

        return !inputLanguage.Trim().Equals(outputLanguage.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static string MapIsoToLanguageName(string code) => (code ?? string.Empty).Trim().ToLowerInvariant() switch
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
        "he" or "iw" => "Hebrew",
        "en" => "English",
        _ => "English"
    };

    /// <summary>
    /// Applies legacy <see cref="TranslationSettings.Enabled"/> migration to <see cref="TranslationSettings.Mode"/>.
    /// </summary>
    public static void MigrateTranslationSettings(TranslationSettings translation)
    {
        if (translation.Enabled && !string.IsNullOrWhiteSpace(translation.TargetLanguage))
            translation.Mode = "Fixed";
        else if (string.IsNullOrWhiteSpace(translation.Mode))
            translation.Mode = "SameAsSpoken";
    }

    public static string NormalizeMode(TranslationSettings translation)
    {
        MigrateTranslationSettings(translation);
        return translation.Mode.Equals("Fixed", StringComparison.OrdinalIgnoreCase) ? "Fixed" : "SameAsSpoken";
    }

    public static void SyncLegacyEnabledFlag(TranslationSettings translation)
    {
        var mode = NormalizeMode(translation);
        translation.Enabled = mode == "Fixed" && !string.IsNullOrWhiteSpace(translation.TargetLanguage);
    }

    public static string GetOutputLanguageChipLabel(AppSettings settings)
    {
        if (!IsOutputLanguageSupported(settings))
            return "N/A";

        var output = GetOutputLanguage(settings.Translation);
        if (output == null)
            return "Auto";

        return OutputLanguageCatalog.GetDisplayName(output, "en");
    }
}
