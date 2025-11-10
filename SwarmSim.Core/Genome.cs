namespace SwarmSim.Core;

/// <summary>
/// Agent genetic traits packed into a readonly struct.
/// Passed by value, no allocations, immutable after creation.
/// </summary>
/// <param name="SpeedFactor">Multiplier for max speed (0.5 to 2.0)</param>
/// <param name="SenseFactor">Multiplier for sense radius (0.5 to 2.0)</param>
/// <param name="Aggression">Base aggression level (-1.0 to 1.0, affects combat/flee)</param>
/// <param name="ColorIdx">Visual color index (0-15, for rendering)</param>
public readonly record struct Genome(
    float SpeedFactor,
    float SenseFactor,
    float Aggression,
    byte ColorIdx)
{
    /// <summary>
    /// Creates a default genome with neutral traits.
    /// </summary>
    public static Genome Default => new(
        SpeedFactor: 1.0f,
        SenseFactor: 1.0f,
        Aggression: 0.0f,
        ColorIdx: 0
    );

    /// <summary>
    /// Creates a random genome within valid trait ranges.
    /// </summary>
    public static Genome Random(Rng rng)
    {
        return new Genome(
            SpeedFactor: rng.NextFloat(0.5f, 2.0f),
            SenseFactor: rng.NextFloat(0.5f, 2.0f),
            Aggression: rng.NextFloat(-1.0f, 1.0f),
            ColorIdx: (byte)rng.Next(16)
        );
    }

    /// <summary>
    /// Creates a mutated copy of this genome with clamped Gaussian noise.
    /// </summary>
    /// <param name="rng">Random number generator</param>
    /// <param name="mutationRate">Probability of each trait mutating (0-1)</param>
    /// <param name="mutationStdDev">Standard deviation of mutation noise</param>
    public Genome Mutate(Rng rng, float mutationRate = 0.1f, float mutationStdDev = 0.2f)
    {
        float MutateFloat(float value, float min, float max)
        {
            if (rng.NextFloat() > mutationRate)
                return value;

            float noise = rng.NextGaussian() * mutationStdDev;
            return Math.Clamp(value + noise, min, max);
        }

        byte MutateByte(byte value, byte max)
        {
            if (rng.NextFloat() > mutationRate)
                return value;

            // For discrete values, shift by ±1
            int shift = rng.NextBool() ? 1 : -1;
            return (byte)Math.Clamp(value + shift, 0, max);
        }

        return new Genome(
            SpeedFactor: MutateFloat(SpeedFactor, 0.5f, 2.0f),
            SenseFactor: MutateFloat(SenseFactor, 0.5f, 2.0f),
            Aggression: MutateFloat(Aggression, -1.0f, 1.0f),
            ColorIdx: MutateByte(ColorIdx, 15)
        );
    }
}
