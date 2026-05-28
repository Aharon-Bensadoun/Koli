using Koli.Config;
using Koli.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Koli.WinUI.Dialogs;

public sealed class OutputLanguageSettingsDialog : ContentDialog
{
    private readonly TranslationSettings _settings;
    private readonly bool _isAvailable;
    private readonly RadioButton? _sameAsSpokenRadio;
    private readonly RadioButton? _fixedRadio;
    private readonly ComboBox? _languageCombo;
    private readonly TextBox? _customIsoBox;

    private readonly IReadOnlyList<(string Label, string Code)> _presetLanguages;

    public OutputLanguageSettingsDialog(TranslationSettings settings, string? apiEndpoint, string? displayLocale = null)
    {
        _settings = settings;
        _isAvailable = TranscriptionOutputLanguageService.IsOpenAiEndpoint(apiEndpoint);
        _presetLanguages = OutputLanguageCatalog.GetPresetOptions(displayLocale);

        Title = "Output language";
        PrimaryButtonText = "Save";
        CloseButtonText = "Cancel";
        IsPrimaryButtonEnabled = _isAvailable;

        TranscriptionOutputLanguageService.MigrateTranslationSettings(_settings);
        var isFixed = _settings.Mode.Equals("Fixed", StringComparison.OrdinalIgnoreCase)
                      && !string.IsNullOrWhiteSpace(_settings.TargetLanguage);

        var panel = new StackPanel { Spacing = 12, MinWidth = 420 };

        if (!_isAvailable)
        {
            panel.Children.Add(new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = InfoBarSeverity.Warning,
                Title = "Not available with this endpoint",
                Message = "Output language selection is only available with OpenAI or Azure OpenAI endpoints."
            });
            Content = panel;
            return;
        }

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
            Text = "For English with whisper-1, native audio translation is used.\n"
                   + "In Realtime mode, an automatic fallback may apply.",
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
        if (_languageCombo is null || _customIsoBox is null)
            return;
        _languageCombo.IsEnabled = fixedMode;
        _customIsoBox.IsEnabled = fixedMode;
    }

    private void SaveSettings()
    {
        if (!_isAvailable || _sameAsSpokenRadio is null || _customIsoBox is null || _languageCombo is null)
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
