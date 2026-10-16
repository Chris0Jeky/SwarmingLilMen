using SwarmSim.Core;
using SwarmSim.Core.Canonical;

namespace SwarmSim.Tests;

public class NonFiniteIngestTests
{
    private static CanonicalWorld CreateCanonicalWorld()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            TargetSpeed = 2f,
            MaxForce = 1f,
            FieldOfView = 360f,
            SenseRadius = 5f
        };
        return new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
    }

    [Fact]
    [Trait("Category", "InputValidationIngest")]
    public void AddAgent_NaN_ReturnsMinusOneAndLeavesCountUnchanged()
    {
        var world = new World(new SimConfig(), seed: 42u);

        int result = world.AddAgent(float.NaN, 0f);

        Assert.Equal(-1, result);
        Assert.Equal(0, world.Count);
    }

    [Fact]
    [Trait("Category", "InputValidationIngest")]
    public void AddAgent_PositiveInfinity_ReturnsMinusOneAndLeavesCountUnchanged()
    {
        var world = new World(new SimConfig(), seed: 42u);

        int result = world.AddAgent(0f, float.PositiveInfinity);

        Assert.Equal(-1, result);
        Assert.Equal(0, world.Count);
    }

    [Fact]
    [Trait("Category", "InputValidationIngest")]
    public void TryAddBoid_NaNPosition_ReturnsFalseAndLeavesCountUnchanged()
    {
        var world = CreateCanonicalWorld();

        bool result = world.TryAddBoid(new Vec2(float.NaN, 0f), new Vec2(1f, 0f));

        Assert.False(result);
        Assert.Equal(0, world.Count);
    }

    [Fact]
    [Trait("Category", "InputValidationIngest")]
    public void TryAddBoid_InfiniteVelocity_ReturnsFalseAndLeavesCountUnchanged()
    {
        var world = CreateCanonicalWorld();

        bool result = world.TryAddBoid(new Vec2(1f, 1f), new Vec2(float.PositiveInfinity, 0f));

        Assert.False(result);
        Assert.Equal(0, world.Count);
    }

    [Fact]
    [Trait("Category", "InputValidationIngest")]
    public void SetVelocity_NaN_LeavesVelocityUnchanged()
    {
        var world = CreateCanonicalWorld();
        Assert.True(world.TryAddBoid(new Vec2(1f, 1f), new Vec2(1f, 0f)));
        Vec2 before = world.Boids[0].Velocity;

        world.SetVelocity(0, new Vec2(float.NaN, 0f));

        Assert.Equal(before.X, world.Boids[0].Velocity.X);
        Assert.Equal(before.Y, world.Boids[0].Velocity.Y);
    }

    [Fact]
    [Trait("Category", "InputValidationIngest")]
    public void ValidAdds_Succeed()
    {
        var world = new World(new SimConfig(), seed: 42u);

        int idx = world.AddAgent(100f, 100f);

        Assert.Equal(0, idx);
        Assert.Equal(1, world.Count);

        var canonical = CreateCanonicalWorld();

        bool added = canonical.TryAddBoid(new Vec2(1f, 1f), new Vec2(1f, 0f));

        Assert.True(added);
        Assert.Equal(1, canonical.Count);
    }

    [Fact]
    [Trait("Category", "InputValidationIngest")]
    public void World_Tick_AfterRejectedAdds_LeavesPositionsFinite()
    {
        var world = new World(new SimConfig(), seed: 42u);
        Assert.Equal(-1, world.AddAgent(float.NaN, 0f));
        Assert.Equal(-1, world.AddAgent(0f, float.PositiveInfinity));
        Assert.Equal(0, world.AddAgent(100f, 100f));

        world.Tick();

        Assert.Equal(1, world.Count);
        Assert.True(float.IsFinite(world.X[0]));
        Assert.True(float.IsFinite(world.Y[0]));
        Assert.True(float.IsFinite(world.Vx[0]));
        Assert.True(float.IsFinite(world.Vy[0]));
    }

    [Fact]
    [Trait("Category", "InputValidationIngest")]
    public void Canonical_Step_AfterRejectedAdds_LeavesPositionsFinite()
    {
        var world = CreateCanonicalWorld();
        Assert.False(world.TryAddBoid(new Vec2(float.NaN, 0f), new Vec2(1f, 0f)));
        Assert.False(world.TryAddBoid(new Vec2(1f, 1f), new Vec2(float.PositiveInfinity, 0f)));
        Assert.True(world.TryAddBoid(new Vec2(1f, 1f), new Vec2(1f, 0f)));

        world.Step(0.1f);

        Assert.Equal(1, world.Count);
        Assert.True(float.IsFinite(world.Boids[0].Position.X));
        Assert.True(float.IsFinite(world.Boids[0].Position.Y));
        Assert.True(float.IsFinite(world.Boids[0].Velocity.X));
        Assert.True(float.IsFinite(world.Boids[0].Velocity.Y));
    }
}
