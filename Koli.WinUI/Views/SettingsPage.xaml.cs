using Microsoft.UI.Xaml.Controls;
using Koli.WinUI.ViewModels;
using Koli.Config;
using Koli.WinUI.Dialogs;
using Microsoft.UI.Xaml;

namespace Koli.WinUI.Views;

public sealed partial class SettingsPage : Page
{
    private SettingsViewModel? ViewModel => DataContext as SettingsViewModel;
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => DataContext = App.Services.Get<SettingsViewModel>();
    }

    private async void AddCustomAction_Click(object sender, RoutedEventArgs e) => await EditProfileAsync(null);

    private async void EditCustomAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CustomActionProfile profile })
            await EditProfileAsync(profile);
    }

    private void DeleteCustomAction_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null && sender is Button { Tag: CustomActionProfile profile })
            ViewModel.DeleteProfile(profile);
    }

    private async Task EditProfileAsync(CustomActionProfile? profile)
    {
        if (ViewModel == null)
            return;
        var dialog = new CustomActionProfileDialog(profile) { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;
        if (ViewModel.HasDuplicateHotkey(dialog.Profile))
        {
            var error = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Shortcut already assigned",
                Content = "Choose a different shortcut for this profile.",
                CloseButtonText = "OK"
            };
            await error.ShowAsync();
            return;
        }
        ViewModel.UpsertProfile(dialog.Profile);
    }
}
