using SwarmSim.Core.Spatial;

namespace SwarmSim.Tests;

public class UniformGridCtorTests
{
    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(-10f)]
    public void Ctor_NonPositiveCellSize_Throws(float cellSize)
    {
        Assert.Throws<ArgumentException>(() => new UniformGrid(cellSize, 100f, 100f, 100));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Ctor_NonPositiveWorldWidth_Throws(float worldWidth)
    {
        Assert.Throws<ArgumentException>(() => new UniformGrid(10f, worldWidth, 100f, 100));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Ctor_NonPositiveWorldHeight_Throws(float worldHeight)
    {
        Assert.Throws<ArgumentException>(() => new UniformGrid(10f, 100f, worldHeight, 100));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ctor_NonPositiveCapacity_Throws(int capacity)
    {
        Assert.Throws<ArgumentException>(() => new UniformGrid(10f, 100f, 100f, capacity));
    }

    [Fact]
    public void Query3x3_TruncatedBuffer_FirstEntryIsValidAgentIndex()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 100);
        var x = new float[] { 5f, 6f, 7f, 8f, 9f };
        var y = new float[] { 5f, 5f, 5f, 5f, 5f };

        grid.Rebuild(x, y, count: 5);

        Span<int> buffer = stackalloc int[3];
        grid.Query3x3(5f, 5f, buffer, maxResults: 3);

        Assert.InRange(buffer[0], 0, 4);
    }
}
