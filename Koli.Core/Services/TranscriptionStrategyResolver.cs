using Koli.Config;

namespace Koli.Services;

public enum TranscriptionApiMode
{
    Transcribe,
    TranslateToEnglish,
    TranscribeWithOutputPrompt,
    PostTranslationFallback
}

public sealed record TranscriptionRequestPlan(
    TranscriptionApiMode Mode,
    string? InputLanguageHint,
    string? OutputLanguage,
    string EffectivePrompt,
    string EndpointPath);

/// <summary>
/// Chooses OpenAI/Azure STT strategy for cross-lingual output. On-prem endpoints always get plain transcription.
/// </summary>
public static class TranscriptionStrategyResolver
{
    public static TranscriptionRequestPlan ResolvePlan(
        AzureOpenAISettings azureSettings,
        TranslationSettings translationSettings,
        string inputLanguage,
        bool isRealtime)
    {
        var basePrompt = azureSettings.Prompt ?? string.Empty;

        if (!TranscriptionOutputLanguageService.IsOpenAiEndpoint(azureSettings.Endpoint))
        {
            return new TranscriptionRequestPlan(
                TranscriptionApiMode.Transcribe,
                inputLanguage,
                null,
                basePrompt,
                "transcriptions");
        }

        var outputLanguage = TranscriptionOutputLanguageService.GetOutputLanguage(translationSettings);
        if (!TranscriptionOutputLanguageService.RequiresCrossLingualOutput(inputLanguage, outputLanguage))
        {
            return new TranscriptionRequestPlan(
                TranscriptionApiMode.Transcribe,
                ResolveInputLanguageHint(azureSettings, inputLanguage, crossLingual: false),
                null,
                basePrompt,
                "transcriptions");
        }

        var model = (azureSettings.Model ?? "whisper-1").Trim();

        if (isRealtime)
        {
            return new TranscriptionRequestPlan(
                TranscriptionApiMode.PostTranslationFallback,
                ResolveInputLanguageHint(azureSettings, inputLanguage, crossLingual: true),
                outputLanguage,
                basePrompt,
                "transcriptions");
        }

        if (outputLanguage!.Equals("en", StringComparison.OrdinalIgnoreCase)
            && model.Equals("whisper-1", StringComparison.OrdinalIgnoreCase))
        {
            return new TranscriptionRequestPlan(
                TranscriptionApiMode.TranslateToEnglish,
                null,
                outputLanguage,
                basePrompt,
                "translations");
        }

        if (model.Equals("gpt-4o-transcribe", StringComparison.OrdinalIgnoreCase)
            || model.StartsWith("gpt-4o-transcribe-", StringComparison.OrdinalIgnoreCase))
        {
            return new TranscriptionRequestPlan(
                TranscriptionApiMode.TranscribeWithOutputPrompt,
                ResolveInputLanguageHint(azureSettings, inputLanguage, crossLingual: true),
                outputLanguage,
                BuildOutputPrompt(basePrompt, outputLanguage),
                "transcriptions");
        }

        return new TranscriptionRequestPlan(
            TranscriptionApiMode.PostTranslationFallback,
            ResolveInputLanguageHint(azureSettings, inputLanguage, crossLingual: true),
            outputLanguage,
            basePrompt,
            "transcriptions");
    }

    public static string? ResolveInputLanguageHint(AzureOpenAISettings settings, string inputLanguage, bool crossLingual)
    {
        if (settings.OmitTranscriptionLanguage)
            return null;

        if (crossLingual)
        {
            if (settings.LanguageMode == "Auto" && !string.IsNullOrWhiteSpace(inputLanguage))
                return inputLanguage.Trim().ToLowerInvariant();
            return null;
        }

        var hint = settings.LanguageMode == "Manual" && !string.IsNullOrWhiteSpace(settings.ManualLanguage)
            ? settings.ManualLanguage
            : !string.IsNullOrWhiteSpace(settings.Language) ? settings.Language : inputLanguage;

        return string.IsNullOrWhiteSpace(hint) ? null : hint.Trim().ToLowerInvariant();
    }

    public static string BuildOutputPrompt(string userPrompt, string outputLanguageCode)
    {
        var languageName = TranscriptionOutputLanguageService.MapIsoToLanguageName(outputLanguageCode);
        var instruction =
            $"Write all output text in {languageName}. Transcribe the spoken content directly in {languageName}, even if the speaker uses another language. Do not include text in any other language.";

        if (string.IsNullOrWhiteSpace(userPrompt))
            return instruction;

        return $"{userPrompt.Trim()}\n\n{instruction}";
    }

    public static bool OutputAppliedByStt(TranscriptionApiMode mode) =>
        mode is TranscriptionApiMode.TranslateToEnglish or TranscriptionApiMode.TranscribeWithOutputPrompt;
}
