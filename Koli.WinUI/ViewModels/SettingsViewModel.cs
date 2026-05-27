using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koli.Config;
using Koli.Platform;
using Koli.WinUI.Dialogs;
using Koli.WinUI.Services;
using Microsoft.UI.Xaml.Controls;

namespace Koli.WinUI.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly IAppPaths _paths;
    private readonly ToastNotificationService _toast;
    private readonly InputLanguageService _inputLanguage;

    [ObservableProperty] private string _aboutText = "";
    [ObservableProperty] private bool _rewriteEnabled;
    [ObservableProperty] private string _rewriteLevel = "Professional";
    [ObservableProperty] private bool _translationEnabled;
    [ObservableProperty] private string _translationTarget = "";
    [ObservableProperty] private bool _typingAutoSpace = true;
    [ObservableProperty] private bool _typingInActiveWindow = true;
    [ObservableProperty] private bool _typingStreamingMode;

    public IReadOnlyList<string> RewriteLevels { get; } =
        ["Casual", "Polished", "Professional", "Formal", "Executive"];

    public SettingsViewModel(AppSettings settings, IAppPaths paths, ToastNotificationService toast, InputLanguageService inputLanguage)
    {
        _settings = settings;
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
        TranslationTarget = _settings.Translation.TargetLanguage;
        TypingAutoSpace = _settings.Typing.AutoSpace;
        TypingInActiveWindow = _settings.Typing.TypeInActiveWindow;
        TypingStreamingMode = _settings.Typing.StreamingMode;
    }

    [RelayCommand]
    private void Save()
    {
        _settings.Rewrite.Enabled = RewriteEnabled;
        _settings.Rewrite.ProfessionalismLevel = RewriteLevel;
        _settings.Translation.Enabled = TranslationEnabled;
        _settings.Translation.TargetLanguage = TranslationTarget;
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
        var dialog = new ApiConfigurationDialog(_settings.AzureOpenAI, isStartup: false);
        if (MainWindowHolder.Instance?.Content.XamlRoot != null)
            dialog.XamlRoot = MainWindowHolder.Instance.Content.XamlRoot;
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            _settings.Save(_paths.ConfigPath);
    }
}
