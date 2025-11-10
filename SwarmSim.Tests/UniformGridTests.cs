using SwarmSim.Core.Spatial;
using SwarmSim.Core.Utils;

namespace SwarmSim.Tests;

public class UniformGridTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesGrid()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 1000);

        Assert.Equal(10f, grid.CellSize);
        Assert.Equal(10, grid.Cols); // 100 / 10 = 10
        Assert.Equal(10, grid.Rows);
        Assert.Equal(100, grid.TotalCells); // 10 * 10
    }

    [Fact]
    public void Constructor_NonDivisibleDimensions_RoundsUp()
    {
        var grid = new UniformGrid(cellSize: 15f, worldWidth: 100f, worldHeight: 100f, capacity: 1000);

        // 100 / 15 = 6.666... → ceil = 7
        Assert.Equal(7, grid.Cols);
        Assert.Equal(7, grid.Rows);
        Assert.Equal(49, grid.TotalCells); // 7 * 7
    }

    [Fact]
    public void Rebuild_EmptyWorld_AllCellsEmpty()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 1000);
        var x = new float[1000];
        var y = new float[1000];

        grid.Rebuild(x, y, count: 0);

        var stats = grid.GetStats(agentCount: 0);
        Assert.Equal(0, stats.OccupiedCells);
        Assert.Equal(100, stats.EmptyCells);
    }

    [Fact]
    public void Rebuild_SingleAgent_OneOccupiedCell()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 1000);
        var x = new float[] { 5f };
        var y = new float[] { 5f };

        grid.Rebuild(x, y, count: 1);

        var stats = grid.GetStats(agentCount: 1);
        Assert.Equal(1, stats.OccupiedCells);
        Assert.Equal(99, stats.EmptyCells);
        Assert.Equal(1, stats.MaxAgentsPerCell);
    }

    [Fact]
    public void Rebuild_MultipleAgentsSameCell_CountsCorrectly()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 1000);
        // All agents in cell (0, 0) which covers [0, 10) x [0, 10)
        var x = new float[] { 1f, 2f, 3f, 4f, 5f };
        var y = new float[] { 1f, 2f, 3f, 4f, 5f };

        grid.Rebuild(x, y, count: 5);

        var stats = grid.GetStats(agentCount: 5);
        Assert.Equal(1, stats.OccupiedCells);
        Assert.Equal(5, stats.MaxAgentsPerCell);
        Assert.Equal(5f, stats.AvgAgentsPerOccupiedCell);
    }

    [Fact]
    public void Query3x3_FindsAgentInSameCell()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 1000);
        var x = new float[] { 5f, 25f }; // Agent 0 at (5,5), Agent 1 at (25,25)
        var y = new float[] { 5f, 25f };

        grid.Rebuild(x, y, count: 2);

        var found = new List<int>();
        grid.Query3x3(7f, 7f, idx => found.Add(idx)); // Query near agent 0

        Assert.Contains(0, found);
        Assert.DoesNotContain(1, found); // Agent 1 is too far (in different 3x3 neighborhood)
    }

    [Fact]
    public void Query3x3_FindsAgentsInNeighborCells()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 1000);
        // Agent 0 at (5, 5) → cell (0, 0)
        // Agent 1 at (15, 5) → cell (1, 0) - neighbor
        // Agent 2 at (5, 15) → cell (0, 1) - neighbor
        // Agent 3 at (25, 25) → cell (2, 2) - not neighbor
        var x = new float[] { 5f, 15f, 5f, 25f };
        var y = new float[] { 5f, 5f, 15f, 25f };

        grid.Rebuild(x, y, count: 4);

        var found = new List<int>();
        grid.Query3x3(5f, 5f, idx => found.Add(idx));

        Assert.Contains(0, found);
        Assert.Contains(1, found); // Adjacent cell
        Assert.Contains(2, found); // Adjacent cell
        Assert.DoesNotContain(3, found); // Too far
    }

    [Fact]
    public void Query3x3_BufferVersion_ReturnsCorrectCount()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 1000);
        var x = new float[] { 5f, 15f, 5f };
        var y = new float[] { 5f, 5f, 15f };

        grid.Rebuild(x, y, count: 3);

        Span<int> buffer = stackalloc int[10];
        int count = grid.Query3x3(5f, 5f, buffer, maxResults: 10);

        Assert.Equal(3, count);
        var results = buffer[0..count].ToArray();
        Assert.Contains(0, results);
        Assert.Contains(1, results);
        Assert.Contains(2, results);
    }

    [Fact]
    public void Query3x3_BufferTooSmall_ReturnsActualCount()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 1000);
        var x = new float[] { 5f, 6f, 7f, 8f, 9f }; // 5 agents in same cell
        var y = new float[] { 5f, 5f, 5f, 5f, 5f };

        grid.Rebuild(x, y, count: 5);

        Span<int> buffer = stackalloc int[3]; // Only room for 3
        int count = grid.Query3x3(5f, 5f, buffer, maxResults: 3);

        // Should return actual count (5) even though only 3 fit in buffer
        Assert.Equal(5, count);
    }

    [Fact]
    public void Query3x3_BoundaryCell_DoesNotCrash()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 1000);
        var x = new float[] { 0f, 99f };
        var y = new float[] { 0f, 99f };

        grid.Rebuild(x, y, count: 2);

        var found = new List<int>();
        // Query at boundaries
        grid.Query3x3(0f, 0f, idx => found.Add(idx));
        grid.Query3x3(99f, 99f, idx => found.Add(idx));

        // Should find both agents without crashing
        Assert.Contains(0, found);
        Assert.Contains(1, found);
    }

    [Fact]
    public void Query3x3_CompareWithBruteForce_MatchesExactly()
    {
        // Property test: grid query should match brute force within radius
        var rng = new Rng(42);
        var grid = new UniformGrid(cellSize: 20f, worldWidth: 200f, worldHeight: 200f, capacity: 100);

        // Create 100 random agents
        var x = new float[100];
        var y = new float[100];
        for (int i = 0; i < 100; i++)
        {
            x[i] = rng.NextFloat(0f, 200f);
            y[i] = rng.NextFloat(0f, 200f);
        }

        grid.Rebuild(x, y, count: 100);

        // Pick a random query point
        float qx = 100f;
        float qy = 100f;
        float radius = 30f; // Should cover 3x3 cells (cellSize=20)

        // Grid query
        var gridResults = new HashSet<int>();
        grid.Query3x3(qx, qy, idx =>
        {
            float dx = x[idx] - qx;
            float dy = y[idx] - qy;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist <= radius)
                gridResults.Add(idx);
        });

        // Brute force
        var bruteForceResults = new HashSet<int>();
        for (int i = 0; i < 100; i++)
        {
            float dx = x[i] - qx;
            float dy = y[i] - qy;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist <= radius)
                bruteForceResults.Add(i);
        }

        // Should match exactly
        Assert.Equal(bruteForceResults, gridResults);
    }

    [Fact]
    public void Rebuild_CalledMultipleTimes_UpdatesCorrectly()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 1000);
        var x = new float[1000];
        var y = new float[1000];

        // First rebuild with 5 agents
        for (int i = 0; i < 5; i++)
        {
            x[i] = 5f + i * 10f;
            y[i] = 5f;
        }
        grid.Rebuild(x, y, count: 5);

        var stats1 = grid.GetStats(agentCount: 5);
        Assert.Equal(5, stats1.OccupiedCells);

        // Second rebuild with 10 agents
        for (int i = 0; i < 10; i++)
        {
            x[i] = 5f + i * 10f;
            y[i] = 5f;
        }
        grid.Rebuild(x, y, count: 10);

        var stats2 = grid.GetStats(agentCount: 10);
        Assert.Equal(10, stats2.OccupiedCells);
    }

    [Fact]
    public void GetStats_CalculatesCorrectAverages()
    {
        var grid = new UniformGrid(cellSize: 10f, worldWidth: 100f, worldHeight: 100f, capacity: 1000);
        // Cell 0: 3 agents
        // Cell 1: 2 agents
        // Cell 2: 1 agent
        var x = new float[] { 1f, 2f, 3f, 11f, 12f, 21f };
        var y = new float[] { 1f, 1f, 1f, 1f, 1f, 1f };

        grid.Rebuild(x, y, count: 6);

        var stats = grid.GetStats(agentCount: 6);
        Assert.Equal(3, stats.OccupiedCells);
        Assert.Equal(97, stats.EmptyCells);
        Assert.Equal(3, stats.MaxAgentsPerCell);
        Assert.Equal(2f, stats.AvgAgentsPerOccupiedCell); // (3+2+1)/3 = 2
    }
}
