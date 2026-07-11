using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace Koli.Config;

public sealed class MsiProfileProvisioningRequest
{
    public int SchemaVersion { get; set; } = 1;
    public string TargetSid { get; set; } = "";
    public string ProfileName { get; set; } = "Default";
    public string ApiKey { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public int? ProviderId { get; set; }
    public string? Model { get; set; }
    public bool OverwriteProfile { get; set; }
    public bool SetDefaultProfile { get; set; } = true;
}

public sealed class KoliProfileIndex
{
    public int SchemaVersion { get; set; } = 1;
    public string? DefaultProfileId { get; set; }
    public List<KoliProfileRecord> Profiles { get; set; } = [];
}

public sealed class KoliProfileRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string OwnerSid { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}

public static class MsiProfileProvisioning
{
    public const string ProvisioningFolderName = "Provisioning";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static string ResolveSid(string accountOrSid)
    {
        if (string.IsNullOrWhiteSpace(accountOrSid))
            throw new ArgumentException("A target Windows user or SID is required.", nameof(accountOrSid));

        var value = accountOrSid.Trim();
        if (value.StartsWith("S-1-", StringComparison.OrdinalIgnoreCase))
            return new SecurityIdentifier(value).Value;

        return ((SecurityIdentifier)new NTAccount(value).Translate(typeof(SecurityIdentifier))).Value;
    }

    public static string GetCurrentUserSid() =>
        WindowsIdentity.GetCurrent().User?.Value
        ?? throw new InvalidOperationException("Unable to resolve the current Windows user SID.");

    public static string WriteRequest(string programDataRoot, MsiProfileProvisioningRequest request)
    {
        Validate(request);
        var directory = Path.Combine(programDataRoot, "Koli", ProvisioningFolderName);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{request.TargetSid}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(request, JsonOptions));
        ApplyRestrictedAcl(path, request.TargetSid);
        return path;
    }

    public static bool ImportForCurrentUser(string programDataRoot, string dataDirectory, string templateConfigPath)
    {
        var sid = GetCurrentUserSid();
        var requestPath = Path.Combine(programDataRoot, "Koli", ProvisioningFolderName, $"{sid}.json");
        if (!File.Exists(requestPath))
            return false;

        var request = JsonSerializer.Deserialize<MsiProfileProvisioningRequest>(File.ReadAllText(requestPath), JsonOptions)
            ?? throw new InvalidOperationException("The MSI provisioning request is invalid.");
        Validate(request);
        if (!request.TargetSid.Equals(sid, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The MSI provisioning request belongs to another Windows user.");

        var profilesRoot = Path.Combine(dataDirectory, "Config", "Profiles");
        var indexPath = Path.Combine(dataDirectory, "Config", "profiles.json");
        Directory.CreateDirectory(profilesRoot);
        var index = File.Exists(indexPath)
            ? JsonSerializer.Deserialize<KoliProfileIndex>(File.ReadAllText(indexPath), JsonOptions) ?? new KoliProfileIndex()
            : new KoliProfileIndex();

        var existing = index.Profiles.FirstOrDefault(p =>
            p.OwnerSid.Equals(sid, StringComparison.OrdinalIgnoreCase)
            && p.Name.Equals(request.ProfileName, StringComparison.OrdinalIgnoreCase));
        if (existing != null && !request.OverwriteProfile)
            throw new InvalidOperationException($"Profile '{request.ProfileName}' already exists. Use KOLI_OVERWRITE_PROFILE=1 to replace it.");

        var record = existing ?? new KoliProfileRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = request.ProfileName,
            OwnerSid = sid,
            CreatedUtc = DateTime.UtcNow
        };
        if (existing == null)
            index.Profiles.Add(record);

        var profileDirectory = Path.Combine(profilesRoot, record.Id);
        Directory.CreateDirectory(profileDirectory);
        var profileConfigPath = Path.Combine(profileDirectory, "appsettings.json");
        if (!File.Exists(profileConfigPath))
            File.Copy(templateConfigPath, profileConfigPath, overwrite: false);

        var settings = AppSettings.Load(profileConfigPath);
        settings.AzureOpenAI.Endpoint = request.Endpoint;
        settings.AzureOpenAI.ProviderId = request.ProviderId;
        if (!string.IsNullOrWhiteSpace(request.Model))
            settings.AzureOpenAI.Model = request.Model.Trim();
        settings.AzureOpenAI.ApiKey = "";
        settings.Save(profileConfigPath);

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            var secureStore = new SecureSettingsStore(profileDirectory);
            secureStore.ResolveApiKeyAsync(request.ApiKey, CancellationToken.None).GetAwaiter().GetResult();
        }

        if (request.SetDefaultProfile || string.IsNullOrWhiteSpace(index.DefaultProfileId))
            index.DefaultProfileId = record.Id;
        File.WriteAllText(indexPath, JsonSerializer.Serialize(index, JsonOptions));

        var activeConfig = Path.Combine(dataDirectory, "Config", "appsettings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(activeConfig)!);
        File.Copy(profileConfigPath, activeConfig, overwrite: true);
        var profileSecret = Path.Combine(profileDirectory, "Config", "api.secret");
        var activeSecret = Path.Combine(dataDirectory, "Config", "api.secret");
        if (File.Exists(profileSecret))
            File.Copy(profileSecret, activeSecret, overwrite: true);

        File.Delete(requestPath);
        return true;
    }

    public static void Validate(MsiProfileProvisioningRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TargetSid))
            throw new ArgumentException("TargetSid is required.");
        _ = new SecurityIdentifier(request.TargetSid);
        if (string.IsNullOrWhiteSpace(request.ProfileName))
            throw new ArgumentException("ProfileName is required.");
        if (request.ProfileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("ProfileName contains invalid characters.");
        if (!string.IsNullOrWhiteSpace(request.Endpoint)
            && (!Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            throw new ArgumentException("Endpoint must be an absolute HTTP or HTTPS URL.");
        if (request.ProviderId is < 0)
            throw new ArgumentException("ProviderId must be zero or greater.");
    }

    private static void ApplyRestrictedAcl(string path, string targetSid)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var rights = FileSystemRights.Read | FileSystemRights.Write | FileSystemRights.Delete;
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(targetSid), rights, AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(security);
    }
}
