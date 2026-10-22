using SwarmSim.Core;
using SwarmSim.Core.Spatial;

namespace SwarmSim.Tests;

/// <summary>
/// Pins risky UniformGrid behaviours not covered by UniformGridTests,
/// UniformGridCtorTests, UniformGridNonFiniteCtorTests,
/// UniformGridQuery3x3NullCallbackTests, or ImproveUniformGridTests:
/// the state-filtered Rebuild overload, out-of-world position clamping,
/// empty-grid stats totals, and toroidal
/// radius validation. Each test asserts what the current code does.
/// </summary>
public class ImproveUniformGrid2Tests
{
    [Fact]
    public void Rebuild_WithState_NullStateArray_ThrowsArgumentNullException()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 4);
        var positions = new float[] { 5f };

        var ex = Assert.Throws<ArgumentNullException>(() => grid.Rebuild(positions, positions, null!, 0));
        Assert.Equal("state", ex.ParamName);
    }

    [Fact]
    public void Rebuild_WithState_CountBeyondStateLength_ThrowsArgumentOutOfRange()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 10);
        var x = new float[] { 5f, 15f, 25f };
        var y = new float[] { 5f, 5f, 5f };
        var state = new AgentState[] { AgentState.None, AgentState.None };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => grid.Rebuild(x, y, state, 3));
        Assert.Equal("count", ex.ParamName);
    }

    [Fact]
    public void Rebuild_NegativePositions_ClampedIntoFirstCell()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 10);
        grid.Rebuild(new float[] { -5f }, new float[] { -5f }, count: 1);

        var found = new List<int>();
        grid.Query3x3(5f, 5f, idx => found.Add(idx));

        Assert.Contains(0, found);
    }

    [Fact]
    public void Rebuild_PositionsBeyondWorld_ClampedIntoLastCell()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 10);
        grid.Rebuild(new float[] { 1000f }, new float[] { 1000f }, count: 1);

        var found = new List<int>();
        grid.Query3x3(95f, 95f, idx => found.Add(idx));

        Assert.Contains(0, found);
    }

    [Fact]
    public void GetStats_EmptyGrid_ReportsZeroOccupancyTotals()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 10);
        grid.Rebuild(new float[4], new float[4], count: 0);

        var stats = grid.GetStats(agentCount: 0);

        Assert.Equal(100, stats.TotalCells);
        Assert.Equal(0, stats.MaxAgentsPerCell);
        Assert.Equal(0f, stats.AvgAgentsPerOccupiedCell);
    }

    [Fact]
    public void QueryRadiusToroidal_InvalidRadius_ThrowsArgumentOutOfRange()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 10);
        grid.Rebuild(new float[4], new float[4], count: 0);

        foreach (float radius in new float[] { float.NaN, -1f, float.PositiveInfinity })
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => grid.QueryRadiusToroidal(
                50f, 50f, radius, 0,
                ReadOnlySpan<float>.Empty, ReadOnlySpan<float>.Empty, 0,
                Span<int>.Empty, out _));
            Assert.Equal("radius", ex.ParamName);
        }
    }
}
