using Koli.Config;
using Koli.Platform;
using Koli.Services;
using Koli.WinUI.Dialogs;
using Koli.WinUI.Services;
using Koli.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Koli.WinUI;

public partial class App : Application
{
    public static AppServices Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            try { Services?.DebugLog.LogError("Unhandled exception", e.Exception); }
            catch { /* ignore */ }
        };
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppInfo.Initialize(typeof(App).Assembly);

        var baseDirectory = AppContext.BaseDirectory;
        var configPath = Path.Combine(baseDirectory, "Config", "appsettings.json");
        EnsureConfigFile(configPath);

        AppSettings settings;
        try
        {
            settings = AppSettings.Load(configPath);
        }
        catch (FileNotFoundException)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            settings = new AppSettings();
        }

        var paths = new AppPaths(baseDirectory, configPath);
        var services = new ServiceCollection();
        services.AddKoliServices(settings, paths);
        services.AddSingleton<MainWindow>();
        services.AddSingleton(sp =>
        {
            AppServices.Initialize(sp);
            return AppServices.Current;
        });

        var provider = services.BuildServiceProvider();
        Services = provider.GetRequiredService<AppServices>();

        var window = provider.GetRequiredService<MainWindow>();
        MainWindowHolder.Instance = window;

        if (string.IsNullOrWhiteSpace(settings.AzureOpenAI.ApiKey))
        {
            var dialog = new ApiConfigurationDialog(settings.AzureOpenAI, isStartup: true)
            {
                XamlRoot = window.Content.XamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                Exit();
                return;
            }

            try { settings.Save(configPath); }
            catch (Exception ex)
            {
                await ShowErrorAsync(window, ex.Message);
                Exit();
                return;
            }
        }

        Services.InputLanguage.StartMonitoring();
        window.Activate();
    }

    private static void EnsureConfigFile(string configPath)
    {
        if (File.Exists(configPath))
            return;

        var sourceConfigPath = Path.Combine("Config", "appsettings.json");
        if (!File.Exists(sourceConfigPath))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.Copy(sourceConfigPath, configPath, overwrite: true);
    }

    private static async Task ShowErrorAsync(Window window, string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Configuration error",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = window.Content.XamlRoot
        };
        await dialog.ShowAsync();
    }
}
