using Koli.Config;
using Koli.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Koli.WinUI.Dialogs;

public sealed class OutputLanguageSettingsDialog : ContentDialog
{
    private readonly TranslationSettings _settings;
    private readonly RadioButton _sameAsSpokenRadio;
    private readonly RadioButton _fixedRadio;
    private readonly ComboBox _languageCombo;
    private readonly TextBox _customIsoBox;

    private readonly IReadOnlyList<(string Label, string Code)> _presetLanguages;

    public OutputLanguageSettingsDialog(TranslationSettings settings, string? apiEndpoint, string? displayLocale = null)
    {
        _settings = settings;
        _presetLanguages = OutputLanguageCatalog.GetPresetOptions(displayLocale);

        Title = "Output language";
        PrimaryButtonText = "Save";
        CloseButtonText = "Cancel";

        TranscriptionOutputLanguageService.MigrateTranslationSettings(_settings);
        var isFixed = _settings.Mode.Equals("Fixed", StringComparison.OrdinalIgnoreCase)
                      && !string.IsNullOrWhiteSpace(_settings.TargetLanguage);

        var panel = new StackPanel { Spacing = 12, MinWidth = 420 };

        _sameAsSpokenRadio = new RadioButton { Content = "Same as spoken", IsChecked = !isFixed };
        _fixedRadio = new RadioButton { Content = "Fixed language", IsChecked = isFixed };

        _languageCombo = new ComboBox
        {
            Header = "Language",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = isFixed,
            ItemsSource = _presetLanguages.Select(p => p.Label).ToList()
        };

        var currentCode = (_settings.TargetLanguage ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(currentCode))
            currentCode = "en";
        var presetIndex = _presetLanguages.ToList().FindIndex(p => p.Code == currentCode);
        if (presetIndex >= 0)
            _languageCombo.SelectedIndex = presetIndex;

        _customIsoBox = new TextBox
        {
            Header = "Custom ISO 639-1 (optional)",
            PlaceholderText = "en",
            Text = presetIndex < 0 ? currentCode : "",
            IsEnabled = isFixed
        };

        _sameAsSpokenRadio.Checked += (_, _) => UpdateFixedControls(false);
        _fixedRadio.Checked += (_, _) => UpdateFixedControls(true);

        var help = new TextBlock
        {
            Text = TranscriptionOutputLanguageService.IsOpenAiEndpoint(apiEndpoint)
                ? "For English with whisper-1, native audio translation is used.\n"
                  + "In Realtime mode, an automatic fallback may apply."
                : "On-premise endpoints apply the selected output language after transcription.",
            TextWrapping = TextWrapping.WrapWholeWords,
            Foreground = Application.Current.Resources["TextSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush,
            FontSize = 12
        };

        panel.Children.Add(_sameAsSpokenRadio);
        panel.Children.Add(_fixedRadio);
        panel.Children.Add(_languageCombo);
        panel.Children.Add(_customIsoBox);
        panel.Children.Add(help);

        Content = panel;

        PrimaryButtonClick += (_, _) => SaveSettings();
    }

    private void UpdateFixedControls(bool fixedMode)
    {
        _languageCombo.IsEnabled = fixedMode;
        _customIsoBox.IsEnabled = fixedMode;
    }

    private void SaveSettings()
    {
        if (_sameAsSpokenRadio.IsChecked == true)
        {
            _settings.Mode = "SameAsSpoken";
            _settings.TargetLanguage = "";
            _settings.Enabled = false;
            return;
        }

        var code = _customIsoBox.Text.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code)
            && _languageCombo.SelectedIndex >= 0
            && _languageCombo.SelectedIndex < _presetLanguages.Count)
            code = _presetLanguages[_languageCombo.SelectedIndex].Code;

        _settings.Mode = "Fixed";
        _settings.TargetLanguage = code;
        TranscriptionOutputLanguageService.SyncLegacyEnabledFlag(_settings);
    }
}
