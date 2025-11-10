using SwarmSim.Core.Utils;

namespace SwarmSim.Tests;

public class RngTests
{
    [Fact]
    public void Determinism_SameSeed_ProducesSameSequence()
    {
        // Arrange
        const uint seed = 12345u;
        var rng1 = new Rng(seed);
        var rng2 = new Rng(seed);

        // Act & Assert - Generate 100 random numbers
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(rng1.Next(), rng2.Next());
        }
    }

    [Fact]
    public void Determinism_DifferentSeeds_ProduceDifferentSequences()
    {
        // Arrange
        var rng1 = new Rng(12345u);
        var rng2 = new Rng(54321u);

        // Act
        var value1 = rng1.Next();
        var value2 = rng2.Next();

        // Assert
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void NextFloat_ReturnsValueInRange()
    {
        // Arrange
        var rng = new Rng(42u);

        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            float value = rng.NextFloat(10f, 20f);
            Assert.InRange(value, 10f, 20f);
        }
    }

    [Fact]
    public void NextGaussian_ProducesDistributionAroundMean()
    {
        // Arrange
        var rng = new Rng(42u);
        const float mean = 50f;
        const float stdDev = 10f;
        const int samples = 10000;

        // Act
        float sum = 0f;
        for (int i = 0; i < samples; i++)
        {
            sum += rng.NextGaussian(mean, stdDev);
        }

        float actualMean = sum / samples;

        // Assert - Mean should be close (within 1 std dev for large sample)
        Assert.InRange(actualMean, mean - stdDev, mean + stdDev);
    }

    [Fact]
    public void NextBool_ProducesBalancedResults()
    {
        // Arrange
        var rng = new Rng(42u);
        const int samples = 1000;

        // Act
        int trueCount = 0;
        for (int i = 0; i < samples; i++)
        {
            if (rng.NextBool())
                trueCount++;
        }

        // Assert - Should be roughly 50/50 (within 20% tolerance)
        Assert.InRange(trueCount, samples * 0.4, samples * 0.6);
    }

    [Fact]
    public void NextUnitVector_ProducesNormalizedVectors()
    {
        // Arrange
        var rng = new Rng(42u);

        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            (float x, float y) = rng.NextUnitVector();
            float length = MathF.Sqrt(x * x + y * y);

            Assert.InRange(length, 0.999f, 1.001f); // Allow for floating point error
        }
    }

    [Fact]
    public void Shuffle_RearrangesArray()
    {
        // Arrange
        var rng = new Rng(42u);
        int[] original = Enumerable.Range(0, 20).ToArray();
        int[] shuffled = (int[])original.Clone();

        // Act
        rng.Shuffle(shuffled);

        // Assert - Should not be in the same order
        Assert.NotEqual(original, shuffled);

        // But should contain same elements
        Array.Sort(shuffled);
        Assert.Equal(original, shuffled);
    }

    [Fact]
    public void SeedProperty_ReturnsSeedUsedForConstruction()
    {
        // Arrange
        const uint seed = 99999u;
        var rng = new Rng(seed);

        // Act & Assert
        Assert.Equal(seed, rng.Seed);
    }
}
