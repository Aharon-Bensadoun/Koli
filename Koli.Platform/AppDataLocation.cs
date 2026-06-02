namespace Koli.Platform;

/// <summary>
/// Resolves writable application data paths. MSIX and per-machine installs under
/// Program Files use %LocalAppData%\Koli; dev/portable runs use the install folder.
/// </summary>
public static class AppDataLocation
{
    public const string AppFolderName = "Koli";

    public static string ResolveDataDirectory(string installDirectory, bool isPackagedApp)
    {
        if (isPackagedApp || IsInstalledUnderProgramFiles(installDirectory))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppFolderName);
        }

        return installDirectory;
    }

    public static string ResolveConfigDirectory(string installDirectory, bool isPackagedApp) =>
        Path.Combine(ResolveDataDirectory(installDirectory, isPackagedApp), "Config");

    public static bool IsInstalledUnderProgramFiles(string installDirectory)
    {
        if (string.IsNullOrWhiteSpace(installDirectory))
            return false;

        var normalizedInstall = NormalizeDirectoryPath(installDirectory);
        foreach (var programFilesRoot in GetProgramFilesRoots())
        {
            if (normalizedInstall.StartsWith(programFilesRoot, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> GetProgramFilesRoots()
    {
        yield return NormalizeDirectoryPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        yield return NormalizeDirectoryPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
    }

    private static string NormalizeDirectoryPath(string path)
    {
        var fullPath = Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return fullPath + Path.DirectorySeparatorChar;
    }
}
