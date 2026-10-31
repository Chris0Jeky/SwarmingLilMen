using SwarmSim.Core.Configuration;
using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Aco;

/// <summary>
/// Parameters of the minimal ant colony. Deposit = 0 is the zero-pheromone control: identical
/// settings, identical code path. Defaults are the calibrated recipe's model parameters; see
/// docs/ACO.md. Lengths are world units and rates are per tick.
/// </summary>
public sealed class AntColonySettings
{
    /// <summary>Pheromone one returning ant adds to its cell per tick.</summary>
    public float Deposit { get; init; } = 1f;
    /// <summary>Fraction of every cell lost per field update.</summary>
    public float Evaporation { get; init; } = 0.01f;
    /// <summary>
    /// Ticks per field update. It is recorded in every manifest and fixed at 1.
    /// </summary>
    // ponytail: only every-tick is valid; another cadence needs a deposit buffer sized for it.
    public int FieldCadence { get; init; } = 1;
    public float Speed { get; init; } = 1f;
    /// <summary>Full width in radians of the uniform per-tick wander of a searching ant.</summary>
    public float TurnNoise { get; init; } = 0.3f;
    /// <summary>Angle in radians of the left and right sensors, and of the turn toward them.</summary>
    public float SensorAngle { get; init; } = 1f;
    public float SensorDistance { get; init; } = 12f;
    public int FieldColumns { get; init; } = 60;
    public int FieldRows { get; init; } = 60;
    public float NestX { get; init; } = 40f;
    public float NestY { get; init; } = 60f;
    public float NestRadius { get; init; } = 4f;
    public float FoodX { get; init; } = 80f;
    public float FoodY { get; init; } = 60f;
    public float FoodRadius { get; init; } = 6f;
    /// <summary>Units of food at the source; one ant carries one unit.</summary>
    public int FoodAmount { get; init; } = 1_000_000;
    public float WorldWidth { get; init; } = 120f;
    public float WorldHeight { get; init; } = 120f;
    public float FixedDeltaTime { get; init; } = 1f;
    /// <summary>Supported values are 0 through <see cref="Rng.MaxSupportedSeed"/>, inclusive.</summary>
    public uint Seed { get; init; } = 123456u;

    /// <summary>Largest supported pheromone grid, matching the spatial grid's cell limit.</summary>
    public const int MaxFieldCells = 1 << 22;

    public AntColonySettings() { }

    public List<string> Validate()
    {
        var errors = new List<string>();
        void Positive(float value, string name)
        {
            if (!float.IsFinite(value) || value <= 0f) errors.Add($"{name} must be finite and positive");
        }
        void NonNegative(float value, string name)
        {
            if (!float.IsFinite(value) || value < 0f) errors.Add($"{name} must be finite and non-negative");
        }
        void Inside(float value, float extent, string name)
        {
            if (!float.IsFinite(value) || value < 0f || value >= extent) errors.Add($"{name} must lie inside the world");
        }

        if (Seed > Rng.MaxSupportedSeed) errors.Add($"Seed must be between 0 and {Rng.MaxSupportedSeed}, inclusive");
        NonNegative(Deposit, nameof(Deposit));
        if (!(Evaporation >= 0f && Evaporation <= 1f)) errors.Add("Evaporation must lie in [0, 1]");
        if (FieldCadence != 1) errors.Add("FieldCadence must be 1: the field updates every tick");
        Positive(Speed, nameof(Speed));
        NonNegative(TurnNoise, nameof(TurnNoise));
        NonNegative(SensorAngle, nameof(SensorAngle));
        Positive(SensorDistance, nameof(SensorDistance));
        if (FieldColumns <= 0 || FieldRows <= 0) errors.Add("FieldColumns and FieldRows must be positive");
        else if ((long)FieldColumns * FieldRows > MaxFieldCells) errors.Add($"FieldColumns times FieldRows must not exceed {MaxFieldCells}");
        Positive(NestRadius, nameof(NestRadius));
        Positive(FoodRadius, nameof(FoodRadius));
        if (FoodAmount < 0) errors.Add("FoodAmount must be non-negative");
        Positive(WorldWidth, nameof(WorldWidth));
        Positive(WorldHeight, nameof(WorldHeight));
        Positive(FixedDeltaTime, nameof(FixedDeltaTime));
        // A longer step can jump over a circle forever: ants would starve without any error.
        if (Speed * FixedDeltaTime > 2f * MathF.Min(NestRadius, FoodRadius))
            errors.Add("Speed times FixedDeltaTime must not exceed the nest or food diameter");
        Inside(NestX, WorldWidth, nameof(NestX));
        Inside(NestY, WorldHeight, nameof(NestY));
        Inside(FoodX, WorldWidth, nameof(FoodX));
        Inside(FoodY, WorldHeight, nameof(FoodY));
        return errors;
    }

    /// <summary>Reads a JSON object; omitted fields retain their defaults.</summary>
    public static AntColonySettings FromJson(string json) => ConfigurationJson.Deserialize<AntColonySettings>(json, config => config.Validate());

    /// <summary>Serializes the complete settings after validation.</summary>
    public string ToJson() => ConfigurationJson.Serialize(this, Validate());
}
