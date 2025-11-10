namespace SwarmSim.Core.Utils;

/// <summary>
/// Deterministic random number generator wrapper.
/// Uses System.Random with explicit seed for reproducibility.
/// NOT thread-safe - each thread needs its own instance.
/// </summary>
public sealed class Rng
{
    private readonly Random _random;
    private readonly uint _seed;

    /// <summary>
    /// Creates RNG with explicit seed for deterministic behavior.
    /// </summary>
    public Rng(uint seed)
    {
        _seed = seed;
        _random = new Random((int)seed);
    }

    /// <summary>Gets the seed used to initialize this RNG.</summary>
    public uint Seed => _seed;

    /// <summary>
    /// Returns a non-negative random integer.
    /// </summary>
    public int Next() => _random.Next();

    /// <summary>
    /// Returns a non-negative random integer less than maxValue.
    /// </summary>
    public int Next(int maxValue) => _random.Next(maxValue);

    /// <summary>
    /// Returns a random integer within [minValue, maxValue).
    /// </summary>
    public int Next(int minValue, int maxValue) => _random.Next(minValue, maxValue);

    /// <summary>
    /// Returns a random float in [0.0, 1.0).
    /// </summary>
    public float NextFloat() => _random.NextSingle();

    /// <summary>
    /// Returns a random float in [minValue, maxValue).
    /// </summary>
    public float NextFloat(float minValue, float maxValue)
        => minValue + _random.NextSingle() * (maxValue - minValue);

    /// <summary>
    /// Returns a random double in [0.0, 1.0).
    /// </summary>
    public double NextDouble() => _random.NextDouble();

    /// <summary>
    /// Returns a random boolean.
    /// </summary>
    public bool NextBool() => _random.Next(2) == 1;

    /// <summary>
    /// Returns a random boolean with given probability of true.
    /// </summary>
    /// <param name="probability">Probability of returning true (0.0 to 1.0)</param>
    public bool NextBool(float probability) => NextFloat() < probability;

    /// <summary>
    /// Returns a random value from a Gaussian (normal) distribution.
    /// Mean = 0, Standard Deviation = 1.
    /// Uses Box-Muller transform.
    /// </summary>
    public float NextGaussian()
    {
        // Box-Muller transform
        float u1 = NextFloat();
        float u2 = NextFloat();

        // Avoid log(0)
        if (u1 < 1e-10f)
            u1 = 1e-10f;

        float r = MathF.Sqrt(-2.0f * MathF.Log(u1));
        float theta = 2.0f * MathF.PI * u2;

        return r * MathF.Cos(theta);
    }

    /// <summary>
    /// Returns a random value from a Gaussian distribution with given mean and std dev.
    /// </summary>
    public float NextGaussian(float mean, float stdDev)
        => mean + NextGaussian() * stdDev;

    /// <summary>
    /// Fills the byte array with random bytes.
    /// </summary>
    public void NextBytes(byte[] buffer) => _random.NextBytes(buffer);

    /// <summary>
    /// Fills the span with random bytes.
    /// </summary>
    public void NextBytes(Span<byte> buffer) => _random.NextBytes(buffer);

    /// <summary>
    /// Returns a random unit vector (2D, normalized).
    /// </summary>
    public (float x, float y) NextUnitVector()
    {
        float angle = NextFloat(0f, 2f * MathF.PI);
        return (MathF.Cos(angle), MathF.Sin(angle));
    }

    /// <summary>
    /// Returns a random point within a circle of given radius.
    /// </summary>
    public (float x, float y) NextPointInCircle(float radius)
    {
        float angle = NextFloat(0f, 2f * MathF.PI);
        float r = MathF.Sqrt(NextFloat()) * radius; // sqrt for uniform distribution
        return (r * MathF.Cos(angle), r * MathF.Sin(angle));
    }

    /// <summary>
    /// Shuffles an array in place using Fisher-Yates algorithm.
    /// </summary>
    public void Shuffle<T>(T[] array)
    {
        int n = array.Length;
        for (int i = n - 1; i > 0; i--)
        {
            int j = Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }

    /// <summary>
    /// Shuffles a span in place using Fisher-Yates algorithm.
    /// </summary>
    public void Shuffle<T>(Span<T> span)
    {
        int n = span.Length;
        for (int i = n - 1; i > 0; i--)
        {
            int j = Next(i + 1);
            (span[i], span[j]) = (span[j], span[i]);
        }
    }
}
