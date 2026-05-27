using Koli.Config;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Koli.WinUI.Dialogs;

public sealed class ApiConfigurationDialog : ContentDialog
{
    private readonly AzureOpenAISettings _settings;
    private readonly PasswordBox _apiKeyBox;
    private readonly TextBox _endpointBox;
    private readonly ComboBox _modelBox;
    private readonly TextBox _providerIdBox;

    public bool Result { get; private set; }

    public ApiConfigurationDialog(AzureOpenAISettings settings, bool isStartup)
    {
        _settings = settings;
        Title = "API Configuration";
        PrimaryButtonText = "Save";
        CloseButtonText = isStartup ? "Quit" : "Cancel";
        DefaultButton = ContentDialogButton.Primary;

        var panel = new StackPanel { Spacing = 12, MinWidth = 480 };

        if (isStartup)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Configure your transcription API to start using Koli.",
                TextWrapping = TextWrapping.WrapWholeWords,
                Foreground = Application.Current.Resources["TextSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush
            });
        }

        panel.Children.Add(new TextBlock { Text = "API Key" });
        _apiKeyBox = new PasswordBox { Password = settings.ApiKey, PlaceholderText = "sk-..." };
        panel.Children.Add(_apiKeyBox);

        panel.Children.Add(new TextBlock { Text = "Endpoint (empty = OpenAI cloud)" });
        _endpointBox = new TextBox { Text = settings.Endpoint, PlaceholderText = "https://api.openai.com" };
        panel.Children.Add(_endpointBox);

        panel.Children.Add(new TextBlock { Text = "Model" });
        _modelBox = new ComboBox { IsEditable = true, Text = settings.Model };
        _modelBox.Items.Add("gpt-4o-transcribe");
        _modelBox.Items.Add("whisper-1");
        _modelBox.Items.Add("gpt-realtime");
        _modelBox.Items.Add("gpt-realtime-whisper");
        panel.Children.Add(_modelBox);

        panel.Children.Add(new TextBlock { Text = "Provider ID (on-prem, optional)" });
        _providerIdBox = new TextBox { Text = settings.ProviderId?.ToString() ?? "" };
        panel.Children.Add(_providerIdBox);

        Content = panel;

        PrimaryButtonClick += (_, _) =>
        {
            _settings.ApiKey = _apiKeyBox.Password;
            _settings.Endpoint = _endpointBox.Text.Trim();
            _settings.Model = string.IsNullOrWhiteSpace(_modelBox.Text) ? _modelBox.SelectedItem?.ToString() ?? settings.Model : _modelBox.Text.Trim();
            if (int.TryParse(_providerIdBox.Text, out var providerId))
                _settings.ProviderId = providerId;
            else
                _settings.ProviderId = null;
            Result = true;
        };

        CloseButtonClick += (_, _) => Result = false;
    }
}
