using Koli.Config;
using Microsoft.UI.Xaml.Controls;

namespace Koli.WinUI.Dialogs;

public sealed class TranslationSettingsDialog : ContentDialog
{
    private readonly TranslationSettings _settings;
    private readonly ToggleSwitch _enabledSwitch;
    private readonly TextBox _targetBox;

    public TranslationSettingsDialog(TranslationSettings settings)
    {
        _settings = settings;
        Title = "Translation";
        PrimaryButtonText = "Save";
        CloseButtonText = "Cancel";

        _enabledSwitch = new ToggleSwitch
        {
            Header = "Translate transcription into another language",
            IsOn = settings.Enabled
        };
        _targetBox = new TextBox
        {
            Header = "Target language (ISO code, e.g. en, fr, he)",
            Text = settings.TargetLanguage,
            PlaceholderText = "en"
        };

        Content = new StackPanel
        {
            Spacing = 12,
            MinWidth = 420,
            Children = { _enabledSwitch, _targetBox }
        };

        PrimaryButtonClick += (_, _) =>
        {
            _settings.Enabled = _enabledSwitch.IsOn;
            _settings.TargetLanguage = _targetBox.Text.Trim();
        };
    }
}
