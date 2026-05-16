using System.Windows.Forms;
using Koli.Config;
using Koli.UI;

namespace Koli;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var configPath = Path.Combine(baseDirectory, "Config", "appsettings.json");

        // Try to find config in source directory if not found in output directory (for development)
        if (!File.Exists(configPath))
        {
            var sourceConfigPath = Path.Combine("Config", "appsettings.json");
            if (File.Exists(sourceConfigPath))
            {
                var configDir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                File.Copy(sourceConfigPath, configPath, overwrite: true);
            }
        }

        AppSettings settings;
        try
        {
            settings = AppSettings.Load(configPath);
        }
        catch (FileNotFoundException)
        {
            var configDir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(configDir))
            {
                Directory.CreateDirectory(configDir);
            }
            settings = new AppSettings();
        }

        // If the API key is missing, prompt the user to configure it now.
        // The user can either save a valid configuration or quit the app.
        if (string.IsNullOrWhiteSpace(settings.AzureOpenAI.ApiKey))
        {
            using var dialog = new ApiConfigurationDialog(settings.AzureOpenAI, isStartup: true);
            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            try
            {
                settings.Save(configPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not save the configuration file:\n{ex.Message}",
                    "Configuration error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
        }

        var secureStore = new SecureSettingsStore(baseDirectory);

        Application.Run(new MainForm(settings, secureStore, configPath));
    }
}
