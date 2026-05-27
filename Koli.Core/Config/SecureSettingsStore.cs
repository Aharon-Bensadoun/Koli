using System.Security.Cryptography;
using System.Text;

namespace Koli.Config;

public sealed class SecureSettingsStore
{
    private static readonly string[] PlaceholderApiKeys =
    [
        "sk-proj-yourapikey",
        "YOUR_API_KEY_HERE",
    ];

    private readonly string _secretPath;

    public SecureSettingsStore(string baseDirectory)
    {
        _secretPath = Path.Combine(baseDirectory, "Config", "api.secret");
    }

    public bool IsApiKeyConfigured(string? configuredKey) =>
        HasConfiguredKey(configuredKey) || TryReadSecretKey() != null;

    public static bool IsPlaceholderApiKey(string? configuredKey)
    {
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return false;
        }

        var trimmed = configuredKey.Trim();
        foreach (var placeholder in PlaceholderApiKeys)
        {
            if (trimmed.Equals(placeholder, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasConfiguredKey(string? configuredKey) =>
        !string.IsNullOrWhiteSpace(configuredKey) && !IsPlaceholderApiKey(configuredKey);

    public async Task<string?> TryResolveDisplayKeyAsync(string? configuredKey, CancellationToken cancellationToken = default)
    {
        if (!IsApiKeyConfigured(configuredKey))
        {
            return null;
        }

        try
        {
            return await ResolveApiKeyAsync(configuredKey, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public async Task<string> ResolveApiKeyAsync(string? configuredKey, CancellationToken cancellationToken)
    {
        if (HasConfiguredKey(configuredKey))
        {
            var key = configuredKey!.Trim();
            await PersistIfNeededAsync(key, cancellationToken).ConfigureAwait(false);
            return key;
        }

        var secretKey = await TryReadSecretKeyAsync(cancellationToken).ConfigureAwait(false);
        if (secretKey == null)
        {
            throw new InvalidOperationException("Azure OpenAI API key is not configured.");
        }

        return secretKey;
    }

    private string? TryReadSecretKey()
    {
        if (!File.Exists(_secretPath))
        {
            return null;
        }

        try
        {
            var encrypted = File.ReadAllBytes(_secretPath);
            return DecryptSecretKey(encrypted);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private async Task<string?> TryReadSecretKeyAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_secretPath))
        {
            return null;
        }

        try
        {
            var encrypted = await File.ReadAllBytesAsync(_secretPath, cancellationToken).ConfigureAwait(false);
            return DecryptSecretKey(encrypted);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static string? DecryptSecretKey(byte[] encrypted)
    {
        var decrypted = ProtectedData.Unprotect(encrypted, optionalEntropy: null, DataProtectionScope.CurrentUser);
        var key = Encoding.UTF8.GetString(decrypted);
        return IsPlaceholderApiKey(key) ? null : key;
    }

    private async Task PersistIfNeededAsync(string apiKey, CancellationToken cancellationToken)
    {
        if (IsPlaceholderApiKey(apiKey))
        {
            return;
        }

        if (File.Exists(_secretPath) && TryReadSecretKey() != null)
        {
            return;
        }

        var payload = ProtectedData.Protect(Encoding.UTF8.GetBytes(apiKey), optionalEntropy: null, DataProtectionScope.CurrentUser);
        var directory = Path.GetDirectoryName(_secretPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(_secretPath, payload, cancellationToken).ConfigureAwait(false);
    }
}
