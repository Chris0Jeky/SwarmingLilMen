using System.IO;
using System.Text.Json.Serialization;
using SwarmSim.Core.Configuration;
using SwarmSim.Core.Spatial;
using SwarmSim.Core.Utils;

namespace SwarmSim.Core;

/// <summary>
/// Complete simulation configuration (JSON-serializable).
/// Immutable after construction - create new instance to change settings.
/// </summary>
public sealed class SimConfig
{
    // ===== World Parameters =====
    private uint _seed = 42u;

    /// <summary>
    /// Seed shared by deterministic construction paths. Supported values are 0 through
    /// <see cref="Rng.MaxSupportedSeed"/>, inclusive.
    /// </summary>
    public uint Seed
    {
        get => _seed;
        init => _seed = value;
    }

    public float WorldWidth { get; init; } = 1920f;
    public float WorldHeight { get; init; } = 1080f;
    public BoundaryMode BoundaryMode { get; init; } = BoundaryMode.Wrap;

    // ===== Time & Physics =====
    public float FixedDeltaTime { get; init; } = 1f / 120f; // 120 Hz simulation
    public float MaxSpeed { get; init; } = 200f;
    public float MaxForce { get; init; } = 5f; // Maximum steering force magnitude
    public float Friction { get; init; } = 1f; // Velocity decay per tick (1.0 = no friction)
    public SpeedModel SpeedModel { get; init; } = SpeedModel.ConstantSpeed; // Constant-speed vs damped

    // ===== Spatial Grid =====
    // Legacy grid partition size; neighbor queries cover SenseRadius even across multiple cells.
    public float GridCellSize { get; init; } = 50f;

    // ===== Boids Parameters =====
    public float SenseRadius { get; init; } = 50f;
    public float SeparationRadius { get; init; } = 20f;
    public float FieldOfView { get; init; } = 270f; // Degrees, 360 = omnidirectional

    public float SeparationWeight { get; init; } = 1.5f;
    public float AlignmentWeight { get; init; } = 1.0f;
    public float CohesionWeight { get; init; } = 1.0f;
    public float WanderStrength { get; init; } = 0f; // Random exploration force (0 = disabled)
    /// <summary>Maximum canonical wander-angle change, in radians per second.</summary>
    public float WanderRate { get; init; } = 1.5f;

    /// <summary>Maximum canonical heading change, in degrees per second.</summary>
    public float MaxTurnRateDegPerSecond { get; init; } = 360f;

    /// <summary>Canonical predictive-collision lookahead, in seconds.</summary>
    public float WhiskerTimeHorizon { get; init; } = 0.4f;

    /// <summary>Canonical predictive-collision steering weight.</summary>
    public float WhiskerWeight { get; init; } = 1.2f;

    /// <summary>Canonical separation-priority entry radius as a fraction of SenseRadius.</summary>
    public float SeparationPriorityRadiusFactor { get; init; } = 0.20f;

    /// <summary>Canonical separation-priority exit radius as a fraction of SenseRadius.</summary>
    public float SeparationPriorityExitFactor { get; init; } = 0.45f;

    /// <summary>Canonical steering multiplier while separation priority is active.</summary>
    public float SeparationPriorityBoost { get; init; } = 2.5f;

    /// <summary>Minimum canonical separation-priority hold time, in seconds.</summary>
    public float SeparationPriorityHoldTime { get; init; } = 0.08f;

    /// <summary>Canonical separation-priority ramp-in time, in seconds.</summary>
    public float SeparationPriorityRampInTime { get; init; } = 0.08f;

    /// <summary>Canonical separation-priority ramp-out time, in seconds.</summary>
    public float SeparationPriorityRampOutTime { get; init; } = 0.1f;

    /// <summary>Canonical target-speed reduction while separation priority is active.</summary>
    public float SeparationSpeedDroop { get; init; } = 0.03f;

    public int SeparationCrowdingThreshold { get; init; } = 12; // Neighbor count before boosting separation
    public float SeparationCrowdingBoost { get; init; } = 2.5f; // Multiplier for separation weight when very crowded
    public int MaxNeighbors { get; init; } = 16; // cap neighbors contributing to steering
    public float CollisionAvoidanceRadius { get; init; } = 12f;
    public float CollisionAvoidanceBoost { get; init; } = 4f;

    // ===== Combat =====
    public float AttackRadius { get; init; } = 15f;
    public float AttackDamage { get; init; } = 10f;
    public float AttackCooldown { get; init; } = 0.5f; // seconds

    // ===== Energy & Metabolism =====
    public float InitialEnergy { get; init; } = 100f;
    public float MaxEnergy { get; init; } = 200f;
    public float BaseDrain { get; init; } = 0.5f; // Energy per second
    public float MoveCost { get; init; } = 0.01f; // Energy per unit of speed
    public float DeathEnergyThreshold { get; init; } = 0f;

    // ===== Health =====
    public float InitialHealth { get; init; } = 100f;
    public float MaxHealth { get; init; } = 100f;
    public float HealthRegenRate { get; init; } = 0.5f; // Per second when not in combat

    // ===== Reproduction =====
    public float ReproductionEnergyThreshold { get; init; } = 150f;
    public float ReproductionEnergyCost { get; init; } = 50f; // Parent loses this
    public float ChildEnergyStart { get; init; } = 50f; // Child starts with this
    public float MutationRate { get; init; } = 0.1f; // Per-trait probability
    public float MutationStdDev { get; init; } = 0.2f; // Standard deviation of mutations

    // ===== Foraging =====
    public float FoodEnergyGain { get; init; } = 20f;
    public float ForageRadius { get; init; } = 10f;

    // ===== Group Aggression Matrix =====
    /// <summary>
    /// Aggression[groupA, groupB] defines how groupA treats groupB.
    /// Values: -1 (fear/flee), 0 (neutral), +1 (hunt/attack).
    /// Default: 4 groups, neutral to self, hostile to others.
    /// </summary>
    [JsonConverter(typeof(AggressionMatrixJsonConverter))]
    public float[,] AggressionMatrix { get; init; } = DefaultAggressionMatrix();

    // ===== Population Limits =====
    /// <summary>Initial legacy storage allocation; spawn calls may grow it up to MaxCapacity.</summary>
    public int InitialCapacity { get; init; } = 100_000;
    /// <summary>Legacy occupied-slot ceiling, including dead slots until compaction.</summary>
    public int MaxCapacity { get; init; } = 200_000;
    public int CompactionInterval { get; init; } = 600; // Ticks between compactions

    /// <summary>
    /// Creates a default 4x4 aggression matrix:
    /// Groups are neutral to themselves, hostile to others.
    /// </summary>
    private static float[,] DefaultAggressionMatrix()
    {
        var matrix = new float[4, 4];
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                matrix[i, j] = i == j ? 0f : 0.5f; // Neutral to self, mildly hostile to others
            }
        }
        return matrix;
    }

    /// <summary>
    /// Creates the shared balanced tuning scenario, also available as configs/balanced.json.
    /// </summary>
    public static SimConfig Balanced() => ConfigurationResolver.ResolveSimConfig("balanced");

    /// <summary>
    /// Creates a configuration preset for peaceful flocking behavior.
    /// </summary>
    public static SimConfig PeacefulFlocks() => new()
    {
        MaxSpeed = 10f,
        MaxForce = 2.0f,
        SpeedModel = SpeedModel.ConstantSpeed,
        Friction = 1.0f,
        SeparationWeight = 2.0f,
        AlignmentWeight = 1.5f,
        CohesionWeight = 1.0f,
        FieldOfView = 270f,
        AttackDamage = 0f, // No combat
        BaseDrain = 0.1f, // Low energy drain
        AggressionMatrix = NeutralAggressionMatrix(4)
    };

    /// <summary>
    /// Creates a configuration preset for warband behavior (groups fight).
    /// </summary>
    public static SimConfig Warbands() => new()
    {
        SeparationWeight = 1.0f,
        AlignmentWeight = 1.0f,
        CohesionWeight = 1.5f,
        AttackDamage = 20f,
        AttackRadius = 20f,
        AggressionMatrix = HostileAggressionMatrix(4)
    };

    /// <summary>
    /// Creates a configuration preset for rapid evolution.
    /// </summary>
    public static SimConfig RapidEvolution() => new()
    {
        ReproductionEnergyThreshold = 100f, // Lower threshold
        ReproductionEnergyCost = 30f, // Cheaper reproduction
        MutationRate = 0.3f, // High mutation
        MutationStdDev = 0.3f, // Larger mutations
        BaseDrain = 0.3f
    };

    /// <summary>
    /// Creates an aggression matrix where all groups are neutral.
    /// </summary>
    public static float[,] NeutralAggressionMatrix(int groupCount)
    {
        var matrix = new float[groupCount, groupCount];
        // All zeros (neutral)
        return matrix;
    }

    /// <summary>
    /// Creates an aggression matrix where different groups are hostile.
    /// </summary>
    private static float[,] HostileAggressionMatrix(int groupCount)
    {
        var matrix = new float[groupCount, groupCount];
        for (int i = 0; i < groupCount; i++)
        {
            for (int j = 0; j < groupCount; j++)
            {
                matrix[i, j] = i == j ? 0f : 1.0f; // Neutral to self, hostile to others
            }
        }
        return matrix;
    }

    /// <summary>
    /// Validates configuration values and returns error messages if invalid.
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (Seed > Rng.MaxSupportedSeed)
            errors.Add($"Seed must be between 0 and {Rng.MaxSupportedSeed}, inclusive");
        if (!Enum.IsDefined(BoundaryMode)) errors.Add("BoundaryMode must be a defined boundary mode");
        if (!Enum.IsDefined(SpeedModel)) errors.Add("SpeedModel must be a defined speed model");
        if (!float.IsFinite(WorldWidth) || WorldWidth <= 0) errors.Add("WorldWidth must be positive");
        if (!float.IsFinite(WorldHeight) || WorldHeight <= 0) errors.Add("WorldHeight must be positive");
        if (!float.IsFinite(FixedDeltaTime) || FixedDeltaTime <= 0) errors.Add("FixedDeltaTime must be positive");
        if (!float.IsFinite(MaxSpeed) || MaxSpeed <= 0) errors.Add("MaxSpeed must be positive");
        if (!float.IsFinite(MaxForce) || MaxForce <= 0) errors.Add("MaxForce must be positive");
        if (!float.IsFinite(GridCellSize) || GridCellSize <= 0) errors.Add("GridCellSize must be positive");
        if (!float.IsFinite(SenseRadius) || SenseRadius <= 0) errors.Add("SenseRadius must be positive");
        if (!float.IsFinite(SeparationRadius) || SeparationRadius < 0) errors.Add("SeparationRadius must be finite and >= 0");
        if (!float.IsFinite(FieldOfView) || FieldOfView <= 0 || FieldOfView > 360) errors.Add("FieldOfView must be in (0, 360]");
        if (float.IsFinite(WorldWidth) && WorldWidth > 0 && float.IsFinite(WorldHeight) && WorldHeight > 0 &&
            float.IsFinite(GridCellSize) && GridCellSize > 0 &&
            !UniformGrid.TryGetDimensions(GridCellSize, WorldWidth, WorldHeight, out _, out _))
            errors.Add($"GridCellSize {GridCellSize} over a {WorldWidth} x {WorldHeight} world needs more than {UniformGrid.MaxCells} spatial grid cells; increase GridCellSize or reduce WorldWidth/WorldHeight");
        if (InitialCapacity <= 0) errors.Add("InitialCapacity must be positive");
        if (InitialCapacity > Array.MaxLength) errors.Add($"InitialCapacity must be <= the managed array limit ({Array.MaxLength})");
        if (MaxCapacity < InitialCapacity) errors.Add("MaxCapacity must be >= InitialCapacity");
        if (MaxCapacity > Array.MaxLength) errors.Add($"MaxCapacity must be <= the managed array limit ({Array.MaxLength})");
        if (CompactionInterval < 1) errors.Add("CompactionInterval must be >= 1");
        if (SeparationCrowdingThreshold < 0) errors.Add("SeparationCrowdingThreshold must be >= 0");

        // Spawn copies these values into live state. Their eventual gameplay ranges remain open.
        if (!float.IsFinite(InitialEnergy)) errors.Add("InitialEnergy must be finite");
        if (!float.IsFinite(InitialHealth)) errors.Add("InitialHealth must be finite");

        if (!float.IsFinite(Friction) || Friction < 0 || Friction > 1) errors.Add("Friction must be in [0, 1]");
        if (!float.IsFinite(MutationRate) || MutationRate < 0 || MutationRate > 1) errors.Add("MutationRate must be in [0, 1]");
        if (!float.IsFinite(WanderStrength) || WanderStrength < 0) errors.Add("WanderStrength must be finite and >= 0");

        // These are consumed by the canonical bridge. Finite negative timing/radius factors
        // retain canonical's established clamps; boosts/rates and speed droop have real bounds.
        if (!float.IsFinite(WanderRate) || WanderRate < 0f) errors.Add("WanderRate must be finite and >= 0");
        if (!float.IsFinite(MaxTurnRateDegPerSecond) || MaxTurnRateDegPerSecond < 0f) errors.Add("MaxTurnRateDegPerSecond must be finite and >= 0");
        if (!float.IsFinite(WhiskerTimeHorizon)) errors.Add("WhiskerTimeHorizon must be finite");
        if (!float.IsFinite(WhiskerWeight) || WhiskerWeight < 0f) errors.Add("WhiskerWeight must be finite and >= 0");
        if (!float.IsFinite(SeparationPriorityRadiusFactor)) errors.Add("SeparationPriorityRadiusFactor must be finite");
        if (!float.IsFinite(SeparationPriorityExitFactor)) errors.Add("SeparationPriorityExitFactor must be finite");
        if (!float.IsFinite(SeparationPriorityBoost) || SeparationPriorityBoost < 0f) errors.Add("SeparationPriorityBoost must be finite and >= 0");
        if (!float.IsFinite(SeparationPriorityHoldTime)) errors.Add("SeparationPriorityHoldTime must be finite");
        if (!float.IsFinite(SeparationPriorityRampInTime)) errors.Add("SeparationPriorityRampInTime must be finite");
        if (!float.IsFinite(SeparationPriorityRampOutTime)) errors.Add("SeparationPriorityRampOutTime must be finite");
        if (!float.IsFinite(SeparationSpeedDroop) || SeparationSpeedDroop < 0f || SeparationSpeedDroop > 1f) errors.Add("SeparationSpeedDroop must be in [0, 1]");

        // Legacy steering inputs: negatives stay legal (existing behaviour), NaN/Infinity would poison forces.
        if (!float.IsFinite(SeparationWeight)) errors.Add("SeparationWeight must be finite");
        if (!float.IsFinite(AlignmentWeight)) errors.Add("AlignmentWeight must be finite");
        if (!float.IsFinite(CohesionWeight)) errors.Add("CohesionWeight must be finite");
        if (!float.IsFinite(CollisionAvoidanceRadius)) errors.Add("CollisionAvoidanceRadius must be finite");
        if (!float.IsFinite(CollisionAvoidanceBoost)) errors.Add("CollisionAvoidanceBoost must be finite");
        if (!float.IsFinite(SeparationCrowdingBoost)) errors.Add("SeparationCrowdingBoost must be finite");

        if (AggressionMatrix is null)
        {
            errors.Add("AggressionMatrix must not be null");
            return errors;
        }

        int groups = AggressionMatrix.GetLength(0);
        if (AggressionMatrix.GetLength(1) != groups)
            errors.Add("AggressionMatrix must be square");

        return errors;
    }

    internal SimConfig WithSeed(uint seed)
    {
        var copy = (SimConfig)MemberwiseClone();
        copy._seed = seed;
        return copy;
    }

    /// <summary>Loads a partial JSON file over the class defaults, then validates the result.</summary>
    public static SimConfig LoadFromJson(string filePath) => FromJson(ConfigurationJson.ReadFile(filePath));

    /// <summary>Reads a JSON object; omitted fields retain their class defaults.</summary>
    public static SimConfig FromJson(string json) => ConfigurationJson.Deserialize<SimConfig>(json, config => config.Validate());

    /// <summary>Serializes every property, including aggression rows, after validation.</summary>
    public string ToJson() => ConfigurationJson.Serialize(this, Validate());

    /// <summary>Writes a complete resolved configuration after validation succeeds.</summary>
    public void SaveToJson(string filePath) => File.WriteAllText(filePath, ToJson());
}

/// <summary>
/// Boundary behavior mode for the world edges.
/// </summary>
public enum BoundaryMode
{
    /// <summary>Wrap around to opposite side (toroidal)</summary>
    Wrap,

    /// <summary>Reflect off boundaries (bounce)</summary>
    Reflect,

    /// <summary>Clamp to boundaries (stop at edge)</summary>
    Clamp
}

/// <summary>
/// Speed model for agent movement.
/// </summary>
public enum SpeedModel
{
    /// <summary>
    /// Undamped model: the integrator skips the friction step entirely, so
    /// <see cref="SimConfig.Friction"/> is ignored in this mode rather than coerced to 1.0 -
    /// configuring it has no effect on trajectories.
    /// Steering is added straight into velocity, so it changes speed as well as direction.
    /// The only speed constraint is the upper <see cref="SimConfig.MaxSpeed"/> clamp: agents
    /// slower than MaxSpeed are never renormalized back up to it, so despite the name this
    /// mode does not hold speed constant.
    /// </summary>
    ConstantSpeed,

    /// <summary>
    /// Damped model: friction &lt; 1.0, agents have variable speeds based on forces.
    /// Equilibrium speed depends on force magnitude and friction.
    /// More realistic physics, but requires careful tuning.
    /// </summary>
    Damped
}
