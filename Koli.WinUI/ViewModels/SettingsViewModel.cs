using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koli.Config;
using Koli.Platform;
using Koli.Services;
using Koli.WinUI.Dialogs;
using Koli.WinUI.Services;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;

namespace Koli.WinUI.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly SecureSettingsStore _secureStore;
    private readonly IAppPaths _paths;
    private readonly ToastNotificationService _toast;
    private readonly InputLanguageService _inputLanguage;
    private readonly StartupTaskService _startupTask;
    private readonly GlobalHotkeyService _hotkeys;
    private bool _loadingLaunchAtStartup;

    [ObservableProperty] private bool _isLaunchAtStartupAvailable;
    [ObservableProperty] private bool _launchAtStartup;
    [ObservableProperty] private bool _rewriteEnabled;
    [ObservableProperty] private string _rewriteLevel = "Professional";
    [ObservableProperty] private bool _translationEnabled;
    [ObservableProperty] private string _translationTarget = "en";
    [ObservableProperty] private string _outputLanguageMode = "SameAsSpoken";
    [ObservableProperty] private bool _isOutputLanguageAvailable = true;
    [ObservableProperty] private bool _isTargetLanguageEnabled;
    [ObservableProperty] private LanguagePickerItem? _selectedTargetLanguage;
    [ObservableProperty] private OutputLanguageModeItem? _selectedOutputLanguageMode;
    [ObservableProperty] private bool _typingAutoSpace = true;
    [ObservableProperty] private bool _typingInActiveWindow = true;
    [ObservableProperty] private bool _typingStreamingMode;
    [ObservableProperty] private bool _assistantEnabled = true;
    [ObservableProperty] private bool _assistantWebSearchEnabled = true;
    [ObservableProperty] private string _assistantModel = "gpt-4.1";
    [ObservableProperty] private bool _customActionsEnabled;
    [ObservableProperty] private string _customActionsDefaultModel = "gpt-4.1";
    [ObservableProperty] private string _customActionsDefaultProviderId = "";
    public ObservableCollection<CustomActionProfile> CustomActionProfiles { get; } = [];

    public IReadOnlyList<string> RewriteLevels { get; } =
        ["Casual", "Polished", "Professional", "Formal", "Executive"];

    public IReadOnlyList<OutputLanguageModeItem> OutputLanguageModes { get; } =
    [
        new() { Label = "Same as spoken", Value = "SameAsSpoken" },
        new() { Label = "Fixed language", Value = "Fixed" }
    ];

    public IReadOnlyList<LanguagePickerItem> TargetLanguageOptions { get; private set; } = [];

    public string AboutSummary { get; } =
        $"{AppInfo.ProductName} {AppInfo.Version}\n{AppInfo.Description}";

    public string AboutDeveloper { get; } = AppInfo.DeveloperName;
    public string ContactEmail { get; } = AppInfo.ContactEmail;
    public Uri ContactMailtoUri { get; } = new($"mailto:{AppInfo.ContactEmail}");
    public string RepositoryUrl { get; } = AppInfo.RepositoryUrl;
    public Uri RepositoryUri { get; } = new(AppInfo.RepositoryUrl);
    public string AboutCopyright { get; } = AppInfo.Copyright;

    public SettingsViewModel(AppSettings settings, SecureSettingsStore secureStore, IAppPaths paths, ToastNotificationService toast, InputLanguageService inputLanguage, StartupTaskService startupTask, GlobalHotkeyService hotkeys)
    {
        _settings = settings;
        _secureStore = secureStore;
        _paths = paths;
        _toast = toast;
        _inputLanguage = inputLanguage;
        _startupTask = startupTask;
        _hotkeys = hotkeys;
        LoadFromSettings();
        _ = LoadLaunchAtStartupAsync();
    }

    private async Task LoadLaunchAtStartupAsync()
    {
        IsLaunchAtStartupAvailable = _startupTask.IsAvailable;
        if (!IsLaunchAtStartupAvailable)
            return;

        _loadingLaunchAtStartup = true;
        LaunchAtStartup = await _startupTask.IsEnabledAsync();
        _loadingLaunchAtStartup = false;
    }

    partial void OnLaunchAtStartupChanged(bool value)
    {
        if (_loadingLaunchAtStartup)
            return;

        _ = ApplyLaunchAtStartupAsync(value);
    }

    private async Task ApplyLaunchAtStartupAsync(bool enabled)
    {
        var result = await _startupTask.SetEnabledAsync(enabled);
        if (result == StartupTaskChangeResult.Success)
            return;

        _loadingLaunchAtStartup = true;
        LaunchAtStartup = await _startupTask.IsEnabledAsync();
        _loadingLaunchAtStartup = false;

        var message = result switch
        {
            StartupTaskChangeResult.DisabledByUser =>
                "Startup was turned off in Windows Settings. Re-enable it under Settings → Apps → Startup apps.",
            StartupTaskChangeResult.DisabledByPolicy =>
                "Startup is blocked by a policy on this device.",
            StartupTaskChangeResult.NotAvailable =>
                "Launch at startup is only available in the MSIX-installed version of Koli.",
            _ => "Could not change startup setting. Try again or use Settings → Apps → Startup apps."
        };
        _toast.ShowError("Startup", message);
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
        TypingAutoSpace = _settings.Typing.AutoSpace;
        TypingInActiveWindow = _settings.Typing.TypeInActiveWindow;
        TypingStreamingMode = _settings.Typing.StreamingMode;
        AssistantEnabled = _settings.Assistant.Enabled;
        AssistantWebSearchEnabled = _settings.Assistant.WebSearchEnabled;
        AssistantModel = string.IsNullOrWhiteSpace(_settings.Assistant.Model)
            ? "gpt-4.1"
            : _settings.Assistant.Model.Trim();
        CustomActionsEnabled = _settings.CustomActions.Enabled;
        CustomActionsDefaultModel = _settings.CustomActions.DefaultOpenAiModel;
        CustomActionsDefaultProviderId = _settings.CustomActions.DefaultAiNexusProviderId?.ToString() ?? "";
        CustomActionProfiles.Clear();
        foreach (var profile in _settings.CustomActions.Profiles)
            CustomActionProfiles.Add(profile.Copy());
        RefreshTargetLanguageOptions();
        SelectedOutputLanguageMode = OutputLanguageModes.FirstOrDefault(m =>
            m.Value.Equals(OutputLanguageMode, StringComparison.OrdinalIgnoreCase))
            ?? OutputLanguageModes[0];
        UpdateTargetLanguageEnabled();
    }

    private void RefreshTargetLanguageOptions()
    {
        const string displayLocale = "en";
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
        _settings.Assistant.Enabled = AssistantEnabled;
        _settings.Assistant.WebSearchEnabled = AssistantWebSearchEnabled;
        _settings.Assistant.Model = string.IsNullOrWhiteSpace(AssistantModel) ? "gpt-4.1" : AssistantModel.Trim();
        _settings.CustomActions.Enabled = CustomActionsEnabled;
        _settings.CustomActions.DefaultOpenAiModel = string.IsNullOrWhiteSpace(CustomActionsDefaultModel) ? "gpt-4.1" : CustomActionsDefaultModel.Trim();
        _settings.CustomActions.DefaultAiNexusProviderId = int.TryParse(CustomActionsDefaultProviderId, out var defaultProviderId) ? defaultProviderId : null;
        _settings.CustomActions.Profiles = CustomActionProfiles.Select(profile => profile.Copy()).ToList();
        _settings.Save(_paths.ConfigPath);
        RegisterCompatibleCustomHotkeys();
        if (_hotkeys.CustomHotkeyErrors.Count > 0)
            _toast.ShowWarning("Custom shortcuts", "One or more shortcuts could not be registered. Check for duplicates or Windows conflicts.");
        _inputLanguage.StartMonitoring();
        _toast.ShowInfo("Settings", "Settings saved.");
    }

    public bool HasDuplicateHotkey(CustomActionProfile candidate) =>
        CustomActionProfiles.Any(profile => profile.Id != candidate.Id &&
            profile.Hotkey.ToString().Equals(candidate.Hotkey.ToString(), StringComparison.OrdinalIgnoreCase));

    public void UpsertProfile(CustomActionProfile profile)
    {
        var existing = CustomActionProfiles.FirstOrDefault(item => item.Id == profile.Id);
        if (existing != null)
            CustomActionProfiles[CustomActionProfiles.IndexOf(existing)] = profile;
        else
            CustomActionProfiles.Add(profile);
    }

    public void DeleteProfile(CustomActionProfile profile) => CustomActionProfiles.Remove(profile);

    private void RegisterCompatibleCustomHotkeys()
    {
        if (!_settings.CustomActions.Enabled)
        {
            _hotkeys.RegisterCustomHotkeys([]);
            return;
        }
        var isAiNexus = OpenAiModelProfiles.IsOnPremiseStyleEndpoint(_settings.AzureOpenAI.Endpoint);
        _hotkeys.RegisterCustomHotkeys(_settings.CustomActions.Profiles.Where(profile =>
            isAiNexus || !profile.PromptMode.Equals("AiNexusPromptId", StringComparison.OrdinalIgnoreCase)));
    }

    [RelayCommand]
    private async Task ConfigureApiAsync()
    {
        var displayApiKey = await _secureStore.TryResolveDisplayKeyAsync(_settings.AzureOpenAI.ApiKey, CancellationToken.None);
        var dialog = new ApiConfigurationDialog(_settings.AzureOpenAI, isStartup: false, displayApiKey);
        if (MainWindowHolder.Instance?.Content.XamlRoot != null)
            dialog.XamlRoot = MainWindowHolder.Instance.Content.XamlRoot;
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _settings.Save(_paths.ConfigPath);
            RegisterCompatibleCustomHotkeys();
        }
    }
}
