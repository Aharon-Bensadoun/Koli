using Koli.Config;
using Koli.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Koli.WinUI.Dialogs;

public sealed class ApiConfigurationDialog : ContentDialog
{
    private const string EndpointKindOpenAi = "OpenAI";
    private const string EndpointKindOnPremise = "On-premise";

    private readonly AzureOpenAISettings _settings;
    private readonly PasswordBox _apiKeyPasswordBox;
    private readonly TextBox _apiKeyTextBox;
    private readonly CheckBox _showApiKeyCheckBox;
    private readonly ComboBox _endpointKindBox;
    private readonly TextBox _endpointBox;
    private readonly TextBlock _modelLabel;
    private readonly ComboBox _modelBox;
    private readonly TextBox _providerIdBox;
    private readonly CheckBox _noLogCheckBox;
    private readonly StackPanel _onPremStreamingPanel;
    private readonly CheckBox _enableStreamingCheckBox;
    private readonly TextBox _streamingEndpointBox;
    private readonly TextBox _streamingProviderIdBox;

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

        // Endpoint kind + optional on-prem URL
        var isOnPrem = IsOnPremEndpoint(settings.Endpoint);
        _endpointKindBox = new ComboBox
        {
            Header = "Endpoint",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _endpointKindBox.Items.Add(EndpointKindOpenAi);
        _endpointKindBox.Items.Add(EndpointKindOnPremise);
        _endpointKindBox.SelectedItem = isOnPrem ? EndpointKindOnPremise : EndpointKindOpenAi;
        panel.Children.Add(_endpointKindBox);

        _endpointBox = new TextBox
        {
            Header = "On-premise URL",
            Text = isOnPrem ? settings.Endpoint : string.Empty,
            PlaceholderText = "https://your-server.example.com/api/ai/queryAudio",
            Visibility = isOnPrem ? Visibility.Visible : Visibility.Collapsed,
            IsEnabled = isOnPrem
        };
        panel.Children.Add(_endpointBox);

        // Model (required for OpenAI; hidden for on-premise)
        _modelLabel = new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Visibility = isOnPrem ? Visibility.Collapsed : Visibility.Visible
        };
        _modelLabel.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = "Model" });
        _modelLabel.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
        {
            Text = " *",
            Foreground = Application.Current.Resources["AccentPrimaryBrush"] as Brush
        });
        panel.Children.Add(_modelLabel);
        _modelBox = new ComboBox
        {
            IsEditable = true,
            Text = settings.Model,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Visibility = isOnPrem ? Visibility.Collapsed : Visibility.Visible
        };
        _modelBox.Items.Add("gpt-4o-transcribe");
        _modelBox.Items.Add("whisper-1");
        _modelBox.Items.Add("gpt-realtime");
        _modelBox.Items.Add("gpt-realtime-whisper");
        panel.Children.Add(_modelBox);

        // Provider ID (primary choice for on-premise)
        _providerIdBox = new TextBox
        {
            Header = isOnPrem ? "Provider ID" : "Provider ID (on-prem, optional)",
            Text = settings.ProviderId?.ToString() ?? ""
        };
        panel.Children.Add(_providerIdBox);

        _noLogCheckBox = new CheckBox
        {
            Content = "Do not log requests in AI Nexus (noLog)",
            IsChecked = settings.NoLog,
            Visibility = isOnPrem ? Visibility.Visible : Visibility.Collapsed
        };
        panel.Children.Add(_noLogCheckBox);

        _enableStreamingCheckBox = new CheckBox
        {
            Content = "Live transcription (WebSocket realtime/transcribe)",
            IsChecked = settings.EnableStreamingTranscription
        };
        var realtimeEndpointBox = new TextBox
        {
            Header = "Realtime WebSocket URL (optional — empty = derived from Endpoint)",
            Text = settings.RealtimeEndpoint,
            PlaceholderText = "wss://your-server.example.com/api/ai/realtime/transcribe"
        };
        var httpFallbackCheckBox = new CheckBox
        {
            Content = "Fallback to queryAudio HTTP streaming if WebSocket fails",
            IsChecked = settings.UseQueryAudioHttpStreamingFallback
        };
        _streamingEndpointBox = new TextBox
        {
            Header = "HTTP streaming endpoint (fallback only)",
            Text = settings.StreamingEndpoint,
            PlaceholderText = "https://your-server.example.com/api/ai/queryAudio"
        };
        _streamingProviderIdBox = new TextBox
        {
            Header = "Streaming provider ID (optional — empty = Provider ID above)",
            Text = settings.StreamingProviderId?.ToString() ?? ""
        };
        // Advanced on-prem streaming options kept in code but hidden by default.
        _onPremStreamingPanel = new StackPanel
        {
            Spacing = 10,
            Visibility = Visibility.Collapsed
        };
        _onPremStreamingPanel.Children.Add(_enableStreamingCheckBox);
        _onPremStreamingPanel.Children.Add(realtimeEndpointBox);
        _onPremStreamingPanel.Children.Add(httpFallbackCheckBox);
        _onPremStreamingPanel.Children.Add(_streamingEndpointBox);
        _onPremStreamingPanel.Children.Add(_streamingProviderIdBox);
        panel.Children.Add(_onPremStreamingPanel);

        _endpointKindBox.SelectionChanged += (_, _) => UpdateEndpointUi();

        Content = new ScrollViewer
        {
            Content = panel,
            MaxHeight = 620,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        PrimaryButtonClick += (_, _) =>
        {
            var apiKey = ApiKeyValue.Trim();
            _settings.ApiKey = SecureSettingsStore.IsPlaceholderApiKey(apiKey) ? string.Empty : apiKey;
            _settings.Endpoint = IsOnPremiseSelected ? _endpointBox.Text.Trim() : string.Empty;
            if (!IsOnPremiseSelected)
            {
                _settings.Model = string.IsNullOrWhiteSpace(_modelBox.Text)
                    ? _modelBox.SelectedItem?.ToString() ?? settings.Model
                    : _modelBox.Text.Trim();
            }
            if (int.TryParse(_providerIdBox.Text, out var providerId))
                _settings.ProviderId = providerId;
            else
                _settings.ProviderId = null;
            _settings.NoLog = _noLogCheckBox.IsChecked == true;
            _settings.EnableStreamingTranscription = _enableStreamingCheckBox.IsChecked == true;
            _settings.RealtimeEndpoint = realtimeEndpointBox.Text.Trim();
            _settings.UseQueryAudioHttpStreamingFallback = httpFallbackCheckBox.IsChecked == true;
            _settings.StreamingEndpoint = _streamingEndpointBox.Text.Trim();
            if (int.TryParse(_streamingProviderIdBox.Text, out var streamingProviderId))
                _settings.StreamingProviderId = streamingProviderId;
            else
                _settings.StreamingProviderId = null;
            Result = true;
        };

        CloseButtonClick += (_, _) => Result = false;
    }

    private string ApiKeyValue =>
        _showApiKeyCheckBox.IsChecked == true ? _apiKeyTextBox.Text : _apiKeyPasswordBox.Password;

    private bool IsOnPremiseSelected =>
        Equals(_endpointKindBox.SelectedItem, EndpointKindOnPremise);

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

    private void UpdateEndpointUi()
    {
        var onPrem = IsOnPremiseSelected;
        _endpointBox.Visibility = onPrem ? Visibility.Visible : Visibility.Collapsed;
        _endpointBox.IsEnabled = onPrem;
        _modelLabel.Visibility = onPrem ? Visibility.Collapsed : Visibility.Visible;
        _modelBox.Visibility = onPrem ? Visibility.Collapsed : Visibility.Visible;
        _providerIdBox.Header = onPrem ? "Provider ID" : "Provider ID (on-prem, optional)";
        _noLogCheckBox.Visibility = onPrem ? Visibility.Visible : Visibility.Collapsed;
        // Keep advanced streaming options hidden; re-enable Visibility here when exposing them again.
        _onPremStreamingPanel.Visibility = Visibility.Collapsed;
        if (onPrem)
            _endpointBox.Focus(FocusState.Programmatic);
    }

    private static bool IsOnPremEndpoint(string? endpoint) =>
        OpenAiModelProfiles.IsOnPremiseStyleEndpoint(endpoint);
}
