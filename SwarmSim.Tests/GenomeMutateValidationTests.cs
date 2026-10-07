using SwarmSim.Core;
using SwarmSim.Core.Utils;

namespace SwarmSim.Tests;

public sealed class GenomeMutateValidationTests
{
    [Fact]
    public void Mutate_NaNRate_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Genome.Default.Mutate(new Rng(42u), float.NaN));
    }

    [Fact]
    public void Mutate_NullRng_Throws()
    {
        Rng rng = null!;
        Assert.Throws<ArgumentNullException>(() => Genome.Default.Mutate(rng));
    }
}
