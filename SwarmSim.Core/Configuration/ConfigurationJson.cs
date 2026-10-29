using System.IO;
using System.Text.Json;

namespace SwarmSim.Core.Configuration;

internal static class ConfigurationJson
{
    internal static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    internal static string ReadFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Configuration file not found.", filePath);
        return File.ReadAllText(filePath);
    }

    internal static T Deserialize<T>(string json, Func<T, List<string>> validate) where T : class
    {
        T config = JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}: expected an object.");
        EnsureValid(validate(config));
        return config;
    }

    internal static string Serialize<T>(T config, List<string> errors)
    {
        EnsureValid(errors);
        return JsonSerializer.Serialize(config, Options);
    }

    private static void EnsureValid(List<string> errors)
    {
        if (errors.Count > 0)
            throw new ArgumentException($"Invalid config: {string.Join(", ", errors)}");
    }
}
