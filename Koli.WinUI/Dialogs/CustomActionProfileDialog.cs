using Koli.Config;
using Koli.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Koli.WinUI.Dialogs;

public sealed class CustomActionProfileDialog : ContentDialog
{
    private readonly TextBox _name = new() { Header = "Name" };
    private readonly CheckBox _enabled = new() { Content = "Enabled" };
    private readonly CheckBox _ctrl = new() { Content = "Ctrl" };
    private readonly CheckBox _alt = new() { Content = "Alt" };
    private readonly CheckBox _shift = new() { Content = "Shift" };
    private readonly CheckBox _win = new() { Content = "Win" };
    private readonly TextBox _key = new() { Header = "Key", PlaceholderText = "M, Space, F10...", MinWidth = 130 };
    private readonly ComboBox _languageMode = new() { Header = "Language mode", ItemsSource = new[] { "Auto", "Fixed" } };
    private readonly TextBox _language = new() { Header = "Fixed language", PlaceholderText = "fr" };
    private readonly ComboBox _promptMode = new() { Header = "Prompt source", ItemsSource = new[] { "InlineSystemPrompt", "AiNexusPromptId" } };
    private readonly TextBox _systemPrompt = new() { Header = "System prompt", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120 };
    private readonly TextBox _promptId = new() { Header = "AI Nexus prompt ID" };
    private readonly TextBox _openAiModel = new() { Header = "OpenAI model override", PlaceholderText = "Use default" };
    private readonly TextBox _providerId = new() { Header = "AI Nexus provider ID override", PlaceholderText = "Use default" };
    private readonly InfoBar _error = new() { Severity = InfoBarSeverity.Error, IsOpen = false, IsClosable = false };

    public CustomActionProfile Profile { get; }

    public CustomActionProfileDialog(CustomActionProfile? profile = null)
    {
        Profile = profile?.Copy() ?? new CustomActionProfile();
        Title = profile == null ? "Add custom action" : "Edit custom action";
        PrimaryButtonText = "Save";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;

        _name.Text = Profile.Name;
        _enabled.IsChecked = Profile.Enabled;
        _ctrl.IsChecked = Profile.Hotkey.Ctrl;
        _alt.IsChecked = Profile.Hotkey.Alt;
        _shift.IsChecked = Profile.Hotkey.Shift;
        _win.IsChecked = Profile.Hotkey.Win;
        _key.Text = Profile.Hotkey.Key;
        _languageMode.SelectedItem = Profile.LanguageMode;
        _language.Text = Profile.Language;
        _promptMode.SelectedItem = Profile.PromptMode;
        _systemPrompt.Text = Profile.SystemPrompt;
        _promptId.Text = Profile.AiNexusPromptId?.ToString() ?? "";
        _openAiModel.Text = Profile.OpenAiModel ?? "";
        _providerId.Text = Profile.AiNexusProviderId?.ToString() ?? "";

        var modifiers = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        modifiers.Children.Add(_ctrl);
        modifiers.Children.Add(_alt);
        modifiers.Children.Add(_shift);
        modifiers.Children.Add(_win);
        var hotkey = new StackPanel { Spacing = 6 };
        hotkey.Children.Add(new TextBlock { Text = "Global shortcut", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        hotkey.Children.Add(modifiers);
        hotkey.Children.Add(_key);

        var panel = new StackPanel { Spacing = 12, MinWidth = 500 };
        panel.Children.Add(_error);
        panel.Children.Add(_name);
        panel.Children.Add(_enabled);
        panel.Children.Add(hotkey);
        panel.Children.Add(_languageMode);
        panel.Children.Add(_language);
        panel.Children.Add(_promptMode);
        panel.Children.Add(_systemPrompt);
        panel.Children.Add(_promptId);
        panel.Children.Add(_openAiModel);
        panel.Children.Add(_providerId);
        Content = new ScrollViewer { Content = panel, MaxHeight = 620, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        PrimaryButtonClick += ValidateAndApply;
    }

    private void ValidateAndApply(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var mode = _promptMode.SelectedItem?.ToString() ?? "InlineSystemPrompt";
        var hotkey = new CustomHotkey
        {
            Ctrl = _ctrl.IsChecked == true,
            Alt = _alt.IsChecked == true,
            Shift = _shift.IsChecked == true,
            Win = _win.IsChecked == true,
            Key = _key.Text.Trim()
        };
        string? message = null;
        if (string.IsNullOrWhiteSpace(_name.Text))
            message = "Name is required.";
        else if (!GlobalHotkeyService.TryResolveHotkey(hotkey, out _, out _, out var hotkeyError))
            message = hotkeyError;
        else if (mode == "InlineSystemPrompt" && string.IsNullOrWhiteSpace(_systemPrompt.Text))
            message = "System prompt is required.";
        else if (mode == "AiNexusPromptId" && (!int.TryParse(_promptId.Text, out var promptId) || promptId <= 0))
            message = "Enter a valid positive AI Nexus prompt ID.";

        if (message != null)
        {
            _error.Message = message;
            _error.IsOpen = true;
            args.Cancel = true;
            return;
        }

        Profile.Name = _name.Text.Trim();
        Profile.Enabled = _enabled.IsChecked == true;
        Profile.Hotkey = hotkey;
        Profile.LanguageMode = _languageMode.SelectedItem?.ToString() ?? "Auto";
        Profile.Language = string.IsNullOrWhiteSpace(_language.Text) ? "en" : _language.Text.Trim().ToLowerInvariant();
        Profile.PromptMode = mode;
        Profile.SystemPrompt = _systemPrompt.Text.Trim();
        Profile.AiNexusPromptId = int.TryParse(_promptId.Text, out var id) ? id : null;
        Profile.OpenAiModel = string.IsNullOrWhiteSpace(_openAiModel.Text) ? null : _openAiModel.Text.Trim();
        Profile.AiNexusProviderId = int.TryParse(_providerId.Text, out var providerId) ? providerId : null;
    }
}
