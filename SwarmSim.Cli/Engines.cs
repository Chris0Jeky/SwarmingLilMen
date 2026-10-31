using System.Text.Json;
using SwarmSim.Core;
using SwarmSim.Core.Aco;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Configuration;
using SwarmSim.Core.Diagnostics;
using SwarmSim.Core.Metrics;
using SwarmSim.Core.Utils;
using SwarmSim.Core.Vicsek;

namespace SwarmSim.Cli;

/// <summary>One initialized engine as the headless loop sees it: no renderer, no wall clock.</summary>
internal interface IHeadlessSimulation
{
    int MetricCapacity { get; }
    float WorldWidth { get; }
    float WorldHeight { get; }
    MetricDomain Domain { get; }
    void Step();
    /// <summary>Copies every live agent; a destination too small throws, state is never truncated.</summary>
    int Capture(Span<KinematicSample> destination);
    string KinematicHash();
}

/// <summary>
/// Everything the CLI spine knows about one engine. Settings stay typed inside a definition and
/// pass through the spine as an opaque object; no other file branches on an engine name.
/// </summary>
internal sealed class EngineDefinition
{
    private EngineDefinition() { }

    internal required string Name { get; init; }
    internal required string SpawnModel { get; init; }
    /// <summary>Schema 1 predates engines added later; their manifests start at schema 2.</summary>
    internal required int MinimumManifestSchema { get; init; }
    /// <summary>Named scenarios are boids recipes; other engines take --config only.</summary>
    internal required bool AcceptsScenario { get; init; }
    /// <summary>A field older manifests may omit, and what that omission has always meant.</summary>
    internal (string Name, Func<JsonElement> Meaning)? VersionedOptionalField { get; private init; }
    /// <summary>Scenario, file, overrides, then defaults; returns validated native settings.</summary>
    internal required Func<string?, string?, IReadOnlyDictionary<string, JsonElement>?, object?, object> Resolve { get; init; }
    internal required Func<string> DefaultSettingsJson { get; init; }
    internal required Func<object, string> SettingsJson { get; init; }
    internal required Func<object, uint> Seed { get; init; }
    internal required Func<object, float> FixedDeltaTime { get; init; }
    internal required Func<RunRequest, IHeadlessSimulation> Create { get; init; }
    /// <summary>
    /// Probes beyond the shared kinematic set. The simulation is null for metadata-only uses
    /// (discovery and selection checks), where a probe is never sampled.
    /// </summary>
    internal required Func<IHeadlessSimulation?, IMetricProbe[]> ExtraProbes { get; init; }

    internal static readonly EngineDefinition Legacy = Native<SimConfig>(
        "legacy", "uniform-single-group-v1", minimumManifestSchema: 1, acceptsScenario: true,
        (scenario, file, overrides, defaults) => ConfigurationResolver.ResolveSimConfig(scenario, file, overrides, defaults),
        settings => settings.ToJson(), settings => settings.Seed, settings => settings.FixedDeltaTime,
        request => new LegacySimulation(request), _ => []);

    internal static readonly EngineDefinition Canonical = Native<CanonicalWorldSettings>(
        "canonical", "uniform-single-group-v1", minimumManifestSchema: 1, acceptsScenario: true,
        (scenario, file, overrides, defaults) => ConfigurationResolver.ResolveCanonicalSettings(scenario, file, overrides, defaults),
        settings => settings.ToJson(), settings => settings.Seed, settings => settings.FixedDeltaTime,
        request => new CanonicalSimulation(request),
        simulation => [simulation is CanonicalSimulation canonical
            ? new CompletedStepBoidsMetricProbe(canonical.World)
            : new BoidsRuleMetricProbe(new RuleInstrumentation(1))],
        // Pre-composition manifests always mean Enhanced. Pin that meaning explicitly rather than
        // inheriting a future settings default.
        (nameof(CanonicalWorldSettings.Mode), static () => JsonSerializer.SerializeToElement(CanonicalMode.Enhanced)));

    internal static readonly EngineDefinition Vicsek = Native<VicsekSettings>(
        "vicsek", "vicsek-uniform-v1", minimumManifestSchema: 2, acceptsScenario: false,
        (_, file, overrides, defaults) => ConfigurationResolver.ResolveVicsekSettings(file, overrides, defaults),
        settings => settings.ToJson(), settings => settings.Seed, settings => settings.FixedDeltaTime,
        request => request.AgentCount <= VicsekWorld.MaxAgents ? new VicsekSimulation(request)
            : throw new ArgumentException($"--agents exceeds the Vicsek maximum of {VicsekWorld.MaxAgents}."), _ => []);

    internal static readonly EngineDefinition Aco = Native<AntColonySettings>(
        "aco", "aco-nest-v1", minimumManifestSchema: 2, acceptsScenario: false,
        (_, file, overrides, defaults) => ConfigurationResolver.ResolveAntColonySettings(file, overrides, defaults),
        settings => settings.ToJson(), settings => settings.Seed, settings => settings.FixedDeltaTime,
        request => new AcoSimulation(request),
        simulation => [new AntColonyMetricProbe((simulation as AcoSimulation)?.World)]);

    private static EngineDefinition Native<TSettings>(string name, string spawnModel, int minimumManifestSchema, bool acceptsScenario,
        Func<string?, string?, IReadOnlyDictionary<string, JsonElement>?, TSettings?, TSettings> resolve,
        Func<TSettings, string> json, Func<TSettings, uint> seed, Func<TSettings, float> fixedDeltaTime,
        Func<RunRequest, IHeadlessSimulation> create, Func<IHeadlessSimulation?, IMetricProbe[]> extraProbes,
        (string Name, Func<JsonElement> Meaning)? versionedOptionalField = null)
        where TSettings : class, new() => new()
        {
            Name = name,
            SpawnModel = spawnModel,
            MinimumManifestSchema = minimumManifestSchema,
            AcceptsScenario = acceptsScenario,
            VersionedOptionalField = versionedOptionalField,
            Resolve = (scenario, file, overrides, defaults) => resolve(scenario, file, overrides, (TSettings?)defaults),
            DefaultSettingsJson = () => json(new TSettings()),
            SettingsJson = settings => json((TSettings)settings),
            Seed = settings => seed((TSettings)settings),
            FixedDeltaTime = settings => fixedDeltaTime((TSettings)settings),
            Create = create,
            ExtraProbes = extraProbes
        };
}

/// <summary>The closed list of engines. Adding one means one definition here, nothing else.</summary>
internal static class EngineRegistry
{
    private static readonly EngineDefinition[] Definitions =
        [EngineDefinition.Legacy, EngineDefinition.Canonical, EngineDefinition.Vicsek, EngineDefinition.Aco];

    /// <summary>For messages: "a, b or c".</summary>
    internal static string NameList => string.Join(", ", Definitions[..^1].Select(engine => engine.Name)) + " or " + Definitions[^1].Name;
    /// <summary>For usage text: "a|b|c".</summary>
    internal static string NameChoices => string.Join('|', Definitions.Select(engine => engine.Name));

    internal static EngineDefinition? Find(string? name) => Array.Find(Definitions, engine => engine.Name == name);

    internal static EngineDefinition Get(string name) =>
        Find(name) ?? throw new ArgumentException($"Engine must be {NameList}.", nameof(name));
}

/// <summary>The boids spawn contract, uniform-single-group-v1, on the legacy generator.</summary>
file static class BoidsSpawn
{
    internal static void Uniform(uint seed, int count, float width, float height, Action<float, float, float, float> add)
    {
        var random = new Rng(seed);
        for (int i = 0; i < count; i++)
        {
            float x = random.NextFloat(0f, width);
            float y = random.NextFloat(0f, height);
            float angle = random.NextFloat(0f, 2f * MathF.PI);
            add(x, y, MathF.Cos(angle), MathF.Sin(angle));
        }
    }
}

internal static class AgentCapture
{
    internal static void RequireRoom(int required, Span<KinematicSample> destination)
    {
        if (destination.Length < required)
            throw new ArgumentException("Capture buffer must hold every simulation slot; state is never truncated.", nameof(destination));
    }

    internal static int Copy(ReadOnlySpan<Boid> agents, Span<KinematicSample> destination)
    {
        RequireRoom(agents.Length, destination);
        int count = 0;
        foreach (Boid boid in agents)
            destination[count++] = new(boid.Position.X, boid.Position.Y, boid.Velocity.X, boid.Velocity.Y);
        return count;
    }
}

internal sealed class LegacySimulation : IHeadlessSimulation
{
    private readonly SimConfig _config;
    private readonly World _world;

    internal LegacySimulation(RunRequest request)
    {
        _config = (SimConfig)request.Settings;
        if (request.AgentCount > _config.MaxCapacity)
            throw new ArgumentException("--agents exceeds the legacy MaxCapacity.");
        _world = new World(_config);
        float speed = _config.MaxSpeed;
        BoidsSpawn.Uniform(request.Seed, request.AgentCount, _config.WorldWidth, _config.WorldHeight, (x, y, dx, dy) =>
        {
            int index = _world.AddAgent(x, y);
            if (index < 0)
                throw new InvalidOperationException("Legacy initialization rejected an agent.");
            _world.Vx[index] = dx * speed;
            _world.Vy[index] = dy * speed;
        });
    }

    public int MetricCapacity => _world.Capacity;
    public float WorldWidth => _config.WorldWidth;
    public float WorldHeight => _config.WorldHeight;
    public MetricDomain Domain => _config.BoundaryMode == BoundaryMode.Wrap
        ? MetricDomain.Toroidal(_config.WorldWidth, _config.WorldHeight) : MetricDomain.Euclidean;
    public void Step() => _world.Tick();
    public string KinematicHash() => SimulationKinematicHash.Compute(_world);

    public int Capture(Span<KinematicSample> destination)
    {
        AgentCapture.RequireRoom(_world.Count, destination);
        int count = 0;
        for (int i = 0; i < _world.Count; i++)
            if ((_world.State[i] & AgentState.Dead) == 0)
                destination[count++] = new(_world.X[i], _world.Y[i], _world.Vx[i], _world.Vy[i]);
        return count;
    }
}

internal sealed class CanonicalSimulation : IHeadlessSimulation
{
    private readonly CanonicalWorldSettings _settings;
    private readonly float _dt;

    internal CanonicalSimulation(RunRequest request)
    {
        _settings = (CanonicalWorldSettings)request.Settings;
        _dt = request.FixedDeltaTime;
        if (request.AgentCount > Math.Max(1, _settings.InitialCapacity))
            throw new ArgumentException("--agents exceeds the canonical InitialCapacity.");
        World = new CanonicalWorld(_settings, new GridSpatialIndex(_settings.SenseRadius, _settings.WorldWidth, _settings.WorldHeight));
        BoidsSpawn.Uniform(request.Seed, request.AgentCount, _settings.WorldWidth, _settings.WorldHeight, (x, y, dx, dy) =>
        {
            if (!World.TryAddBoid(new Vec2(x, y), new Vec2(dx, dy)))
                throw new InvalidOperationException("Canonical initialization rejected an agent.");
        });
    }

    internal CanonicalWorld World { get; }
    public int MetricCapacity => Math.Max(1, _settings.InitialCapacity);
    public float WorldWidth => _settings.WorldWidth;
    public float WorldHeight => _settings.WorldHeight;
    public MetricDomain Domain => MetricDomain.Toroidal(_settings.WorldWidth, _settings.WorldHeight);
    public void Step() => World.Step(_dt);
    public string KinematicHash() => SimulationKinematicHash.Compute(World);
    public int Capture(Span<KinematicSample> destination) => AgentCapture.Copy(World.Boids, destination);
}

internal sealed class VicsekSimulation(RunRequest request) : IHeadlessSimulation
{
    private readonly VicsekSettings _settings = (VicsekSettings)request.Settings;
    private readonly VicsekWorld _world = new((VicsekSettings)request.Settings, request.AgentCount);

    public int MetricCapacity => request.AgentCount;
    public float WorldWidth => _settings.WorldWidth;
    public float WorldHeight => _settings.WorldHeight;
    public MetricDomain Domain => MetricDomain.Toroidal(_settings.WorldWidth, _settings.WorldHeight);
    public void Step() => _world.Step();
    public string KinematicHash() => SimulationKinematicHash.Compute(_world);
    public int Capture(Span<KinematicSample> destination) => AgentCapture.Copy(_world.Agents, destination);
}

internal sealed class AcoSimulation(RunRequest request) : IHeadlessSimulation
{
    private readonly AntColonySettings _settings = (AntColonySettings)request.Settings;

    internal AntColonyWorld World { get; } = new((AntColonySettings)request.Settings, request.AgentCount);
    public int MetricCapacity => request.AgentCount;
    public float WorldWidth => _settings.WorldWidth;
    public float WorldHeight => _settings.WorldHeight;
    public MetricDomain Domain => MetricDomain.Toroidal(_settings.WorldWidth, _settings.WorldHeight);
    public void Step() => World.Step();
    public string KinematicHash() => SimulationKinematicHash.Compute(World);

    public int Capture(Span<KinematicSample> destination)
    {
        World.CopyKinematics(destination);
        return World.Ants.Length;
    }
}
