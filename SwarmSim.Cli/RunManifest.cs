using System.Text.Json;
using System.Text.Json.Serialization;
using SwarmSim.Core;
using SwarmSim.Core.Canonical;

namespace SwarmSim.Cli;

internal sealed class RunManifest
{
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MetricContractVersion { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? MetricsDeterministic { get; init; }
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
        if (manifest.SchemaVersion is not (1 or 2))
            throw new InvalidDataException($"Unsupported manifest schemaVersion {manifest.SchemaVersion}; expected 1 or 2.");
        EngineDefinition engine = EngineRegistry.Find(manifest.Engine)
            ?? throw new InvalidDataException($"Manifest engine must be {EngineRegistry.NameList}.");
        if (manifest.SchemaVersion < engine.MinimumManifestSchema)
            throw new InvalidDataException($"{engine.Name} manifests require schema {engine.MinimumManifestSchema}.");
        if (manifest.Ticks < 0 || manifest.AgentCount <= 0 || !float.IsFinite(manifest.FixedDeltaTime) || manifest.FixedDeltaTime <= 0f)
            throw new InvalidDataException("Manifest ticks, agentCount, or fixedDeltaTime is invalid.");
        if (manifest.RunId is null || !manifest.RunId.StartsWith("run-", StringComparison.Ordinal) ||
            !Guid.TryParseExact(manifest.RunId.AsSpan(4), "N", out _) || manifest.CreatedUtc == default)
            throw new InvalidDataException("Manifest run identity is invalid.");
        if (manifest.SpawnModel != engine.SpawnModel || manifest.KinematicHashVersion != 1)
            throw new InvalidDataException("Manifest spawn model or hash version is unsupported.");
        if (manifest.RequestedMetrics is null)
            throw new InvalidDataException("Manifest requestedMetrics must be an array of registered channel names.");
        if (manifest.SchemaVersion == 1)
        {
            if (!manifest.RequestedMetrics.SequenceEqual(MetricCatalog.DefaultMetrics, StringComparer.Ordinal) ||
                document.RootElement.TryGetProperty("metricContractVersion", out _) ||
                document.RootElement.TryGetProperty("metricsDeterministic", out _))
                throw new InvalidDataException("Schema 1 supports only its original three metric columns and no schema 2 metric metadata.");
        }
        else
        {
            if (manifest.MetricContractVersion != MetricCatalog.ContractVersion || manifest.MetricsDeterministic is null)
                throw new InvalidDataException("Manifest metricContractVersion or metricsDeterministic is missing or unsupported.");
            bool deterministic = MetricCatalog.ValidateSelection(manifest.Engine, manifest.RequestedMetrics);
            if (manifest.MetricsDeterministic != deterministic)
                throw new InvalidDataException("Manifest metricsDeterministic does not match the requested metric channels.");
        }
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
    object Settings,
    RunManifest? Replay)
{
    /// <summary>The original boids shape: exactly one of the two settings is present.</summary>
    internal RunRequest(string engine, int ticks, int agentCount, SimConfig? legacy, CanonicalWorldSettings? canonical, RunManifest? replay)
        : this(engine, ticks, agentCount, (object?)legacy ?? canonical!, replay) { }

    internal string[] RequestedMetrics { get; init; } = MetricCatalog.DefaultMetrics.ToArray();
    /// <summary>Also render filmstrip.png and report.html; never influences the simulation.</summary>
    internal bool Visual { get; init; }
    internal EngineDefinition Definition => EngineRegistry.Get(Engine);
    internal uint Seed => Definition.Seed(Settings);
    internal float FixedDeltaTime => Definition.FixedDeltaTime(Settings);
    internal string ConfigJson => Definition.SettingsJson(Settings);

    internal static RunRequest Resolve(RunOptions options)
    {
        if (options.ManifestPath is not null)
        {
            RunManifest manifest = RunManifest.Read(options.ManifestPath);
            if (manifest.MetricsDeterministic == false)
                throw new InvalidDataException("Exact replay is unavailable for manifests containing nondeterministic runtime metrics.");
            EngineDefinition engine = EngineRegistry.Get(manifest.Engine);
            Dictionary<string, JsonElement> values = ReadCompleteConfig(manifest, engine);
            var replay = new RunRequest(manifest.Engine, manifest.Ticks, manifest.AgentCount, engine.Resolve(null, null, values, null), manifest);
            if (replay.Seed != manifest.Seed || replay.FixedDeltaTime != manifest.FixedDeltaTime)
                throw new InvalidDataException("Manifest seed/fixedDeltaTime metadata does not match resolvedConfig.");
            return replay with { RequestedMetrics = manifest.RequestedMetrics.ToArray() };
        }

        var overrides = new Dictionary<string, JsonElement>();
        if (options.Seed.HasValue)
            overrides[nameof(SimConfig.Seed)] = JsonSerializer.SerializeToElement(options.Seed.Value);
        MetricCatalog.ValidateSelection(options.Engine, options.RequestedMetrics);
        object settings = EngineRegistry.Get(options.Engine).Resolve(options.Scenario, options.ConfigPath, overrides, null);
        return new RunRequest(options.Engine, options.Ticks, options.AgentCount, settings, null)
            { RequestedMetrics = options.RequestedMetrics.ToArray() };
    }

    private static Dictionary<string, JsonElement> ReadCompleteConfig(RunManifest manifest, EngineDefinition engine)
    {
        if (manifest.ResolvedConfig.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Manifest resolvedConfig must be a complete native configuration object.");
        var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in manifest.ResolvedConfig.EnumerateObject())
            if (!values.TryAdd(property.Name, property.Value.Clone()))
                throw new InvalidDataException($"Manifest contains duplicate configuration field '{property.Name}'.");
        using JsonDocument schema = JsonDocument.Parse(engine.DefaultSettingsJson());
        foreach (JsonProperty property in schema.RootElement.EnumerateObject())
        {
            if (engine.VersionedOptionalField is { } optional && property.NameEquals(optional.Name))
                continue;
            if (!values.ContainsKey(property.Name))
                throw new InvalidDataException($"Manifest resolvedConfig is missing '{property.Name}'; replay must not inherit current defaults.");
        }
        if (engine.VersionedOptionalField is { } pinned)
            values.TryAdd(pinned.Name, pinned.Meaning());
        return values;
    }
}
