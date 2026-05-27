using System.Security.Cryptography;
using System.Text;

namespace Koli.Config;

public sealed class SecureSettingsStore
{
    private readonly string _secretPath;

    public SecureSettingsStore(string baseDirectory)
    {
        _secretPath = Path.Combine(baseDirectory, "Config", "api.secret");
    }

    public async Task<string> ResolveApiKeyAsync(string? configuredKey, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            await PersistIfNeededAsync(configuredKey, cancellationToken).ConfigureAwait(false);
            return configuredKey!;
        }

        if (!File.Exists(_secretPath))
        {
            throw new InvalidOperationException("Azure OpenAI API key is not configured.");
        }

        var encrypted = await File.ReadAllBytesAsync(_secretPath, cancellationToken).ConfigureAwait(false);
        var decrypted = ProtectedData.Unprotect(encrypted, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(decrypted);
    }

    private async Task PersistIfNeededAsync(string apiKey, CancellationToken cancellationToken)
    {
        if (File.Exists(_secretPath))
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
