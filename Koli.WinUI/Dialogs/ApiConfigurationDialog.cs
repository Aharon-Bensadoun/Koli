using Koli.Config;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Koli.WinUI.Dialogs;

public sealed class ApiConfigurationDialog : ContentDialog
{
    private readonly AzureOpenAISettings _settings;
    private readonly PasswordBox _apiKeyPasswordBox;
    private readonly TextBox _apiKeyTextBox;
    private readonly CheckBox _showApiKeyCheckBox;
    private readonly TextBox _endpointBox;
    private readonly ComboBox _modelBox;
    private readonly TextBox _providerIdBox;

    public bool Result { get; private set; }

    public ApiConfigurationDialog(AzureOpenAISettings settings, bool isStartup, string? displayApiKey = null)
    {
        _settings = settings;
        Title = "API configuration";
        PrimaryButtonText = "Save";
        CloseButtonText = isStartup ? "Quit" : "Cancel";
        DefaultButton = ContentDialogButton.Primary;

        var panel = new StackPanel { Spacing = 14, MinWidth = 480 };

        if (isStartup)
        {
            var intro = new InfoBar
            {
                Severity = InfoBarSeverity.Informational,
                IsOpen = true,
                IsClosable = false,
                Title = "Welcome to Koli",
                Message = "Enter your OpenAI or Azure OpenAI key to get started."
            };
            panel.Children.Add(intro);
        }

        var initialApiKey = !string.IsNullOrWhiteSpace(displayApiKey)
            ? displayApiKey
            : SecureSettingsStore.HasConfiguredKey(settings.ApiKey)
                ? settings.ApiKey
                : string.Empty;

        // API key field (required) with show/hide toggle
        var apiKeyHeader = new Grid();
        apiKeyHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        apiKeyHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var apiKeyLabel = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        apiKeyLabel.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = "API key" });
        apiKeyLabel.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
        {
            Text = " *",
            Foreground = Application.Current.Resources["AccentPrimaryBrush"] as Brush
        });
        Grid.SetColumn(apiKeyLabel, 0);
        apiKeyHeader.Children.Add(apiKeyLabel);

        _showApiKeyCheckBox = new CheckBox
        {
            Content = "Show",
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_showApiKeyCheckBox, 1);
        apiKeyHeader.Children.Add(_showApiKeyCheckBox);
        panel.Children.Add(apiKeyHeader);

        var apiKeyContainer = new Grid();
        _apiKeyPasswordBox = new PasswordBox { Password = initialApiKey, PlaceholderText = "sk-..." };
        _apiKeyTextBox = new TextBox
        {
            Text = initialApiKey,
            PlaceholderText = "sk-...",
            Visibility = Visibility.Collapsed,
            FontFamily = _apiKeyPasswordBox.FontFamily
        };
        apiKeyContainer.Children.Add(_apiKeyPasswordBox);
        apiKeyContainer.Children.Add(_apiKeyTextBox);
        panel.Children.Add(apiKeyContainer);

        _showApiKeyCheckBox.Checked += (_, _) => SetApiKeyVisible(true);
        _showApiKeyCheckBox.Unchecked += (_, _) => SetApiKeyVisible(false);

        // Endpoint (optional)
        _endpointBox = new TextBox
        {
            Header = "Endpoint (optional — empty = OpenAI cloud)",
            Text = settings.Endpoint,
            PlaceholderText = "https://api.openai.com"
        };
        panel.Children.Add(_endpointBox);

        // Model (required)
        var modelLabel = new TextBlock { FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        modelLabel.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = "Model" });
        modelLabel.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
        {
            Text = " *",
            Foreground = Application.Current.Resources["AccentPrimaryBrush"] as Brush
        });
        panel.Children.Add(modelLabel);
        _modelBox = new ComboBox
        {
            IsEditable = true,
            Text = settings.Model,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _modelBox.Items.Add("gpt-4o-transcribe");
        _modelBox.Items.Add("whisper-1");
        _modelBox.Items.Add("gpt-realtime");
        _modelBox.Items.Add("gpt-realtime-whisper");
        panel.Children.Add(_modelBox);

        // Provider ID (optional)
        _providerIdBox = new TextBox
        {
            Header = "Provider ID (on-prem, optional)",
            Text = settings.ProviderId?.ToString() ?? ""
        };
        panel.Children.Add(_providerIdBox);

        Content = panel;

        PrimaryButtonClick += (_, _) =>
        {
            var apiKey = ApiKeyValue.Trim();
            _settings.ApiKey = SecureSettingsStore.IsPlaceholderApiKey(apiKey) ? string.Empty : apiKey;
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

    private string ApiKeyValue =>
        _showApiKeyCheckBox.IsChecked == true ? _apiKeyTextBox.Text : _apiKeyPasswordBox.Password;

    private void SetApiKeyVisible(bool visible)
    {
        if (visible)
        {
            _apiKeyTextBox.Text = _apiKeyPasswordBox.Password;
            _apiKeyPasswordBox.Visibility = Visibility.Collapsed;
            _apiKeyTextBox.Visibility = Visibility.Visible;
            _apiKeyTextBox.Focus(FocusState.Programmatic);
            return;
        }

        _apiKeyPasswordBox.Password = _apiKeyTextBox.Text;
        _apiKeyTextBox.Visibility = Visibility.Collapsed;
        _apiKeyPasswordBox.Visibility = Visibility.Visible;
    }
}
