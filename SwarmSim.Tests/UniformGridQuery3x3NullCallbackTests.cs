using SwarmSim.Core.Spatial;

namespace SwarmSim.Tests;

public class UniformGridQuery3x3NullCallbackTests
{
    [Fact]
    public void Query3x3_NullCallback_EmptyGrid_ThrowsArgumentNullException()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 10);
        grid.Rebuild(new float[10], new float[10], count: 0);

        var ex = Assert.Throws<ArgumentNullException>(() => grid.Query3x3(50f, 50f, null!));
        Assert.Equal("callback", ex.ParamName);
    }

    [Fact]
    public void Query3x3_NullCallback_OccupiedGrid_ThrowsArgumentNullException()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 10);
        grid.Rebuild(new float[] { 5f }, new float[] { 5f }, count: 1);

        var ex = Assert.Throws<ArgumentNullException>(() => grid.Query3x3(5f, 5f, null!));
        Assert.Equal("callback", ex.ParamName);
    }
}
