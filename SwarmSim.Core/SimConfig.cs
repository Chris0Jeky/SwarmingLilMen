using System.IO;
using System.Text.Json;

namespace SwarmSim.Core;

/// <summary>
/// Complete simulation configuration (JSON-serializable).
/// Immutable after construction - create new instance to change settings.
/// </summary>
public sealed class SimConfig
{
    // ===== World Parameters =====
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
    public float GridCellSize { get; init; } = 50f; // Should be ≈ SenseRadius

    // ===== Boids Parameters =====
    public float SenseRadius { get; init; } = 50f;
    public float SeparationRadius { get; init; } = 20f;
    public float FieldOfView { get; init; } = 270f; // Degrees, 360 = omnidirectional

    public float SeparationWeight { get; init; } = 1.5f;
    public float AlignmentWeight { get; init; } = 1.0f;
    public float CohesionWeight { get; init; } = 1.0f;
    public float WanderStrength { get; init; } = 0f; // Random exploration force (0 = disabled)

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
    public float[,] AggressionMatrix { get; init; } = DefaultAggressionMatrix();

    // ===== Population Limits =====
    public int InitialCapacity { get; init; } = 100_000;
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

        if (WorldWidth <= 0) errors.Add("WorldWidth must be positive");
        if (WorldHeight <= 0) errors.Add("WorldHeight must be positive");
        if (FixedDeltaTime <= 0) errors.Add("FixedDeltaTime must be positive");
        if (MaxSpeed <= 0) errors.Add("MaxSpeed must be positive");
        if (MaxForce <= 0) errors.Add("MaxForce must be positive");
        if (GridCellSize <= 0) errors.Add("GridCellSize must be positive");
        if (SenseRadius <= 0) errors.Add("SenseRadius must be positive");
        if (FieldOfView <= 0 || FieldOfView > 360) errors.Add("FieldOfView must be in (0, 360]");
        if (InitialCapacity <= 0) errors.Add("InitialCapacity must be positive");
        if (MaxCapacity < InitialCapacity) errors.Add("MaxCapacity must be >= InitialCapacity");

        if (Friction < 0 || Friction > 1) errors.Add("Friction must be in [0, 1]");
        if (MutationRate < 0 || MutationRate > 1) errors.Add("MutationRate must be in [0, 1]");

        int groups = AggressionMatrix.GetLength(0);
        if (AggressionMatrix.GetLength(1) != groups)
            errors.Add("AggressionMatrix must be square");

        return errors;
    }

    public static SimConfig LoadFromJson(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Configuration file not found.", filePath);

        string json = File.ReadAllText(filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        var config = JsonSerializer.Deserialize<SimConfig>(json, options);
        if (config is null)
            throw new InvalidOperationException($"Failed to deserialize config file '{filePath}'.");

        return config;
    }
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
    /// Constant-speed model: friction = 1.0, agents always move at or near maxSpeed.
    /// Steering only changes direction, not speed magnitude.
    /// Simple, canonical boids behavior (like Craig Reynolds' original).
    /// </summary>
    ConstantSpeed,

    /// <summary>
    /// Damped model: friction &lt; 1.0, agents have variable speeds based on forces.
    /// Equilibrium speed depends on force magnitude and friction.
    /// More realistic physics, but requires careful tuning.
    /// </summary>
    Damped
}
