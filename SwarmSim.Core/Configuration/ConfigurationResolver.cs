using System.Text.Json;
using SwarmSim.Core.Canonical;

namespace SwarmSim.Core.Configuration;

/// <summary>
/// Resolves configuration in one order: defaults, named scenario, file, explicit overrides.
/// Every layer is a sparse property overlay; arrays replace the whole property. Validation runs
/// after all layers have been applied. This is a construction-time API, outside simulation ticks.
/// Supplied defaults must be JSON-serializable; a later layer cannot repair non-finite defaults.
/// </summary>
public static class ConfigurationResolver
{
    // Only authored values belong here. Omitted fields continue to inherit each engine's defaults.
    // Recipe files remain independent overlays: peaceful.json and warbands.json intentionally
    // differ from the older PeacefulFlocks()/Warbands() scenarios with similar names.
    private static readonly IReadOnlyDictionary<string, string> Scenarios = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["balanced"] = """
            {"MaxSpeed":10,"Friction":0.95,"SenseRadius":110,"SeparationRadius":45,
             "SeparationWeight":7.5,"AlignmentWeight":2.2,"CohesionWeight":0.35,"MaxForce":2.5,
             "FieldOfView":270,"WanderStrength":0.45,"MaxNeighbors":16,"CollisionAvoidanceRadius":12,
             "CollisionAvoidanceBoost":4,"AttackDamage":0,"BaseDrain":0.1,"FixedDeltaTime":0.016666667}
            """,
        ["strong-separation"] = """
            {"MaxSpeed":10,"Friction":0.95,"SenseRadius":100,"SeparationRadius":40,
             "SeparationWeight":10,"AlignmentWeight":2,"CohesionWeight":0.3,"MaxForce":2,
             "FieldOfView":270,"WanderStrength":0.5,"FixedDeltaTime":0.016666667}
            """,
        ["tight-flocking"] = """
            {"MaxSpeed":10,"Friction":0.95,"SenseRadius":80,"SeparationRadius":20,
             "SeparationWeight":3,"AlignmentWeight":5,"CohesionWeight":2,"MaxForce":2,
             "FieldOfView":300,"WanderStrength":0.25,"FixedDeltaTime":0.016666667}
            """,
        ["fast-loose"] = """
            {"MaxSpeed":15,"Friction":0.9,"SenseRadius":120,"SeparationRadius":30,
             "SeparationWeight":8,"AlignmentWeight":4,"CohesionWeight":0.5,"MaxForce":3,
             "FieldOfView":270,"WanderStrength":0.75,"FixedDeltaTime":0.016666667}
            """,
        ["slow-cohesive"] = """
            {"MaxSpeed":5,"Friction":0.98,"SenseRadius":80,"SeparationRadius":25,
             "SeparationWeight":2,"AlignmentWeight":3,"CohesionWeight":3,"MaxForce":1.5,
             "FieldOfView":270,"WanderStrength":0.2,"FixedDeltaTime":0.016666667}
            """,
        ["peaceful-flocks"] = """
            {"MaxSpeed":10,"MaxForce":2,"SpeedModel":0,"Friction":1,"SeparationWeight":2,
             "AlignmentWeight":1.5,"CohesionWeight":1,"FieldOfView":270,"AttackDamage":0,"BaseDrain":0.1,
             "AggressionMatrix":[[0,0,0,0],[0,0,0,0],[0,0,0,0],[0,0,0,0]]}
            """,
        ["warbands"] = """
            {"SeparationWeight":1,"AlignmentWeight":1,"CohesionWeight":1.5,"AttackDamage":20,
             "AttackRadius":20,"AggressionMatrix":[[0,1,1,1],[1,0,1,1],[1,1,0,1],[1,1,1,0]]}
            """,
        ["rapid-evolution"] = """
            {"ReproductionEnergyThreshold":100,"ReproductionEnergyCost":30,"MutationRate":0.3,
             "MutationStdDev":0.3,"BaseDrain":0.3}
            """
    };

    /// <summary>Shared case-insensitive IDs; renderer shortcut labels are a separate UI concern.</summary>
    public static IReadOnlyList<string> ScenarioNames { get; } = Array.AsReadOnly(Scenarios.Keys.ToArray());

    /// <summary>
    /// Resolves a legacy-compatible config. File and override names are validated to catch typos
    /// and accidental use of another engine's schema. The older SimConfig loader stays permissive.
    /// </summary>
    public static SimConfig ResolveSimConfig(
        string? scenarioName = null,
        string? filePath = null,
        IReadOnlyDictionary<string, JsonElement>? overrides = null,
        SimConfig? defaults = null) => SimConfig.FromJson(
            ResolveJson(defaults ?? new SimConfig(), scenarioName, filePath, overrides, canonical: false));

    /// <summary>
    /// Resolves canonical settings with the same layering. Named scenarios map MaxSpeed to
    /// TargetSpeed and overlay only fields that exist in canonical settings. Canonical JSON files
    /// and explicit overrides use canonical names (including TargetSpeed).
    /// </summary>
    public static CanonicalWorldSettings ResolveCanonicalSettings(
        string? scenarioName = null,
        string? filePath = null,
        IReadOnlyDictionary<string, JsonElement>? overrides = null,
        CanonicalWorldSettings? defaults = null) => CanonicalWorldSettings.FromJson(
            ResolveJson(defaults ?? new CanonicalWorldSettings(), scenarioName, filePath, overrides, canonical: true));

    private static string ResolveJson<T>(
        T defaults,
        string? scenarioName,
        string? filePath,
        IReadOnlyDictionary<string, JsonElement>? overrides,
        bool canonical)
    {
        var merged = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        using (JsonDocument document = JsonSerializer.SerializeToDocument(defaults, ConfigurationJson.Options))
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
                merged[property.Name] = property.Value.Clone();

        // This single optional compatibility field is omitted by default serialization. Keep it
        // in the strict overlay schema without relaxing unknown-name checks for any other field.
        if (canonical)
            merged.TryAdd(nameof(CanonicalWorldSettings.Mode),
                JsonSerializer.SerializeToElement(CanonicalMode.Enhanced, ConfigurationJson.Options));

        if (!string.IsNullOrWhiteSpace(scenarioName))
        {
            if (!Scenarios.TryGetValue(scenarioName, out string? scenario))
                throw new ArgumentException($"Unknown scenario '{scenarioName}'.", nameof(scenarioName));
            OverlayJson(merged, scenario, mapCanonicalScenario: canonical, rejectUnknown: false);
        }

        if (!string.IsNullOrWhiteSpace(filePath))
            OverlayJson(merged, ConfigurationJson.ReadFile(filePath), mapCanonicalScenario: false, rejectUnknown: true);

        if (overrides is not null)
        {
            foreach ((string name, JsonElement value) in overrides)
            {
                if (!merged.ContainsKey(name))
                    throw new ArgumentException($"Unknown configuration override '{name}'.", nameof(overrides));
                merged[name] = value.Clone();
            }
        }

        return JsonSerializer.Serialize(merged, ConfigurationJson.Options);
    }

    private static void OverlayJson(Dictionary<string, JsonElement> merged, string json, bool mapCanonicalScenario, bool rejectUnknown)
    {
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("A configuration layer must be a JSON object.");

        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            string name = mapCanonicalScenario && property.NameEquals(nameof(SimConfig.MaxSpeed))
                ? nameof(CanonicalWorldSettings.TargetSpeed)
                : property.Name;
            if (merged.ContainsKey(name))
                merged[name] = property.Value.Clone();
            else if (rejectUnknown)
                throw new ArgumentException($"Unknown configuration field '{property.Name}' for the selected engine.");
        }
    }
}
