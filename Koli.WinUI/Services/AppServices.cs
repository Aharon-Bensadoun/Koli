using Koli.Config;

using Koli.Platform;

using Koli.Services;

using Koli.WinUI.ViewModels;

using Microsoft.Extensions.DependencyInjection;

using Microsoft.UI.Dispatching;



namespace Koli.WinUI.Services;



public sealed class AppServices

{

    private readonly IServiceProvider _services;



    public AppServices(IServiceProvider services) => _services = services;



    public static AppServices Current { get; private set; } = null!;



    public static void Initialize(IServiceProvider services) => Current = new AppServices(services);



    public IServiceProvider ServiceProvider => _services;



    public AppSettings Settings => _services.GetRequiredService<AppSettings>();

    public SecureSettingsStore SecureStore => _services.GetRequiredService<SecureSettingsStore>();

    public IAppPaths Paths => _services.GetRequiredService<IAppPaths>();

    public string ConfigPath => Paths.ConfigPath;

    public string BaseDirectory => Paths.BaseDirectory;

    public DebugLogService DebugLog => _services.GetRequiredService<DebugLogService>();

    public HistoryService History => _services.GetRequiredService<HistoryService>();

    public PendingAudioStore PendingAudio => _services.GetRequiredService<PendingAudioStore>();

    public AudioPlaybackService AudioPlayback => _services.GetRequiredService<AudioPlaybackService>();

    public TypingService Typing => _services.GetRequiredService<TypingService>();

    public GlobalHotkeyService Hotkeys => _services.GetRequiredService<GlobalHotkeyService>();

    public TrayIconService TrayIcon => _services.GetRequiredService<TrayIconService>();

    public TrayIconService Tray => TrayIcon;

    public ToastNotificationService Toast => _services.GetRequiredService<ToastNotificationService>();

    public InputLanguageService InputLanguage => _services.GetRequiredService<InputLanguageService>();



    public T Get<T>() where T : notnull => _services.GetRequiredService<T>();

    public T GetViewModel<T>() where T : notnull => Get<T>();



    public void SaveSettings() => Settings.Save(ConfigPath);



    public async Task<string> ResolveApiKeyAsync(CancellationToken cancellationToken = default) =>

        await SecureStore.ResolveApiKeyAsync(Settings.AzureOpenAI.ApiKey, cancellationToken);

}



public interface IAppPaths

{

    string BaseDirectory { get; }

    string ConfigPath { get; }

    string ConfigDirectory { get; }

}



public sealed class AppPaths : IAppPaths

{

    public AppPaths(string baseDirectory, string configPath)

    {

        BaseDirectory = baseDirectory;

        ConfigPath = configPath;

        ConfigDirectory = Path.GetDirectoryName(configPath) ?? baseDirectory;

    }



    public string BaseDirectory { get; }

    public string ConfigPath { get; }

    public string ConfigDirectory { get; }

}



public static class ServiceCollectionExtensions

{

    public static IServiceCollection AddKoliServices(this IServiceCollection services, AppSettings settings, IAppPaths paths)

    {

        services.AddSingleton(settings);

        services.AddSingleton(paths);

        services.AddSingleton(new SecureSettingsStore(paths.BaseDirectory));

        services.AddSingleton(sp => new DebugLogService(sp.GetRequiredService<WindowContext>()));

        services.AddSingleton(_ => new HistoryService(paths.ConfigDirectory));

        services.AddSingleton(_ =>

        {

            var pendingFolder = Path.Combine(paths.ConfigDirectory, "PendingAudio");

            var pendingIndex = Path.Combine(paths.ConfigDirectory, "pending-audio.json");

            return new PendingAudioStore(pendingFolder, pendingIndex);

        });

        services.AddSingleton<AudioPlaybackService>();

        services.AddSingleton<TypingService>();

        services.AddSingleton<GlobalHotkeyService>();

        services.AddSingleton<TrayIconService>();

        services.AddSingleton<WindowContext>();

        services.AddSingleton<WinUiToastPresenter>();

        services.AddSingleton<IToastPresenter>(sp => sp.GetRequiredService<WinUiToastPresenter>());

        services.AddSingleton<ToastNotificationService>();

        services.AddSingleton(sp =>

        {

            var toast = sp.GetRequiredService<ToastNotificationService>();

            return new InputLanguageService(settings, paths.ConfigPath, _ => { }, msg => toast.ShowError("Language", msg));

        });



        services.AddSingleton(sp => sp.GetRequiredService<WindowContext>().DispatcherQueue);

        services.AddSingleton<Func<IntPtr>>(_ => () => MainWindowHolder.Instance?.WindowHandle ?? IntPtr.Zero);



        services.AddSingleton<HomeViewModel>();

        services.AddTransient<HistoryViewModel>();

        services.AddTransient<MeetingViewModel>();

        services.AddTransient<DebugViewModel>();

        services.AddTransient<SettingsViewModel>();

        services.AddTransient<MainViewModel>();



        return services;

    }

}


