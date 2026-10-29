using System.Text.Json;
using System.Text.Json.Serialization;
using SwarmSim.Core;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Configuration;

namespace SwarmSim.Cli;

internal sealed class RunManifest
{
    internal const string SupportedSpawnModel = "uniform-single-group-v1";
    internal static readonly string[] SupportedMetrics = ["population", "meanSpeed", "polarization"];
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public RunManifest() { }
    public required int SchemaVersion { get; init; }
    public required string RunId { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required string? GitCommit { get; init; }
    public required bool? DirtyWorktree { get; init; }
    public required string Engine { get; init; }
    public required uint Seed { get; init; }
    public required int Ticks { get; init; }
    public required int AgentCount { get; init; }
    public required float FixedDeltaTime { get; init; }
    public required JsonElement ResolvedConfig { get; init; }
    public required string[] RequestedMetrics { get; init; }
    public required string SpawnModel { get; init; }
    public required int KinematicHashVersion { get; init; }
    public required string FinalKinematicHash { get; init; }
    public required string MetricsSha256 { get; init; }
    public required RuntimeIdentity Runtime { get; init; }

    internal static RunManifest Read(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        RejectDuplicateProperties(document.RootElement, "$");
        RunManifest manifest = document.RootElement.Deserialize<RunManifest>(JsonOptions)
            ?? throw new InvalidDataException("A replay manifest must be a JSON object.");
        if (manifest.SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported manifest schemaVersion {manifest.SchemaVersion}; expected 1.");
        if (manifest.Engine is not ("legacy" or "canonical"))
            throw new InvalidDataException("Manifest engine must be legacy or canonical.");
        if (manifest.Ticks < 0 || manifest.AgentCount <= 0 || !float.IsFinite(manifest.FixedDeltaTime) || manifest.FixedDeltaTime <= 0f)
            throw new InvalidDataException("Manifest ticks, agentCount, or fixedDeltaTime is invalid.");
        if (manifest.RunId is null || !manifest.RunId.StartsWith("run-", StringComparison.Ordinal) ||
            !Guid.TryParseExact(manifest.RunId.AsSpan(4), "N", out _) || manifest.CreatedUtc == default)
            throw new InvalidDataException("Manifest run identity is invalid.");
        if (manifest.SpawnModel != SupportedSpawnModel || manifest.KinematicHashVersion != 1 ||
            manifest.RequestedMetrics is null || !manifest.RequestedMetrics.SequenceEqual(SupportedMetrics, StringComparer.Ordinal))
            throw new InvalidDataException("Manifest spawn model, hash version, or requested metrics are unsupported.");
        if (!IsHash(manifest.FinalKinematicHash, 64) || !IsHash(manifest.MetricsSha256, 64))
            throw new InvalidDataException("Manifest hashes must be SHA-256 hexadecimal values.");
        if ((manifest.GitCommit is null && manifest.DirtyWorktree is not null) ||
            (manifest.GitCommit is not null && !IsHash(manifest.GitCommit, 40) && !IsHash(manifest.GitCommit, 64)))
            throw new InvalidDataException("Manifest Git provenance must use a 40- or 64-character hexadecimal commit when known; dirtyWorktree requires a commit and may be null when build dirtiness is unknown.");
        if (manifest.Runtime is null || string.IsNullOrWhiteSpace(manifest.Runtime.Framework) ||
            string.IsNullOrWhiteSpace(manifest.Runtime.OperatingSystem) || string.IsNullOrWhiteSpace(manifest.Runtime.Architecture) ||
            string.IsNullOrWhiteSpace(manifest.Runtime.CliVersion))
            throw new InvalidDataException("Manifest runtime identity is incomplete.");
        return manifest;
    }

    private static void RejectDuplicateProperties(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                string propertyPath = path + "." + property.Name;
                if (!names.Add(property.Name))
                    throw new InvalidDataException($"Manifest contains duplicate field at '{propertyPath}'.");
                RejectDuplicateProperties(property.Value, propertyPath);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
                RejectDuplicateProperties(item, $"{path}[{index++}]");
        }
    }

    private static bool IsHash(string? value, int length) => value is not null && value.Length == length && value.All(Uri.IsHexDigit);
}

internal sealed class RuntimeIdentity
{
    public RuntimeIdentity() { }
    public required string Framework { get; init; }
    public required string OperatingSystem { get; init; }
    public required string Architecture { get; init; }
    public required string CliVersion { get; init; }
}

internal sealed record RunRequest(
    string Engine,
    int Ticks,
    int AgentCount,
    SimConfig? Legacy,
    CanonicalWorldSettings? Canonical,
    RunManifest? Replay)
{
    internal uint Seed => Canonical?.Seed ?? Legacy!.Seed;
    internal float FixedDeltaTime => Canonical?.FixedDeltaTime ?? Legacy!.FixedDeltaTime;
    internal string ConfigJson => Canonical?.ToJson() ?? Legacy!.ToJson();

    internal static RunRequest Resolve(RunOptions options)
    {
        if (options.ManifestPath is not null)
        {
            RunManifest manifest = RunManifest.Read(options.ManifestPath);
            Dictionary<string, JsonElement> values = ReadCompleteConfig(manifest);
            RunRequest replay = manifest.Engine == "canonical"
                ? new(manifest.Engine, manifest.Ticks, manifest.AgentCount, null, ConfigurationResolver.ResolveCanonicalSettings(overrides: values), manifest)
                : new(manifest.Engine, manifest.Ticks, manifest.AgentCount, ConfigurationResolver.ResolveSimConfig(overrides: values), null, manifest);
            if (replay.Seed != manifest.Seed || replay.FixedDeltaTime != manifest.FixedDeltaTime)
                throw new InvalidDataException("Manifest seed/fixedDeltaTime metadata does not match resolvedConfig.");
            return replay;
        }

        var overrides = new Dictionary<string, JsonElement>();
        if (options.Seed.HasValue)
            overrides[nameof(SimConfig.Seed)] = JsonSerializer.SerializeToElement(options.Seed.Value);
        return options.Engine == "canonical"
            ? new(options.Engine, options.Ticks, options.AgentCount, null,
                ConfigurationResolver.ResolveCanonicalSettings(options.Scenario, options.ConfigPath, overrides), null)
            : new(options.Engine, options.Ticks, options.AgentCount,
                ConfigurationResolver.ResolveSimConfig(options.Scenario, options.ConfigPath, overrides), null, null);
    }

    private static Dictionary<string, JsonElement> ReadCompleteConfig(RunManifest manifest)
    {
        if (manifest.ResolvedConfig.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Manifest resolvedConfig must be a complete native configuration object.");
        var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in manifest.ResolvedConfig.EnumerateObject())
            if (!values.TryAdd(property.Name, property.Value.Clone()))
                throw new InvalidDataException($"Manifest contains duplicate configuration field '{property.Name}'.");
        string defaults = manifest.Engine == "canonical" ? new CanonicalWorldSettings().ToJson() : new SimConfig().ToJson();
        using JsonDocument schema = JsonDocument.Parse(defaults);
        foreach (JsonProperty property in schema.RootElement.EnumerateObject())
            if (!values.ContainsKey(property.Name))
                throw new InvalidDataException($"Manifest resolvedConfig is missing '{property.Name}'; replay must not inherit current defaults.");
        return values;
    }
}
