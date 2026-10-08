using SwarmSim.Core.Systems;

namespace SwarmSim.Tests;

public sealed class SenseSystemInitializeTests
{
    [Fact]
    public void Initialize_NegativeOne_ThrowsArgumentOutOfRangeException()
    {
        var system = new SenseSystem();

        Assert.Throws<ArgumentOutOfRangeException>(() => system.Initialize(-1));
    }

    [Fact]
    public void Initialize_Zero_ThrowsArgumentOutOfRangeException()
    {
        var system = new SenseSystem();

        Assert.Throws<ArgumentOutOfRangeException>(() => system.Initialize(0));
    }

    [Fact]
    public void Initialize_PositiveCapacity_Initializes()
    {
        var system = new SenseSystem();

        system.Initialize(4);

        Assert.Equal(4, system.NeighborCounts.Length);
    }
}
