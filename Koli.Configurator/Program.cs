using Koli.Config;

try
{
    var options = ParseArguments(args);
    var target = Get(options, "target-sid")
        ?? Get(options, "target-user")
        ?? GetRequired(options, "fallback-user");
    var request = new MsiProfileProvisioningRequest
    {
        TargetSid = MsiProfileProvisioning.ResolveSid(target),
        ProfileName = Get(options, "profile-name") ?? "Default",
        ApiKey = Get(options, "api-key") ?? "",
        Endpoint = Get(options, "endpoint") ?? "",
        ProviderId = ParseNullableInt(Get(options, "provider-id"), "provider-id"),
        Model = Get(options, "model"),
        OverwriteProfile = ParseBool(Get(options, "overwrite-profile")),
        SetDefaultProfile = !options.TryGetValue("set-default-profile", out var defaultValue) || ParseBool(defaultValue)
    };
    var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    MsiProfileProvisioning.WriteRequest(programData, request);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Koli MSI provisioning failed: {ex.Message}");
    return 2;
}

static Dictionary<string, string> ParseArguments(string[] args)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i += 2)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
            throw new ArgumentException("Arguments must use --name value pairs.");
        result[args[i][2..]] = args[i + 1];
    }
    return result;
}

static string GetRequired(Dictionary<string, string> values, string name) =>
    Get(values, name) ?? throw new ArgumentException($"--{name} is required.");

static string? Get(Dictionary<string, string> values, string name) =>
    values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

static int? ParseNullableInt(string? value, string name) => value == null
    ? null
    : int.TryParse(value, out var parsed) ? parsed : throw new ArgumentException($"--{name} must be an integer.");

static bool ParseBool(string? value) => value?.Trim() is "1" or "true" or "TRUE" or "True";
