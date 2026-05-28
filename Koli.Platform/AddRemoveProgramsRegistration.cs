using System.Drawing;
using Microsoft.Win32;

namespace Koli.Platform;

/// <summary>
/// Registers sideloaded MSIX installs in Programs and Features (Add/Remove Programs).
/// MSIX packages appear in Settings > Apps but not in the legacy Control Panel list unless
/// an Uninstall registry entry is created explicitly.
/// </summary>
public static class AddRemoveProgramsRegistration
{
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Koli";
    private const string ProductName = "Koli";

    public static void RegisterIfNeeded(
        string installLocation,
        string? iconSourcePath,
        string executablePath,
        string version,
        string publisher)
    {
        try
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Koli");
            Directory.CreateDirectory(appDataDir);

            var uninstallScriptPath = Path.Combine(appDataDir, "Uninstall-Koli.ps1");
            var localIconPath = Path.Combine(appDataDir, "Koli.ico");
            EnsureUninstallScript(uninstallScriptPath);
            EnsureLocalIcon(iconSourcePath, executablePath, localIconPath);

            using var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath, writable: true);
            if (key == null)
                return;

            key.SetValue("DisplayName", ProductName);
            key.SetValue("DisplayVersion", version);
            key.SetValue("Publisher", publisher);
            key.SetValue("InstallLocation", installLocation.TrimEnd('\\', '/'));
            key.SetValue("DisplayIcon", localIconPath);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue(
                "UninstallString",
                $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{uninstallScriptPath}\"");
        }
        catch
        {
            // Non-fatal: the app must still launch if ARP registration fails.
        }
    }

    private static void EnsureLocalIcon(string? iconSourcePath, string executablePath, string destinationPath)
    {
        if (!string.IsNullOrWhiteSpace(iconSourcePath) && File.Exists(iconSourcePath))
        {
            File.Copy(iconSourcePath, destinationPath, overwrite: true);
            return;
        }

        if (!File.Exists(executablePath))
            return;

        using var icon = Icon.ExtractAssociatedIcon(executablePath);
        if (icon == null)
            return;

        using var stream = File.Create(destinationPath);
        icon.Save(stream);
    }

    private static void EnsureUninstallScript(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        const string content = """
            # Removes Koli and its Programs and Features entry.
            Get-AppxPackage -Name 'Koli' -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue
            Remove-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Koli' -Recurse -Force -ErrorAction SilentlyContinue
            """;

        if (!File.Exists(path) || File.ReadAllText(path) != content)
            File.WriteAllText(path, content);
    }
}
