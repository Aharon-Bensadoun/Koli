using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koli.Config;
using Koli.Platform;
using Koli.Services;
using Koli.WinUI.Dialogs;
using Koli.WinUI.Services;
using Microsoft.UI.Xaml.Controls;

namespace Koli.WinUI.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly SecureSettingsStore _secureStore;
    private readonly IAppPaths _paths;
    private readonly ToastNotificationService _toast;
    private readonly InputLanguageService _inputLanguage;

    [ObservableProperty] private string _aboutText = "";
    [ObservableProperty] private bool _rewriteEnabled;
    [ObservableProperty] private string _rewriteLevel = "Professional";
    [ObservableProperty] private bool _translationEnabled;
    [ObservableProperty] private string _translationTarget = "en";
    [ObservableProperty] private string _outputLanguageMode = "SameAsSpoken";
    [ObservableProperty] private bool _isOutputLanguageAvailable = true;
    [ObservableProperty] private bool _showOnPremOutputLanguageNotice;
    [ObservableProperty] private bool _isTargetLanguageEnabled;
    [ObservableProperty] private LanguagePickerItem? _selectedTargetLanguage;
    [ObservableProperty] private OutputLanguageModeItem? _selectedOutputLanguageMode;
    [ObservableProperty] private bool _typingAutoSpace = true;
    [ObservableProperty] private bool _typingInActiveWindow = true;
    [ObservableProperty] private bool _typingStreamingMode;

    public IReadOnlyList<string> RewriteLevels { get; } =
        ["Casual", "Polished", "Professional", "Formal", "Executive"];

    public IReadOnlyList<OutputLanguageModeItem> OutputLanguageModes { get; } =
    [
        new() { Label = "Same as spoken", Value = "SameAsSpoken" },
        new() { Label = "Fixed language", Value = "Fixed" }
    ];

    public IReadOnlyList<LanguagePickerItem> TargetLanguageOptions { get; private set; } = [];

    public SettingsViewModel(AppSettings settings, SecureSettingsStore secureStore, IAppPaths paths, ToastNotificationService toast, InputLanguageService inputLanguage)
    {
        _settings = settings;
        _secureStore = secureStore;
        _paths = paths;
        _toast = toast;
        _inputLanguage = inputLanguage;
        LoadFromSettings();
        AboutText = $"{AppInfo.ProductName} {AppInfo.Version}\n{AppInfo.Description}\n\n{AppInfo.DeveloperName}\n{AppInfo.ContactEmail}\n{AppInfo.RepositoryUrl}\n\n{AppInfo.Copyright}";
    }

    private void LoadFromSettings()
    {
        RewriteEnabled = _settings.Rewrite.Enabled;
        RewriteLevel = _settings.Rewrite.ProfessionalismLevel;
        TranslationEnabled = _settings.Translation.Enabled;
        TranslationTarget = string.IsNullOrWhiteSpace(_settings.Translation.TargetLanguage)
            ? "en"
            : _settings.Translation.TargetLanguage.Trim().ToLowerInvariant();
        OutputLanguageMode = _settings.Translation.Mode;
        IsOutputLanguageAvailable = TranscriptionOutputLanguageService.IsOutputLanguageSupported(_settings);
        ShowOnPremOutputLanguageNotice = !IsOutputLanguageAvailable;
        TypingAutoSpace = _settings.Typing.AutoSpace;
        TypingInActiveWindow = _settings.Typing.TypeInActiveWindow;
        TypingStreamingMode = _settings.Typing.StreamingMode;
        RefreshTargetLanguageOptions();
        SelectedOutputLanguageMode = OutputLanguageModes.FirstOrDefault(m =>
            m.Value.Equals(OutputLanguageMode, StringComparison.OrdinalIgnoreCase))
            ?? OutputLanguageModes[0];
        UpdateTargetLanguageEnabled();
    }

    private void RefreshTargetLanguageOptions()
    {
        var displayLocale = _settings.AzureOpenAI.Language;
        TargetLanguageOptions = OutputLanguageCatalog.GetPresetOptions(displayLocale)
            .Select(p => new LanguagePickerItem { Label = p.DisplayName, Code = p.Code })
            .ToList();

        var code = string.IsNullOrWhiteSpace(TranslationTarget) ? "en" : TranslationTarget.Trim().ToLowerInvariant();
        SelectedTargetLanguage = TargetLanguageOptions.FirstOrDefault(o => o.Code == code)
            ?? TargetLanguageOptions.FirstOrDefault(o => o.Code == "en");
    }

    private void UpdateTargetLanguageEnabled() =>
        IsTargetLanguageEnabled = IsOutputLanguageAvailable
            && OutputLanguageMode.Equals("Fixed", StringComparison.OrdinalIgnoreCase);

    partial void OnSelectedTargetLanguageChanged(LanguagePickerItem? value)
    {
        if (value != null)
            TranslationTarget = value.Code;
    }

    partial void OnSelectedOutputLanguageModeChanged(OutputLanguageModeItem? value)
    {
        if (value != null)
            OutputLanguageMode = value.Value;
        UpdateTargetLanguageEnabled();
    }

    [RelayCommand]
    private void Save()
    {
        _settings.Rewrite.Enabled = RewriteEnabled;
        _settings.Rewrite.ProfessionalismLevel = RewriteLevel;
        _settings.Translation.Enabled = TranslationEnabled;
        _settings.Translation.TargetLanguage = OutputLanguageMode.Equals("Fixed", StringComparison.OrdinalIgnoreCase)
            ? (SelectedTargetLanguage?.Code ?? TranslationTarget)
            : "";
        _settings.Translation.Mode = OutputLanguageMode;
        TranscriptionOutputLanguageService.SyncLegacyEnabledFlag(_settings.Translation);
        _settings.Typing.AutoSpace = TypingAutoSpace;
        _settings.Typing.TypeInActiveWindow = TypingInActiveWindow;
        _settings.Typing.StreamingMode = TypingStreamingMode;
        _settings.Save(_paths.ConfigPath);
        _inputLanguage.StartMonitoring();
        _toast.ShowInfo("Settings", "Settings saved.");
    }

    [RelayCommand]
    private async Task ConfigureApiAsync()
    {
        var displayApiKey = await _secureStore.TryResolveDisplayKeyAsync(_settings.AzureOpenAI.ApiKey, CancellationToken.None);
        var dialog = new ApiConfigurationDialog(_settings.AzureOpenAI, isStartup: false, displayApiKey);
        if (MainWindowHolder.Instance?.Content.XamlRoot != null)
            dialog.XamlRoot = MainWindowHolder.Instance.Content.XamlRoot;
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            _settings.Save(_paths.ConfigPath);
    }
}
