using SwarmSim.Core;
using SwarmSim.Core.Systems;

namespace SwarmSim.Tests;

public sealed class LegacyWrapSensingTests
{
    [Fact]
    public void WrapSensing_AcrossBothSeams_MatchesTranslatedInterior()
    {
        var config = CreateConfig();
        var seam = CreatePair(config, 1f, 1f, 199f, 99f);
        var interior = CreatePair(config, 51f, 51f, 49f, 49f);
        seam.Vx[1] = interior.Vx[1] = 3f;
        seam.Vy[1] = interior.Vy[1] = 4f;

        var seamSense = Sense(seam);
        var interiorSense = Sense(interior);

        Assert.Equal(1, seamSense.NeighborCounts[0]);
        Assert.Equal(interiorSense.NeighborCounts[0], seamSense.NeighborCounts[0]);
        Assert.Equal(-2f, seamSense.NearestNeighborDx[0]);
        Assert.Equal(-2f, seamSense.NearestNeighborDy[0]);
        Assert.Equal(8f, seamSense.NearestNeighborDistSq[0]);
        Assert.Equal(interiorSense.SeparationX[0], seamSense.SeparationX[0]);
        Assert.Equal(interiorSense.SeparationY[0], seamSense.SeparationY[0]);
        Assert.Equal(3f, seamSense.AlignmentVx[0]);
        Assert.Equal(4f, seamSense.AlignmentVy[0]);
        Assert.Equal(-2f, seamSense.CohesionX[0] - seam.X[0]);
        Assert.Equal(-2f, seamSense.CohesionY[0] - seam.Y[0]);

        new BehaviorSystem(seamSense).Run(seam, config.FixedDeltaTime);
        new BehaviorSystem(interiorSense).Run(interior, config.FixedDeltaTime);
        Assert.NotEqual(0f, seam.Fx[0]);
        Assert.NotEqual(0f, seam.Fy[0]);
        Assert.Equal(interior.Fx[0], seam.Fx[0]);
        Assert.Equal(interior.Fy[0], seam.Fy[0]);
    }

    [Fact]
    public void WrapTick_CohesionSteersThroughTheNearestImage()
    {
        var config = CreateConfig(separationRadius: 0.01f);
        var seam = CreatePair(config, 199f, 50f, 1f, 50f);
        var interior = CreatePair(config, 99f, 50f, 101f, 50f);

        seam.Tick();
        interior.Tick();

        Assert.True(seam.Fx[0] > 0f, "Cohesion should steer right across the seam.");
        Assert.Equal(interior.Fx[0], seam.Fx[0]);
        Assert.Equal(interior.Vx[0], seam.Vx[0]);
        Assert.Equal(0f, seam.Fy[0]);
    }

    [Theory]
    [InlineData(-1f, 1)]
    [InlineData(1f, 0)]
    public void WrapSensing_FieldOfViewUsesMinimumImageDirection(float velocityX, int expectedCount)
    {
        var world = CreatePair(CreateConfig(fieldOfView: 90f), 1f, 50f, 199f, 50f);
        world.Vx[0] = velocityX;

        Assert.Equal(expectedCount, Sense(world).NeighborCounts[0]);
    }

    [Theory]
    [InlineData(8f, 8f)]
    [InlineData(8f, 20f)]
    [InlineData(20f, 8f)]
    [InlineData(20f, 20f)]
    public void WrapSensing_OneAndTwoCellAxes_DoNotDuplicateNeighbors(float width, float height)
    {
        var config = CreateConfig(width: width, height: height, senseRadius: 4f);
        var world = new World(config, seed: 42);
        world.AddAgent(1f, 1f);
        world.AddAgent(width - 1f, 1f);
        world.AddAgent(1f, height - 1f);
        world.AddAgent(width - 1f, height - 1f);

        var sense = Sense(world);

        for (int i = 0; i < world.Count; i++)
            Assert.Equal(3, sense.NeighborCounts[i]);
    }

    [Fact]
    public void WrapSensing_CrossesTheShortTerminalCell()
    {
        // The last X cell spans only [100, 105). Reaching x=98 from x=1 crosses
        // two cell boundaries even though the minimum-image distance is only eight.
        var world = CreatePair(CreateConfig(width: 105f, senseRadius: 8f), 1f, 50f, 98f, 50f);

        var sense = Sense(world);

        Assert.Equal(1, sense.NeighborCounts[0]);
        Assert.Equal(-8f, sense.NearestNeighborDx[0]);
    }

    [Theory]
    [InlineData(BoundaryMode.Reflect)]
    [InlineData(BoundaryMode.Clamp)]
    public void NonWrappingSensing_KeepsEuclideanDistances(BoundaryMode boundaryMode)
    {
        var config = CreateConfig(boundaryMode: boundaryMode);
        var seam = CreatePair(config, 1f, 50f, 199f, 50f);
        var interior = CreatePair(config, 51f, 50f, 49f, 50f);

        Assert.Equal(0, Sense(seam).NeighborCounts[0]);
        var interiorSense = Sense(interior);
        Assert.Equal(1, interiorSense.NeighborCounts[0]);
        Assert.Equal(-2f, interiorSense.NearestNeighborDx[0]);
        Assert.Equal(49f, interiorSense.CohesionX[0]);
    }

    private static SimConfig CreateConfig(
        float width = 200f,
        float height = 100f,
        float senseRadius = 10f,
        float separationRadius = 10f,
        float fieldOfView = 360f,
        BoundaryMode boundaryMode = BoundaryMode.Wrap) => new()
    {
        InitialCapacity = 8,
        WorldWidth = width,
        WorldHeight = height,
        GridCellSize = 10f,
        SenseRadius = senseRadius,
        SeparationRadius = separationRadius,
        FieldOfView = fieldOfView,
        BoundaryMode = boundaryMode,
        CollisionAvoidanceRadius = 0f,
        MaxNeighbors = 0,
        MaxSpeed = 10f,
        MaxForce = 100f,
        WanderStrength = 0f
    };

    private static World CreatePair(SimConfig config, float x0, float y0, float x1, float y1)
    {
        var world = new World(config, seed: 42);
        world.AddAgent(x0, y0);
        world.AddAgent(x1, y1);
        return world;
    }

    private static SenseSystem Sense(World world)
    {
        world.Grid.Rebuild(world.X, world.Y, world.State, world.Count);
        var sense = new SenseSystem();
        sense.Initialize(world.Capacity);
        sense.Run(world, world.Config.FixedDeltaTime);
        return sense;
    }
}
