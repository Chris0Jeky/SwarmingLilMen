using SwarmSim.Core;
using SwarmSim.Core.Systems;

namespace SwarmSim.Tests;

public sealed class LegacyGridCellSizeTests
{
    [Fact]
    public void World_UsesConfiguredGridCellSize()
    {
        var smaller = new World(CreateConfig(5f, BoundaryMode.Wrap), seed: 42);
        var larger = new World(CreateConfig(40f, BoundaryMode.Wrap), seed: 42);

        Assert.Equal(5f, smaller.Grid.CellSize);
        Assert.Equal(40f, larger.Grid.CellSize);
        Assert.True(smaller.Grid.TotalCells > larger.Grid.TotalCells);
    }

    [Theory]
    [InlineData(BoundaryMode.Wrap)]
    [InlineData(BoundaryMode.Reflect)]
    [InlineData(BoundaryMode.Clamp)]
    public void Sensing_RadiusLargerThanCellsStillFindsTheWholeCircle(BoundaryMode boundaryMode)
    {
        var small = CreateWorld(5f, boundaryMode);
        var large = CreateWorld(40f, boundaryMode);

        var smallSense = Sense(small);
        var largeSense = Sense(large);

        Assert.Equal(3, smallSense.NeighborCounts[0]);
        Assert.Equal(largeSense.NeighborCounts[0], smallSense.NeighborCounts[0]);
        Assert.Equal(largeSense.AlignmentVx[0], smallSense.AlignmentVx[0]);
        Assert.Equal(largeSense.CohesionX[0], smallSense.CohesionX[0]);
        Assert.Equal(largeSense.CohesionY[0], smallSense.CohesionY[0]);
        Assert.Equal(0f, smallSense.NearestNeighborDx[0]);
        Assert.Equal(-17f, smallSense.NearestNeighborDy[0]);
    }

    [Theory]
    [InlineData(BoundaryMode.Wrap)]
    [InlineData(BoundaryMode.Reflect)]
    [InlineData(BoundaryMode.Clamp)]
    public void Sensing_AcrossGridSizesMatchesBruteForceCounts(BoundaryMode boundaryMode)
    {
        // Small populations stay below both legacy neighbor caps. Integer coordinates make
        // the independent double-precision circle oracle exact at the inclusive boundary.
        foreach (float cellSize in new[] { 5f, 7f, 25f, 200f })
        {
            var random = new Random(61233);
            for (int scenario = 0; scenario < 20; scenario++)
            {
                var config = new SimConfig
                {
                    InitialCapacity = 24,
                    WorldWidth = 105f,
                    WorldHeight = 83f,
                    GridCellSize = cellSize,
                    SenseRadius = 20f,
                    BoundaryMode = boundaryMode,
                    FieldOfView = 360f,
                    MaxNeighbors = 0
                };
                var world = new World(config, seed: 42);
                for (int i = 0; i < 24; i++)
                    world.AddAgent(random.Next(105), random.Next(83));
                var sense = Sense(world);

                for (int i = 0; i < world.Count; i++)
                {
                    int expected = 0;
                    for (int j = 0; j < world.Count; j++)
                    {
                        if (i == j)
                            continue;
                        double dx = Math.Abs((double)world.X[j] - world.X[i]);
                        double dy = Math.Abs((double)world.Y[j] - world.Y[i]);
                        if (boundaryMode == BoundaryMode.Wrap)
                        {
                            dx = Math.Min(dx, 105d - dx);
                            dy = Math.Min(dy, 83d - dy);
                        }
                        double distanceSquared = dx * dx + dy * dy;
                        if (distanceSquared >= 0.0001d && distanceSquared <= 400d)
                            expected++;
                    }
                    Assert.Equal(expected, sense.NeighborCounts[i]);
                }
            }
        }
    }

    private static World CreateWorld(float cellSize, BoundaryMode mode)
    {
        var world = new World(CreateConfig(cellSize, mode), seed: 42);
        world.AddAgent(50f, 50f);
        world.AddAgent(70f, 50f); // Inclusive radius boundary, four cells away at size five.
        world.AddAgent(50f, 33f);
        world.AddAgent(38f, 66f); // 12-16-20 triangle, also on the boundary.
        world.AddAgent(70f, 70f); // Inside the candidate square, outside the circle.
        for (int i = 1; i < world.Count; i++)
            world.Vx[i] = i;
        return world;
    }

    private static SimConfig CreateConfig(float cellSize, BoundaryMode mode) => new()
    {
        InitialCapacity = 8,
        WorldWidth = 100f,
        WorldHeight = 100f,
        GridCellSize = cellSize,
        SenseRadius = 20f,
        SeparationRadius = 5f,
        FieldOfView = 360f,
        BoundaryMode = mode,
        MaxNeighbors = 0
    };

    private static SenseSystem Sense(World world)
    {
        world.Grid.Rebuild(world.X, world.Y, world.State, world.Count);
        var sense = new SenseSystem();
        sense.Initialize(world.Capacity);
        sense.Run(world, world.Config.FixedDeltaTime);
        return sense;
    }
}
