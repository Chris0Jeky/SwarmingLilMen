using SwarmSim.Core;
using SwarmSim.Core.Utils;

namespace SwarmSim.Tests;

public sealed class GenomeMutateValidationTests
{
    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    public void Mutate_InvalidRate_ThrowsBeforeConsumingRandomness(float rate)
    {
        var rng = new Rng(42u);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Genome.Default.Mutate(rng, rate));
        Assert.Equal(new Rng(42u).Next(), rng.Next());
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Mutate_NonFiniteDeviation_ThrowsBeforeConsumingRandomness(float deviation)
    {
        var rng = new Rng(42u);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Genome.Default.Mutate(rng, mutationStdDev: deviation));
        Assert.Equal(new Rng(42u).Next(), rng.Next());
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    public void Mutate_RateEndpointsWithZeroDeviation_PreserveContinuousTraits(float rate)
    {
        var mutated = Genome.Default.Mutate(new Rng(42u), rate, 0f);
        Assert.Equal(1f, mutated.SpeedFactor);
        Assert.Equal(1f, mutated.SenseFactor);
        Assert.Equal(0f, mutated.Aggression);
    }

    [Fact]
    public void Mutate_NullRng_Throws()
    {
        Rng rng = null!;
        Assert.Throws<ArgumentNullException>(() => Genome.Default.Mutate(rng));
    }
}
