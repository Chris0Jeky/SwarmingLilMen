using SwarmSim.Core.Canonical;

namespace SwarmSim.Tests;

public sealed class ImproveCanonicalWorld2Tests
{
    private static CanonicalWorld CreateWorld(CanonicalWorldSettings settings) =>
        new(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));

    private static CanonicalWorldSettings CreateSettings() =>
        new()
        {
            InitialCapacity = 4,
            TargetSpeed = 2f,
            MaxForce = 1f,
            FieldOfView = 360f,
            SenseRadius = 5f
        };

    [Fact]
    public void TryAddBoid_GroupTag_RoundTripsAndSurvivesStep()
    {
        var world = CreateWorld(CreateSettings());
        Assert.True(world.TryAddBoid(new Vec2(10f, 10f), new Vec2(1f, 0f)));
        Assert.True(world.TryAddBoid(new Vec2(20f, 20f), new Vec2(0f, 1f), group: 7));

        Assert.Equal(0, world.Boids[0].Group);
        Assert.Equal(7, world.Boids[1].Group);

        world.Step(1f / 60f);

        Assert.Equal(2, world.Count);
        Assert.Equal(0, world.Boids[0].Group);
        Assert.Equal(7, world.Boids[1].Group);
    }

    [Fact]
    public void SetVelocity_PreservesGroupAndPositionWhileRenormalizing()
    {
        var world = CreateWorld(new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            TargetSpeed = 10f,
            MaxForce = 1f,
            FieldOfView = 360f,
            SenseRadius = 5f
        });
        Assert.True(world.TryAddBoid(new Vec2(5f, 5f), new Vec2(1f, 0f), group: 2));

        world.SetVelocity(0, new Vec2(3f, 4f));

        Assert.Equal(5f, world.Boids[0].Position.X);
        Assert.Equal(5f, world.Boids[0].Position.Y);
        Assert.Equal(2, world.Boids[0].Group);
        Assert.Equal(6f, world.Boids[0].Velocity.X, 4);
        Assert.Equal(8f, world.Boids[0].Velocity.Y, 4);
        Assert.Equal(10f, world.Boids[0].Velocity.Length, 4);
    }

    [Fact]
    public void SetVelocity_InfiniteVelocity_LeavesBoidUnchanged()
    {
        var world = CreateWorld(CreateSettings());
        Assert.True(world.TryAddBoid(new Vec2(1f, 1f), new Vec2(1f, 0f)));
        Vec2 before = world.Boids[0].Velocity;

        world.SetVelocity(0, new Vec2(float.PositiveInfinity, 0f));

        Assert.Equal(1, world.Count);
        Assert.Equal(before.X, world.Boids[0].Velocity.X);
        Assert.Equal(before.Y, world.Boids[0].Velocity.Y);
    }

    [Fact]
    public void TryGetMetrics_InvalidIndex_ReturnsFalse()
    {
        var world = CreateWorld(CreateSettings());
        Assert.True(world.TryAddBoid(new Vec2(1f, 1f), new Vec2(1f, 0f)));

        Assert.False(world.TryGetMetrics(0, out _));
        Assert.False(world.TryGetMetrics(-1, out _));
        Assert.False(world.TryGetMetrics(world.Count, out _));

        world.Step(1f / 60f);

        Assert.True(world.TryGetMetrics(0, out _));
        Assert.False(world.TryGetMetrics(-1, out _));
        Assert.False(world.TryGetMetrics(world.Count, out _));
    }

    [Fact]
    public void Ctor_ZeroInitialCapacity_AcceptsSingleBoid()
    {
        var world = CreateWorld(new CanonicalWorldSettings
        {
            InitialCapacity = 0,
            TargetSpeed = 2f,
            MaxForce = 1f,
            FieldOfView = 360f,
            SenseRadius = 5f
        });

        Assert.True(world.TryAddBoid(new Vec2(1f, 1f), new Vec2(1f, 0f)));
        Assert.Equal(1, world.Count);
        Assert.Equal(1, world.Boids.Length);
        Assert.False(world.TryAddBoid(new Vec2(2f, 2f), new Vec2(0f, 1f)));
        Assert.Equal(1, world.Count);
    }

    [Fact]
    public void Step_WrapsPositionAcrossWorldEdge()
    {
        var world = CreateWorld(new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            TargetSpeed = 2f,
            MaxForce = 1f,
            FieldOfView = 360f,
            SenseRadius = 5f,
            WorldWidth = 100f,
            WorldHeight = 100f
        });
        Assert.True(world.TryAddBoid(new Vec2(99.5f, 20f), new Vec2(1f, 0f)));

        world.Step(0.5f);

        Assert.Equal(1, world.Count);
        Assert.Equal(0.5f, world.Boids[0].Position.X, 3);
        Assert.Equal(20f, world.Boids[0].Position.Y, 4);
        Assert.InRange(world.Boids[0].Velocity.Length, 1.999f, 2.001f);
    }

}
