using SwarmSim.Core.Spatial;

namespace SwarmSim.Tests;

public class UniformGridNonFiniteCtorTests
{
    [Fact]
    public void Ctor_NaNCellSize_Throws()
    {
        Assert.Throws<ArgumentException>(() => new UniformGrid(float.NaN, 100f, 100f, 10));
    }

    [Fact]
    public void Ctor_InfiniteCellSize_Throws()
    {
        Assert.Throws<ArgumentException>(() => new UniformGrid(float.PositiveInfinity, 100f, 100f, 10));
    }

    [Fact]
    public void Ctor_NonFiniteWorldDimensions_Throws()
    {
        Assert.Throws<ArgumentException>(() => new UniformGrid(10f, float.NaN, 100f, 10));
        Assert.Throws<ArgumentException>(() => new UniformGrid(10f, 100f, float.NaN, 10));
        Assert.Throws<ArgumentException>(() => new UniformGrid(10f, float.PositiveInfinity, 100f, 10));
        Assert.Throws<ArgumentException>(() => new UniformGrid(10f, 100f, float.NegativeInfinity, 10));
    }
}
