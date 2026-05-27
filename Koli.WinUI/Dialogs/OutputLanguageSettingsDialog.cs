using Koli.Config;
using Koli.Services;
using Microsoft.UI.Xaml.Controls;

namespace Koli.WinUI.Dialogs;

public sealed class OutputLanguageSettingsDialog : ContentDialog
{
    private readonly TranslationSettings _settings;
    private readonly bool _isAvailable;
    private readonly RadioButton _sameAsSpokenRadio;
    private readonly RadioButton _fixedRadio;
    private readonly ComboBox _languageCombo;
    private readonly TextBox _customIsoBox;
    private readonly TextBlock _unavailableMessage;

    private readonly IReadOnlyList<(string Label, string Code)> _presetLanguages;

    public OutputLanguageSettingsDialog(TranslationSettings settings, string? apiEndpoint, string? displayLocale = null)
    {
        _settings = settings;
        _isAvailable = TranscriptionOutputLanguageService.IsOpenAiEndpoint(apiEndpoint);
        _presetLanguages = OutputLanguageCatalog.GetPresetOptions(displayLocale);

        Title = "Langue de sortie";
        PrimaryButtonText = "Save";
        CloseButtonText = "Cancel";
        IsPrimaryButtonEnabled = _isAvailable;

        TranscriptionOutputLanguageService.MigrateTranslationSettings(_settings);
        var isFixed = _settings.Mode.Equals("Fixed", StringComparison.OrdinalIgnoreCase)
                      && !string.IsNullOrWhiteSpace(_settings.TargetLanguage);

        _sameAsSpokenRadio = new RadioButton
        {
            Content = "Même langue que parlée",
            IsChecked = !isFixed,
            IsEnabled = _isAvailable
        };
        _fixedRadio = new RadioButton
        {
            Content = "Langue fixe",
            IsChecked = isFixed,
            IsEnabled = _isAvailable
        };

        _languageCombo = new ComboBox
        {
            Header = "Langue",
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
            IsEnabled = _isAvailable && isFixed,
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
            Header = "Autre (ISO 639-1)",
            PlaceholderText = "en",
            Text = presetIndex < 0 ? currentCode : "",
            IsEnabled = _isAvailable && isFixed
        };

        _unavailableMessage = new TextBlock
        {
            Text = "Langue de sortie disponible uniquement avec OpenAI / Azure OpenAI.",
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
            Visibility = _isAvailable
                ? Microsoft.UI.Xaml.Visibility.Collapsed
                : Microsoft.UI.Xaml.Visibility.Visible
        };

        var help = new TextBlock
        {
            Text = "Disponible avec OpenAI / Azure OpenAI uniquement.\n"
                   + "Pour l'anglais avec whisper-1, la traduction audio native est utilisée.\n"
                   + "En mode Realtime, un repli automatique peut s'appliquer.",
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
            Opacity = 0.8
        };

        _sameAsSpokenRadio.Checked += (_, _) => UpdateFixedControls(false);
        _fixedRadio.Checked += (_, _) => UpdateFixedControls(true);

        Content = new StackPanel
        {
            Spacing = 12,
            MinWidth = 420,
            Children =
            {
                _unavailableMessage,
                _sameAsSpokenRadio,
                _fixedRadio,
                _languageCombo,
                _customIsoBox,
                help
            }
        };

        PrimaryButtonClick += (_, _) => SaveSettings();
    }

    private void UpdateFixedControls(bool fixedMode)
    {
        _languageCombo.IsEnabled = _isAvailable && fixedMode;
        _customIsoBox.IsEnabled = _isAvailable && fixedMode;
    }

    private void SaveSettings()
    {
        if (!_isAvailable)
            return;

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
