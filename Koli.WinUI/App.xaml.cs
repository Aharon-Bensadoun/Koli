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
            try
            {
                var logDir = IsPackagedApp()
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Koli", "Config")
                    : Path.Combine(AppContext.BaseDirectory, "Config");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "startup-error.log"),
                    $"[{DateTime.Now:O}] UNHANDLED: {e.Exception}{Environment.NewLine}");
            }
            catch { /* ignore */ }
            try { Services?.DebugLog.LogError("Unhandled exception", e.Exception); }
            catch { /* ignore */ }
        };
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppInfo.Initialize(typeof(App).Assembly);

        if (IsPackagedApp())
        {
            var package = Windows.ApplicationModel.Package.Current;
            var packageInstallLocation = package.InstalledLocation.Path;
            var packageVersion = package.Id.Version;
            AddRemoveProgramsRegistration.RegisterIfNeeded(
                packageInstallLocation,
                Path.Combine(packageInstallLocation, "Assets", "Koli.ico"),
                Path.Combine(packageInstallLocation, "Koli.exe"),
                $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}.{packageVersion.Revision}",
                AppInfo.DeveloperName);
        }

        var installDirectory = AppContext.BaseDirectory;
        var dataDirectory = IsPackagedApp()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Koli")
            : installDirectory;
        var configPath = Path.Combine(dataDirectory, "Config", "appsettings.json");
        EnsureConfigFile(configPath, installDirectory);

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
        catch (Exception ex)
        {
            settings = new AppSettings();
            try
            {
                var logPath = Path.Combine(Path.GetDirectoryName(configPath)!, "startup-error.log");
                File.WriteAllText(logPath, $"{DateTime.Now:O}{Environment.NewLine}{ex}");
            }
            catch { /* ignore */ }
        }

        var paths = new AppPaths(dataDirectory, configPath);
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
        window.Activate();

        if (!Services.SecureStore.IsApiKeyConfigured(settings.AzureOpenAI.ApiKey))
        {
            var xamlRoot = await WaitForXamlRootAsync(window);
            if (xamlRoot == null)
            {
                ShutdownApplication();
                return;
            }

            var dialog = new ApiConfigurationDialog(settings.AzureOpenAI, isStartup: true)
            {
                XamlRoot = xamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                ShutdownApplication();
                return;
            }

            try { settings.Save(configPath); }
            catch (Exception ex)
            {
                await ShowErrorAsync(window, ex.Message);
                ShutdownApplication();
                return;
            }
        }

        Services.InputLanguage.StartMonitoring();
    }

    private static void EnsureConfigFile(string configPath, string installDirectory)
    {
        if (File.Exists(configPath))
            return;

        var sourceConfigPath = Path.Combine(installDirectory, "Config", "appsettings.json");
        if (!File.Exists(sourceConfigPath))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.Copy(sourceConfigPath, configPath, overwrite: true);
    }

    private static bool IsPackagedApp()
    {
        try
        {
            return Windows.ApplicationModel.Package.Current != null;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<Microsoft.UI.Xaml.XamlRoot?> WaitForXamlRootAsync(Window window)
    {
        if (window.Content.XamlRoot != null)
            return window.Content.XamlRoot;

        var tcs = new TaskCompletionSource<Microsoft.UI.Xaml.XamlRoot?>();
        void OnLoaded(object sender, RoutedEventArgs e)
        {
            ((FrameworkElement)sender).Loaded -= OnLoaded;
            tcs.TrySetResult(((FrameworkElement)sender).XamlRoot);
        }
        ((FrameworkElement)window.Content).Loaded += OnLoaded;
        return await tcs.Task;
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

    private static void ShutdownApplication()
    {
        try { Services?.TrayIcon.Dispose(); }
        catch { /* ignore */ }
        try { Services?.InputLanguage.StopMonitoring(); }
        catch { /* ignore */ }
        Current.Exit();
    }
}
