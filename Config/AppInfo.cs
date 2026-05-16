using System.Reflection;

namespace Koli.Config;

/// <summary>
/// Application metadata shown in the About dialog (sourced from assembly attributes and project constants).
/// </summary>
internal static class AppInfo
{
    public const string ContactEmail = "bensadounaharon@gmail.com";
    public const string RepositoryUrl = "https://github.com/Aharon-Bensadoun/Koli";

    private static Assembly Assembly => Assembly.GetExecutingAssembly();

    public static string ProductName =>
        Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Koli";

    public static string Version
    {
        get
        {
            var informational = Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational))
            {
                var plus = informational.IndexOf('+');
                return plus >= 0 ? informational[..plus] : informational;
            }

            return Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        }
    }

    public static string DeveloperName =>
        Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company
        ?? "Aharon Bensadoun";

    public static string Copyright =>
        Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright
        ?? "Copyright © 2026 Aharon Bensadoun. All rights reserved.";

    public static string Description =>
        Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description
        ?? "Real-time speech transcription for Windows.";
}
