using System.IO;
using System.Text.Json.Serialization;
using SwarmSim.Core.Configuration;
using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Canonical;

public sealed class CanonicalWorldSettings
{
    /// <summary>
    /// Built-in composition used by the two-argument world constructor and the CLI. Omitted JSON
    /// means Enhanced, including old complete manifests. Enhanced is omitted when serializing to
    /// preserve that contract. An explicitly injected pipeline takes precedence over this factory choice.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public CanonicalMode Mode { get; init; } = CanonicalMode.Enhanced;

    public int InitialCapacity { get; init; } = 1024;
    public float TargetSpeed { get; init; } = 1f;
    /// <summary>
    /// In Enhanced mode, the per-tick ceiling on the TOTAL composed steering magnitude, in units of
    /// speed per second. It is a single shared budget, not a per-contribution allowance: whisker
    /// avoidance, separation, alignment, cohesion and wander all draw from the same remainder in
    /// that order, and once it is spent later contributions receive nothing. Separation
    /// additionally withholds whatever it leaves unspent from alignment, cohesion and wander -
    /// that priority behaviour is unchanged; what it may no longer do is spend beyond this
    /// ceiling.
    /// In the explicitly selected ClassicReynolds mode this is instead a per-rule allowance:
    /// each of three contributions is clamped independently and summed without a shared
    /// post-clamp. Its total can exceed MaxForce. Injected pipelines use their chosen resolver.
    /// <para>
    /// The enhanced total bound holds to within float rounding, not exactly. Contributions are summed as
    /// normalized vectors, so the composed magnitude can exceed this value by a few ULP - measured
    /// at 2 ULP (2.5000005 against a 2.5 ceiling) on roughly a quarter of agent-ticks in a dense
    /// crowd. Assert against a small relative tolerance rather than a bare
    /// <c>&lt;= MaxForce</c> comparison, which reports false violations.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Enforced since issue #19. Before that fix separation clamped to a fresh MaxForce and added
    /// on top of an already-spent whisker budget, so one tick could compose up to 2x this value.
    /// <see cref="RuleInstrumentation.SteeringMagnitudesSquared"/> exposes the composed magnitude
    /// so the selected resolver's invariant is checkable from outside. Composition order belongs
    /// to the explicitly chosen pipeline.
    /// </remarks>
    public float MaxForce { get; init; } = 0.2f;
    public float SenseRadius { get; init; } = 10f;
    public float FieldOfView { get; init; } = 270f;
    public int MaxNeighbors { get; init; } = 32;
    public float SeparationRadius { get; init; } = 5f;
    public float SeparationWeight { get; init; } = 1.5f;
    public float AlignmentWeight { get; init; } = 1f;
    public float CohesionWeight { get; init; } = 1f;
    public float SeparationPriorityRadiusFactor { get; init; } = 0.20f;
    public float SeparationPriorityExitFactor { get; init; } = 0.45f;
    public float SeparationPriorityBoost { get; init; } = 2.5f;
    public float SeparationPriorityHoldTime { get; init; } = 0.08f;
    public float SeparationPriorityRampInTime { get; init; } = 0.08f;
    public float SeparationPriorityRampOutTime { get; init; } = 0.1f;
    public float SeparationSpeedDroop { get; init; } = 0.03f;
    public float MaxTurnRateDegPerSecond { get; init; } = 360f;
    public float WanderStrength { get; init; } = 0f;
    public float WanderRate { get; init; } = 1.5f;
    public float WhiskerTimeHorizon { get; init; } = 0.4f;
    public float WhiskerWeight { get; init; } = 1.2f;
    /// <summary>
    /// External deterministic seed. Supported values are 0 through
    /// <see cref="SwarmSim.Core.Utils.Rng.MaxSupportedSeed"/>, inclusive.
    /// </summary>
    public uint Seed { get; init; } = 123456u;
    public float WorldWidth { get; init; } = 1920f;
    public float WorldHeight { get; init; } = 1080f;
    public float FixedDeltaTime { get; init; } = 1f / 60f;

    public CanonicalWorldSettings() { }

    /// <summary>
    /// Supported classic baseline: full-circle observation and three independent-clamp/sum rules.
    /// Enhancement knobs retain their serialized values but their stages are absent in this mode.
    /// </summary>
    public static CanonicalWorldSettings ClassicReynolds() => new()
    {
        Mode = CanonicalMode.ClassicReynolds,
        FieldOfView = 360f
    };

    /// <summary>
    /// Maps a complete SimConfig using the renderer's established numeric clamps. Shared names
    /// do not imply equal dynamics: MaxSpeed is a legacy ceiling and TargetSpeed is canonical's
    /// fixed speed. Legacy-only fields have no canonical counterpart and are intentionally omitted.
    /// </summary>
    public static CanonicalWorldSettings FromSimConfig(SimConfig config, int? initialCapacity = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new CanonicalWorldSettings
        {
            InitialCapacity = initialCapacity ?? config.InitialCapacity,
            TargetSpeed = Math.Max(0.01f, config.MaxSpeed),
            MaxForce = Math.Max(0.01f, config.MaxForce),
            SenseRadius = Math.Max(1f, config.SenseRadius),
            SeparationRadius = Math.Max(1f, config.SeparationRadius),
            SeparationWeight = Math.Max(0f, config.SeparationWeight),
            AlignmentWeight = Math.Max(0f, config.AlignmentWeight),
            CohesionWeight = Math.Max(0f, config.CohesionWeight),
            FieldOfView = config.FieldOfView,
            WorldWidth = config.WorldWidth,
            WorldHeight = config.WorldHeight,
            FixedDeltaTime = config.FixedDeltaTime,
            MaxNeighbors = config.MaxNeighbors,
            WanderStrength = config.WanderStrength,
            WanderRate = config.WanderRate,
            MaxTurnRateDegPerSecond = config.MaxTurnRateDegPerSecond,
            WhiskerTimeHorizon = config.WhiskerTimeHorizon,
            WhiskerWeight = config.WhiskerWeight,
            SeparationPriorityRadiusFactor = config.SeparationPriorityRadiusFactor,
            SeparationPriorityExitFactor = config.SeparationPriorityExitFactor,
            SeparationPriorityBoost = config.SeparationPriorityBoost,
            SeparationPriorityHoldTime = config.SeparationPriorityHoldTime,
            SeparationPriorityRampInTime = config.SeparationPriorityRampInTime,
            SeparationPriorityRampOutTime = config.SeparationPriorityRampOutTime,
            SeparationSpeedDroop = config.SeparationSpeedDroop,
            Seed = config.Seed
        };
    }

    /// <summary>
    /// Validates serializable inputs while preserving the engine's minimum-capacity,
    /// minimum-neighbor and finite control-clamping behavior.
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();
        if (!Enum.IsDefined(Mode)) errors.Add("Unknown canonical Mode");
        if (Mode == CanonicalMode.ClassicReynolds && FieldOfView != 360f)
            errors.Add("ClassicReynolds mode requires FieldOfView 360");
        if (Seed > Rng.MaxSupportedSeed) errors.Add($"Seed must be between 0 and {Rng.MaxSupportedSeed}, inclusive");
        if (!float.IsFinite(TargetSpeed) || TargetSpeed <= 0f) errors.Add("TargetSpeed must be finite and positive");
        if (!float.IsFinite(MaxForce) || MaxForce <= 0f) errors.Add("MaxForce must be finite and positive");
        if (!float.IsFinite(SenseRadius) || SenseRadius <= 0f) errors.Add("SenseRadius must be finite and positive");
        if (!float.IsFinite(FieldOfView) || FieldOfView <= 0f || FieldOfView > 360f) errors.Add("FieldOfView must be in (0, 360]");
        if (!float.IsFinite(WorldWidth) || WorldWidth <= 0f) errors.Add("WorldWidth must be finite and positive");
        if (!float.IsFinite(WorldHeight) || WorldHeight <= 0f) errors.Add("WorldHeight must be finite and positive");
        if (!float.IsFinite(FixedDeltaTime) || FixedDeltaTime <= 0f) errors.Add("FixedDeltaTime must be finite and positive");
        if (!float.IsFinite(SeparationRadius)) errors.Add("SeparationRadius must be finite");
        if (!float.IsFinite(SeparationWeight)) errors.Add("SeparationWeight must be finite");
        if (!float.IsFinite(AlignmentWeight)) errors.Add("AlignmentWeight must be finite");
        if (!float.IsFinite(CohesionWeight)) errors.Add("CohesionWeight must be finite");
        if (!float.IsFinite(MaxTurnRateDegPerSecond) || MaxTurnRateDegPerSecond < 0f) errors.Add("MaxTurnRateDegPerSecond must be finite and >= 0");
        if (!float.IsFinite(WanderStrength) || WanderStrength < 0f) errors.Add("WanderStrength must be finite and >= 0");
        if (!float.IsFinite(WanderRate) || WanderRate < 0f) errors.Add("WanderRate must be finite and >= 0");
        if (!float.IsFinite(WhiskerTimeHorizon)) errors.Add("WhiskerTimeHorizon must be finite");
        if (!float.IsFinite(WhiskerWeight) || WhiskerWeight < 0f) errors.Add("WhiskerWeight must be finite and >= 0");
        if (!float.IsFinite(SeparationPriorityRadiusFactor)) errors.Add("SeparationPriorityRadiusFactor must be finite");
        if (!float.IsFinite(SeparationPriorityExitFactor)) errors.Add("SeparationPriorityExitFactor must be finite");
        if (!float.IsFinite(SeparationPriorityBoost) || SeparationPriorityBoost < 0f) errors.Add("SeparationPriorityBoost must be finite and >= 0");
        if (!float.IsFinite(SeparationSpeedDroop) || SeparationSpeedDroop < 0f || SeparationSpeedDroop > 1f) errors.Add("SeparationSpeedDroop must be in [0, 1]");
        if (!float.IsFinite(SeparationPriorityHoldTime)) errors.Add("SeparationPriorityHoldTime must be finite");
        if (!float.IsFinite(SeparationPriorityRampInTime)) errors.Add("SeparationPriorityRampInTime must be finite");
        if (!float.IsFinite(SeparationPriorityRampOutTime)) errors.Add("SeparationPriorityRampOutTime must be finite");
        return errors;
    }

    /// <summary>Loads a partial JSON file over canonical defaults, then validates the result.</summary>
    public static CanonicalWorldSettings LoadFromJson(string filePath) => FromJson(ConfigurationJson.ReadFile(filePath));

    /// <summary>Reads a JSON object; omitted fields retain their canonical defaults.</summary>
    public static CanonicalWorldSettings FromJson(string json) => ConfigurationJson.Deserialize<CanonicalWorldSettings>(json, config => config.Validate());

    /// <summary>Serializes resolved settings after validation; the compatibility default Mode is omitted.</summary>
    public string ToJson() => ConfigurationJson.Serialize(this, Validate());

    /// <summary>Writes a complete resolved configuration after validation succeeds.</summary>
    public void SaveToJson(string filePath) => File.WriteAllText(filePath, ToJson());
}
