using SwarmSim.Core;
using SwarmSim.Core.Spatial;

namespace SwarmSim.Tests;

/// <summary>
/// The grid cell count is bounded and computed without 32-bit overflow, and the bound is
/// rejected at config preflight, before any large array is allocated (#138).
/// </summary>
public class UniformGridCellBudgetTests
{
    private const long AllocationCeilingBytes = 4L * 1024 * 1024; // far below the ~400 MB the bug allocated

    [Theory]
    [InlineData(100f, 100f, 0.01f)]       // 1e8 cells: passed validation and allocated ~400 MB before
    [InlineData(100f, 100f, 0.0001f)]     // 1e12 cells: Cols * Rows overflowed int
    [InlineData(100000f, 100000f, 0.001f)] // 1e16 cells: each count alone exceeds int
    [InlineData(1e30f, 1e30f, 1e-6f)]     // float quotient not representable as int
    public void Validate_RejectsUnrepresentableOrOversizedGrid_WithoutAllocating(float width, float height, float gridCellSize)
    {
        var config = new SimConfig { WorldWidth = width, WorldHeight = height, GridCellSize = gridCellSize, InitialCapacity = 2 };

        long before = GC.GetAllocatedBytesForCurrentThread();
        var errors = config.Validate();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        var error = Assert.Single(errors, e => e.Contains("GridCellSize", StringComparison.Ordinal) && e.Contains("grid cells", StringComparison.Ordinal));
        Assert.Contains("WorldWidth", error, StringComparison.Ordinal);
        Assert.True(allocated < AllocationCeilingBytes, $"Validate allocated {allocated} bytes");
    }

    [Fact]
    public void World_RejectsOversizedGridAtPreflight_WithoutAllocating()
    {
        var config = new SimConfig { WorldWidth = 100f, WorldHeight = 100f, GridCellSize = 0.01f, InitialCapacity = 2 };

        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<ArgumentException>(() => new World(config, seed: 1));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < AllocationCeilingBytes, $"World ctor allocated {allocated} bytes");
    }

    [Theory]
    [InlineData(0.01f, 100f, 100f)]
    [InlineData(0.0001f, 100f, 100f)]
    [InlineData(0.001f, 100000f, 100000f)]
    public void Ctor_RejectsOversizedGrid_WithoutAllocating(float cellSize, float width, float height)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<ArgumentException>(() => new UniformGrid(cellSize, width, height, capacity: 2));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < AllocationCeilingBytes, $"UniformGrid ctor allocated {allocated} bytes");
    }

    [Fact]
    public void Ctor_AcceptsGridAtTheCellCap()
    {
        // 2048 x 2048 = 2^22 cells exactly.
        var grid = new UniformGrid(1f, 2048f, 2048f, capacity: 2);
        Assert.Equal(UniformGrid.MaxCells, grid.TotalCells);

        Assert.Throws<ArgumentException>(() => new UniformGrid(1f, 2049f, 2048f, capacity: 2));
    }

    [Theory]
    [InlineData(1e30f, 1e-30f)]
    [InlineData(float.MaxValue, float.Epsilon)]
    public void Ctor_UnderflowingWorldToCellRatioStillCreatesUsableGrid(float cellSize, float extent)
    {
        var grid = new UniformGrid(cellSize, extent, extent, capacity: 2);
        var positions = new[] { 0f, extent * 0.5f };

        grid.Rebuild(positions, positions, positions.Length);
        var results = new int[2];
        int count = grid.Query3x3(0f, 0f, results, results.Length);

        Assert.Equal(2, count);
        Array.Sort(results);
        Assert.Equal(new[] { 0, 1 }, results);
    }

    [Fact]
    public void ShippedPresetsAndConfigs_StillValidate()
    {
        Assert.Empty(new SimConfig().Validate());
        Assert.Empty(SimConfig.PeacefulFlocks().Validate());
        Assert.Empty(SimConfig.Warbands().Validate());
        Assert.Empty(SimConfig.RapidEvolution().Validate());

        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "SwarmingLilMen.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);

        var files = Directory.GetFiles(Path.Combine(dir!, "configs"), "*.json");
        Assert.NotEmpty(files);
        foreach (var file in files)
            SimConfig.LoadFromJson(file); // throws on any validation error
    }
}
