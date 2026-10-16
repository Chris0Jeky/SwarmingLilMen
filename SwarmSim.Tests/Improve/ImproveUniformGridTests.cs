using SwarmSim.Core.Spatial;

namespace SwarmSim.Tests;

/// <summary>
/// Pins risky UniformGrid behaviours not covered by UniformGridTests:
/// constructor guards, Rebuild validation, and Query3x3 boundary/empty/limit cases.
/// Each test asserts what the current code does.
/// </summary>
public class ImproveUniformGridTests
{
    [Fact]
    public void Constructor_NonPositiveCellSize_ThrowsArgumentException()
    {
        var exZero = Assert.Throws<ArgumentException>(() => new UniformGrid(0f, 100f, 100f, 10));
        Assert.Equal("cellSize", exZero.ParamName);

        var exNegative = Assert.Throws<ArgumentException>(() => new UniformGrid(-5f, 100f, 100f, 10));
        Assert.Equal("cellSize", exNegative.ParamName);
    }

    [Fact]
    public void Constructor_NonPositiveWorldDimensions_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new UniformGrid(10f, 0f, 100f, 10));
        Assert.Throws<ArgumentException>(() => new UniformGrid(10f, 100f, -100f, 10));
    }

    [Fact]
    public void Constructor_NonPositiveCapacity_ThrowsArgumentException()
    {
        var exZero = Assert.Throws<ArgumentException>(() => new UniformGrid(10f, 100f, 100f, 0));
        Assert.Equal("capacity", exZero.ParamName);

        var exNegative = Assert.Throws<ArgumentException>(() => new UniformGrid(10f, 100f, 100f, -4));
        Assert.Equal("capacity", exNegative.ParamName);
    }

    [Fact]
    public void Rebuild_NullPositionArrays_ThrowsArgumentNullException()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 4);
        var valid = new float[] { 5f };

        var exX = Assert.Throws<ArgumentNullException>(() => grid.Rebuild(null!, valid, 0));
        Assert.Equal("x", exX.ParamName);

        var exY = Assert.Throws<ArgumentNullException>(() => grid.Rebuild(valid, null!, 0));
        Assert.Equal("y", exY.ParamName);
    }

    [Fact]
    public void Rebuild_CountBeyondArrayLengths_ThrowsArgumentOutOfRange()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 10);
        var shortArray = new float[] { 5f, 15f };
        var longArray = new float[] { 5f, 15f, 25f };

        var exX = Assert.Throws<ArgumentOutOfRangeException>(() => grid.Rebuild(shortArray, longArray, 3));
        Assert.Equal("count", exX.ParamName);

        var exY = Assert.Throws<ArgumentOutOfRangeException>(() => grid.Rebuild(longArray, shortArray, 3));
        Assert.Equal("count", exY.ParamName);
    }

    [Fact]
    public void Query3x3_QueryPointOutsideWorld_ClampsToEdgeCells()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 10);
        var x = new float[] { 5f, 95f };
        var y = new float[] { 5f, 95f };
        grid.Rebuild(x, y, count: 2);

        var fromBelow = new List<int>();
        grid.Query3x3(-50f, -50f, idx => fromBelow.Add(idx));
        Assert.Contains(0, fromBelow);
        Assert.DoesNotContain(1, fromBelow);

        var fromAbove = new List<int>();
        grid.Query3x3(500f, 500f, idx => fromAbove.Add(idx));
        Assert.Contains(1, fromAbove);
        Assert.DoesNotContain(0, fromAbove);
    }

    [Fact]
    public void Query3x3_EmptyGrid_FindsNothing()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 10);
        grid.Rebuild(new float[10], new float[10], count: 0);

        var found = new List<int>();
        grid.Query3x3(50f, 50f, idx => found.Add(idx));
        Assert.Empty(found);

        int count = grid.Query3x3(50f, 50f, new int[4], maxResults: 4);
        Assert.Equal(0, count);
    }

    [Fact]
    public void Query3x3_ZeroMaxResults_CountsWithoutWriting()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 10);
        var x = new float[] { 5f, 6f, 7f };
        var y = new float[] { 5f, 5f, 5f };
        grid.Rebuild(x, y, count: 3);

        var buffer = new int[] { -1, -1, -1 };
        int count = grid.Query3x3(5f, 5f, buffer, maxResults: 0);

        Assert.Equal(3, count);
        Assert.Equal(new int[] { -1, -1, -1 }, buffer);
    }
}
