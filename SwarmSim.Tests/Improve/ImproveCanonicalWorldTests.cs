using System;
using SwarmSim.Core.Canonical;
using Xunit;

namespace SwarmSim.Tests;

public class ImproveCanonicalWorldTests
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
    public void Ctor_NullSettings_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CanonicalWorld(null!, new GridSpatialIndex(10f, 1920f, 1080f)));
    }

    [Fact]
    public void Ctor_NullSpatialIndex_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CanonicalWorld(new CanonicalWorldSettings(), null!));
    }

    [Fact]
    public void TryAddBoid_ZeroVelocity_DefaultsToTargetSpeedAlongPositiveX()
    {
        var world = CreateWorld(CreateSettings());

        bool added = world.TryAddBoid(new Vec2(10f, 10f), Vec2.Zero);

        Assert.True(added);
        Assert.Equal(1, world.Count);
        Vec2 velocity = world.Boids[0].Velocity;
        Assert.InRange(velocity.Length, 1.999f, 2.001f);
        Assert.Equal(0f, velocity.Y);
        Assert.True(velocity.X > 0f);
    }

    [Fact]
    public void SetVelocity_OutOfRangeIndex_LeavesWorldUnchanged()
    {
        var world = CreateWorld(CreateSettings());
        Assert.True(world.TryAddBoid(new Vec2(1f, 1f), new Vec2(1f, 0f)));
        Vec2 before = world.Boids[0].Velocity;

        world.SetVelocity(-1, new Vec2(0f, 1f));
        world.SetVelocity(world.Count, new Vec2(0f, 1f));
        world.SetVelocity(100, new Vec2(0f, 1f));

        Assert.Equal(1, world.Count);
        Assert.Equal(before.X, world.Boids[0].Velocity.X);
        Assert.Equal(before.Y, world.Boids[0].Velocity.Y);
    }

    [Fact]
    public void SetVelocity_OverlongVelocity_RenormalizesToTargetSpeed()
    {
        var world = CreateWorld(CreateSettings());
        Assert.True(world.TryAddBoid(new Vec2(1f, 1f), new Vec2(1f, 0f)));

        world.SetVelocity(0, new Vec2(10f, 4f));

        Vec2 velocity = world.Boids[0].Velocity;
        Assert.InRange(velocity.Length, 1.999f, 2.001f);
        Assert.True(velocity.X > 0f);
        Assert.True(velocity.Y > 0f);
    }

    [Fact]
    public void Step_EmptyWorld_IncrementsTickAndReportsZeroAgents()
    {
        var world = CreateWorld(CreateSettings());

        world.Step(1f / 60f);

        Assert.Equal(0, world.Count);
        Assert.Equal((ulong)1, world.TickCount);
        Assert.Equal(0, world.Boids.Length);
        CanonicalWorld.PerceptionSnapshot snapshot = world.CapturePerceptionSnapshot();
        Assert.Equal((ulong)1, snapshot.TickCount);
        Assert.Equal(0, snapshot.AgentCount);
        Assert.Empty(snapshot.NearestDistances);
        Assert.Empty(snapshot.NearestAngles);
        Assert.Empty(snapshot.WhiskerCounts);
    }

    [Fact]
    public void QueryVisibleNeighbors_InvalidIndex_ReturnsEmptyWithoutThrowing()
    {
        var world = CreateWorld(CreateSettings());
        Assert.True(world.TryAddBoid(new Vec2(1f, 1f), new Vec2(1f, 0f)));
        var buffer = new int[4];
        var weights = new float[4];

        SpatialQueryResult below = world.QueryVisibleNeighbors(-1, buffer, weights);
        SpatialQueryResult atCount = world.QueryVisibleNeighbors(world.Count, buffer, weights);

        Assert.Equal(0, below.Count);
        Assert.False(below.IsTruncated);
        Assert.Equal(0, atCount.Count);
        Assert.False(atCount.IsTruncated);
    }

    [Fact]
    public void EffectiveMaxNeighbors_ClampsSmallMaxNeighborsToFour()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            MaxNeighbors = 1
        };

        var world = CreateWorld(settings);

        Assert.Equal(4, world.EffectiveMaxNeighbors);
    }
}
