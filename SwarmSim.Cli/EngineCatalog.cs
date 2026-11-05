using System.Text.Json;

namespace SwarmSim.Cli;

/// <summary>Read-only discovery from the same definitions that construct actual simulations.</summary>
internal static class EngineCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    internal static void Write(RunOptions options, TextWriter output)
    {
        if (options.ListEngines)
        {
            if (options.Json)
                output.WriteLine(JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    engines = EngineRegistry.All.Select(Summary).ToArray()
                }, JsonOptions));
            else
            {
                output.WriteLine("Available simulation engines:");
                foreach (EngineDefinition engine in EngineRegistry.All)
                    output.WriteLine($"  {engine.Name} — {engine.Description.Summary}");
                output.WriteLine("Inspect: --engine NAME --describe-engine [--json]");
                output.WriteLine("Run: --engine NAME --config FILE --ticks 600 --out runs");
            }
            return;
        }

        EngineDefinition selected = EngineRegistry.Get(options.Engine);
        using JsonDocument defaults = JsonDocument.Parse(selected.DefaultSettingsJson());
        IReadOnlyList<MetricDescription> metrics = MetricCatalog.Describe(selected.Name);
        if (options.Json)
        {
            output.WriteLine(JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                engine = new
                {
                    name = selected.Name,
                    summary = selected.Description.Summary,
                    state = selected.Description.State,
                    interactiveRenderer = selected.Description.InteractiveRenderer,
                    documentation = selected.Description.Documentation,
                    acceptsScenario = selected.AcceptsScenario,
                    supportsKinematics = selected.SupportsKinematics,
                    defaultAgentCount = selected.DefaultAgentCount,
                    agentCountContract = selected.SupportsKinematics ? "positive" : "zero; grid dimensions are native configuration",
                    minimumManifestSchema = selected.MinimumManifestSchema,
                    spawnModel = selected.SpawnModel,
                    modelStateProfile = selected.ModelStateProfile,
                    checkpointRestore = false,
                    defaultConfiguration = defaults.RootElement,
                    defaultMetrics = MetricCatalog.DefaultsFor(selected.Name),
                    metrics
                }
            }, JsonOptions));
            return;
        }

        output.WriteLine($"{selected.Name} — {selected.Description.Summary}");
        output.WriteLine("Principal state: " + string.Join(", ", selected.Description.State));
        output.WriteLine(selected.SupportsKinematics
            ? $"Kinematic agents: available; --agents is positive and defaults to {selected.DefaultAgentCount}."
            : "Field-only observations: --agents is 0; grid dimensions are native configuration. Shared motion and exact-neighbor metrics are unavailable.");
        output.WriteLine("Interactive renderer: " + (selected.Description.InteractiveRenderer ? "available" : "not integrated; use CLI --visual reports"));
        output.WriteLine($"Native configuration: defaults below, then --config overrides; named boids scenarios {(selected.AcceptsScenario ? "accepted" : "not accepted")}.");
        output.WriteLine($"Spawn: {selected.SpawnModel}; minimum manifest schema: {selected.MinimumManifestSchema}.");
        output.WriteLine("Model-state verification: " + (selected.ModelStateProfile ?? "unavailable; kinematic v1 identity only"));
        output.WriteLine("Replay reruns from initialization; checkpoint restore is not implemented.");
        output.WriteLine("Guide: " + selected.Description.Documentation);
        output.WriteLine($"Run: --engine {selected.Name} --ticks 600 --out runs --visual");
        output.WriteLine("Default configuration:");
        output.WriteLine(selected.DefaultSettingsJson());
        MetricCatalog.WriteAvailable(selected.Name, output);
    }

    private static object Summary(EngineDefinition engine) => new
    {
        name = engine.Name,
        summary = engine.Description.Summary,
        state = engine.Description.State,
        supportsKinematics = engine.SupportsKinematics,
        defaultAgentCount = engine.DefaultAgentCount,
        interactiveRenderer = engine.Description.InteractiveRenderer,
        documentation = engine.Description.Documentation
    };
}
