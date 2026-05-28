namespace Koli.Services;

/// <summary>
/// Preset output languages with display names localized for the user's UI language.
/// ISO codes are stored in settings; only human-readable labels are shown in the UI.
/// </summary>
public static class OutputLanguageCatalog
{
    public static readonly IReadOnlyList<string> PresetCodes = ["en", "fr", "he", "de", "es", "it"];

    private static readonly Dictionary<(string Code, string Locale), string> DisplayNames = new()
    {
        { ("en", "en"), "English" },
        { ("fr", "en"), "French" },
        { ("he", "en"), "Hebrew" },
        { ("de", "en"), "German" },
        { ("es", "en"), "Spanish" },
        { ("it", "en"), "Italian" },

        { ("en", "he"), "אנגלית" },
        { ("fr", "he"), "צרפתית" },
        { ("he", "he"), "עברית" },
        { ("de", "he"), "גרמנית" },
        { ("es", "he"), "ספרדית" },
        { ("it", "he"), "איטלקית" },
    };

    public static string NormalizeDisplayLocale(string? locale)
    {
        var code = (locale ?? "en").Trim().ToLowerInvariant();
        return code switch
        {
            "he" or "iw" => "he",
            _ => "en"
        };
    }

    public static string GetDisplayName(string isoCode, string? displayLocale)
    {
        var code = (isoCode ?? "").Trim().ToLowerInvariant();
        if (code is "iw")
            code = "he";

        var locale = NormalizeDisplayLocale(displayLocale);
        if (DisplayNames.TryGetValue((code, locale), out var localized))
            return localized;

        if (DisplayNames.TryGetValue((code, "en"), out var english))
            return english;

        return TranscriptionOutputLanguageService.MapIsoToLanguageName(code);
    }

    public static IReadOnlyList<(string DisplayName, string Code)> GetPresetOptions(string? displayLocale) =>
        PresetCodes.Select(code => (GetDisplayName(code, displayLocale), code)).ToList();
}
