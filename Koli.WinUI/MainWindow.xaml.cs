using Koli.Platform;
using Koli.WinUI.Services;
using Koli.WinUI.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace Koli.WinUI;

public sealed partial class MainWindow : Window
{
    private bool _isExiting;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Koli";
        SystemBackdrop = new MicaBackdrop();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Koli.ico");
        Stream? iconStream = File.Exists(iconPath) ? File.OpenRead(iconPath) : null;
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);

        AppServices.Current.TrayIcon.Initialize("Koli", iconStream);

        var ctx = AppServices.Current.Get<WindowContext>();
        ctx.GetWindowHandle = () => WindowNative.GetWindowHandle(this);
        ctx.DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        var hwnd = WindowNative.GetWindowHandle(this);
        AppServices.Current.Hotkeys.HotkeyPressed += OnHotkeyPressed;
        AppServices.Current.Hotkeys.Register(hwnd);

        AppServices.Current.Tray.ShowRequested += (_, _) => RestoreFromTray();
        AppServices.Current.Tray.ExitRequested += (_, _) => ExitApplication();

        Closed += MainWindow_Closed;
        AppWindow.Closing += AppWindow_Closing;

        ContentFrame.Navigate(typeof(HomePage));
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    public IntPtr WindowHandle => WindowNative.GetWindowHandle(this);

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is NavigationViewItem item && item.Tag is string tag)
            NavigateTo(tag);
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is NavigationViewItem item && item.Tag is string tag)
            NavigateTo(tag);
    }

    private void NavigateTo(string tag)
    {
        Type pageType = tag switch
        {
            "History" => typeof(HistoryPage),
            "Meeting" => typeof(MeetingPage),
            "Debug" => typeof(DebugPage),
            "Settings" => typeof(SettingsPage),
            _ => typeof(HomePage)
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
            ContentFrame.Navigate(pageType);
    }

    private void OnHotkeyPressed(object? sender, HotkeyAction action)
    {
        var dispatcher = AppServices.Current.Get<WindowContext>().DispatcherQueue;
        dispatcher.TryEnqueue(async () =>
        {
            var home = AppServices.Current.GetViewModel<ViewModels.HomeViewModel>();
            switch (action)
            {
                case HotkeyAction.ToggleRecording:
                    await home.ToggleRecordingCommand.ExecuteAsync(null);
                    break;
                case HotkeyAction.CancelRecording:
                    await home.CancelRecordingCommand.ExecuteAsync(null);
                    break;
                case HotkeyAction.TogglePause:
                    await home.TogglePauseCommand.ExecuteAsync(null);
                    break;
            }
        });
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExiting)
            return;

        args.Cancel = true;
        AppWindow.Hide();
        AppServices.Current.Toast.ShowInfo("Koli", "The application continues in the background.");
    }

    private void RestoreFromTray()
    {
        AppWindow.Show();
        Activate();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Cleanup();
        Close();
        Application.Current.Exit();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args) => Cleanup();

    private void Cleanup()
    {
        AppServices.Current.Hotkeys.Unregister();
        AppServices.Current.Hotkeys.HotkeyPressed -= OnHotkeyPressed;
        AppServices.Current.InputLanguage.StopMonitoring();
        AppServices.Current.History.Save();
        AppServices.Current.GetViewModel<ViewModels.HomeViewModel>().Dispose();
        AppServices.Current.Hotkeys.Dispose();
        AppServices.Current.TrayIcon.Dispose();
        AppServices.Current.AudioPlayback.Dispose();
    }
}

internal static class MainWindowHolder
{
    public static MainWindow? Instance { get; set; }
}
