using Microsoft.Extensions.Configuration;

namespace Core.Configuration;

public static class SecretFileConfigurationExtensions
{
    public static void ApplyFileBackedSecrets(
        this ConfigurationManager configuration,
        params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (keys is null || keys.Length == 0) return;

        var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in keys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(key)) continue;

            var fileEnvName = $"{key.Replace(":", "__")}_FILE";

            var filePath = Environment.GetEnvironmentVariable(fileEnvName);
            if (string.IsNullOrWhiteSpace(filePath)) continue;

            if (!File.Exists(filePath))
                throw new FileNotFoundException(
                    $"Secret file '{filePath}' configured by '{fileEnvName}' was not found.");

            var value = File.ReadAllText(filePath).Trim();
            overrides[key] = value;
        }

        if (overrides.Count > 0)
            configuration.AddInMemoryCollection(overrides);
    }
}
