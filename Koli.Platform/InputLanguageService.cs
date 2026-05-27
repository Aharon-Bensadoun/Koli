using System.Runtime.InteropServices;
using Koli.Config;

namespace Koli.Platform;

public sealed class InputLanguageService : IDisposable
{
    private readonly AppSettings _settings;
    private readonly Action<string>? _onLanguageChanged;
    private readonly Action<string>? _onError;
    private readonly string _configPath;
    private System.Windows.Forms.Timer? _monitorTimer;
    private string _currentLanguage = "";

    public InputLanguageService(AppSettings settings, string configPath, Action<string>? onLanguageChanged = null, Action<string>? onError = null)
    {
        _settings = settings;
        _configPath = configPath;
        _onLanguageChanged = onLanguageChanged;
        _onError = onError;
        _currentLanguage = settings.AzureOpenAI.Language ?? "en";
    }

    public string CurrentLanguageCode => _settings.AzureOpenAI.Language ?? "en";

    public string GetLanguageButtonText()
    {
        if (_settings.AzureOpenAI.LanguageMode != "Auto")
            return (_settings.AzureOpenAI.ManualLanguage ?? "en").ToUpperInvariant();

        return (_settings.AzureOpenAI.Language ?? "en").ToUpperInvariant();
    }

    public void StartMonitoring()
    {
        _monitorTimer?.Stop();
        _monitorTimer?.Dispose();
        _monitorTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _monitorTimer.Tick += (_, _) => UpdateFromKeyboard();
        _monitorTimer.Start();
        UpdateFromKeyboard();
    }

    public void StopMonitoring()
    {
        _monitorTimer?.Stop();
        _monitorTimer?.Dispose();
        _monitorTimer = null;
    }

    public void UpdateFromKeyboard()
    {
        if (_settings.AzureOpenAI.LanguageMode != "Auto")
            return;

        try
        {
            var langId = GetActiveKeyboardLanguageId();
            if (langId == 0)
                return;

            ApplyLanguageId(langId);
        }
        catch (Exception ex)
        {
            _onError?.Invoke($"Error detecting keyboard language: {ex.Message}");
        }
    }

    public void HandleInputLanguageChange(nint lParam)
    {
        if (_settings.AzureOpenAI.LanguageMode != "Auto")
            return;

        var langId = (ushort)(lParam & 0xFFFF);
        ApplyLanguageId(langId);
    }

    private void ApplyLanguageId(ushort langId)
    {
        var languageCode = MapLanguageIdToCode(langId);
        if (_settings.AzureOpenAI.Language == languageCode)
        {
            _onLanguageChanged?.Invoke(GetLanguageButtonText());
            return;
        }

        _settings.AzureOpenAI.Language = languageCode;
        _currentLanguage = languageCode;
        _onLanguageChanged?.Invoke(GetLanguageButtonText());

        try { _settings.Save(_configPath); }
        catch (Exception ex) { _onError?.Invoke($"Error saving language setting: {ex.Message}"); }
    }

    private static ushort GetActiveKeyboardLanguageId()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow != IntPtr.Zero)
        {
            var foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, out _);
            if (foregroundThreadId != 0)
            {
                var foregroundLayout = GetKeyboardLayout(foregroundThreadId);
                if (foregroundLayout != IntPtr.Zero)
                    return (ushort)(foregroundLayout.ToInt64() & 0xFFFF);
            }
        }

        var keyboardLayout = GetKeyboardLayout(0);
        return keyboardLayout == IntPtr.Zero ? (ushort)0 : (ushort)(keyboardLayout.ToInt64() & 0xFFFF);
    }

    public static string MapLanguageIdToCode(ushort langId)
    {
        var primary = langId & 0x03FF;
        return primary switch
        {
            0x0009 => "en",
            0x000C => "fr",
            0x0007 => "de",
            0x000A => "es",
            0x0010 => "it",
            0x000D => "he",
            _ => "en"
        };
    }

    public void Dispose() => StopMonitoring();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);
}
