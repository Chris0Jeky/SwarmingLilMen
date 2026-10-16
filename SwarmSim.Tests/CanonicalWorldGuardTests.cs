using System;
using SwarmSim.Core.Canonical;
using Xunit;

namespace SwarmSim.Tests;

public sealed class CanonicalWorldGuardTests
{
    [Fact]
    public void CanonicalWorld_SetVelocity_OutOfRangeIsNoOpAndValidRenormalizes()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 1,
            TargetSpeed = 10f,
            MaxForce = 1f,
            SenseRadius = 50f,
            SeparationRadius = 20f,
            FieldOfView = 360f,
            WorldWidth = 100f,
            WorldHeight = 100f,
        };
        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        Assert.True(world.TryAddBoid(new Vec2(5f, 5f), new Vec2(1f, 0f)));
        Vec2 originalPosition = world.Boids[0].Position;
        Vec2 originalVelocity = world.Boids[0].Velocity;

        world.SetVelocity(-1, new Vec2(9f, 9f));
        world.SetVelocity(5, new Vec2(-4f, 7f));

        Assert.Equal(originalPosition.X, world.Boids[0].Position.X);
        Assert.Equal(originalPosition.Y, world.Boids[0].Position.Y);
        Assert.Equal(originalVelocity.X, world.Boids[0].Velocity.X);
        Assert.Equal(originalVelocity.Y, world.Boids[0].Velocity.Y);
        Assert.Equal(0ul, world.TickCount);

        world.SetVelocity(0, new Vec2(3f, 4f));

        Assert.Equal(6f, world.Boids[0].Velocity.X, 4);
        Assert.Equal(8f, world.Boids[0].Velocity.Y, 4);
        Assert.Equal(10f, world.Boids[0].Velocity.Length, 4);
        Assert.Equal(originalPosition.X, world.Boids[0].Position.X);
        Assert.Equal(originalPosition.Y, world.Boids[0].Position.Y);
        Assert.Equal(0ul, world.TickCount);
    }

    [Fact]
    public void CanonicalWorld_AddRule_NullThrows()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            TargetSpeed = 2f,
            MaxForce = 1f,
            FieldOfView = 360f,
            SenseRadius = 5f
        };

        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));

        Assert.Throws<ArgumentNullException>(() => world.AddRule(null!));
    }

    [Fact]
    public void CanonicalWorld_QueryVisibleNeighbors_OutOfRangeReturnsEmpty()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            TargetSpeed = 2f,
            MaxForce = 1f,
            FieldOfView = 360f,
            SenseRadius = 5f
        };

        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        world.TryAddBoid(Vec2.Zero, new Vec2(1f, 0f));

        Span<int> buffer = stackalloc int[8];
        Span<float> weights = stackalloc float[8];

        SpatialQueryResult below = world.QueryVisibleNeighbors(-1, buffer, weights);
        Assert.Equal(0, below.Count);
        Assert.False(below.IsTruncated);

        SpatialQueryResult above = world.QueryVisibleNeighbors(world.Count, buffer, weights);
        Assert.Equal(0, above.Count);
        Assert.False(above.IsTruncated);
    }
}
