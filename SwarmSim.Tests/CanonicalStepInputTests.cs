using System;
using SwarmSim.Core.Canonical;
using Xunit;

namespace SwarmSim.Tests;

public class CanonicalStepInputTests
{
    private static CanonicalWorld CreateWorldWithTwoBoids()
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
        world.TryAddBoid(new Vec2(2f, 0f), new Vec2(0f, 1f));
        return world;
    }

    [Fact]
    [Trait("Category", "InputValidationCanonicalStep")]
    public void Step_NaN_ThrowsArgumentOutOfRange()
    {
        var world = CreateWorldWithTwoBoids();
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Step(float.NaN));
    }

    [Fact]
    [Trait("Category", "InputValidationCanonicalStep")]
    public void Step_PositiveInfinity_ThrowsArgumentOutOfRange()
    {
        var world = CreateWorldWithTwoBoids();
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Step(float.PositiveInfinity));
    }

    [Fact]
    [Trait("Category", "InputValidationCanonicalStep")]
    public void Step_Zero_ThrowsArgumentOutOfRange()
    {
        var world = CreateWorldWithTwoBoids();
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Step(0f));
    }

    [Fact]
    [Trait("Category", "InputValidationCanonicalStep")]
    public void Step_Negative_ThrowsArgumentOutOfRange()
    {
        var world = CreateWorldWithTwoBoids();
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Step(-1f));
    }

    [Fact]
    [Trait("Category", "InputValidationCanonicalStep")]
    public void Step_RejectedNaN_LeavesBoidsFiniteAndUnchanged()
    {
        var world = CreateWorldWithTwoBoids();
        var before0Pos = world.Boids[0].Position;
        var before0Vel = world.Boids[0].Velocity;
        var before1Pos = world.Boids[1].Position;
        var before1Vel = world.Boids[1].Velocity;

        Assert.Throws<ArgumentOutOfRangeException>(() => world.Step(float.NaN));

        Assert.Equal(2, world.Count);
        var after0 = world.Boids[0];
        var after1 = world.Boids[1];

        Assert.True(float.IsFinite(after0.Position.X));
        Assert.True(float.IsFinite(after0.Position.Y));
        Assert.True(float.IsFinite(after0.Velocity.X));
        Assert.True(float.IsFinite(after0.Velocity.Y));
        Assert.True(float.IsFinite(after1.Position.X));
        Assert.True(float.IsFinite(after1.Position.Y));
        Assert.True(float.IsFinite(after1.Velocity.X));
        Assert.True(float.IsFinite(after1.Velocity.Y));

        Assert.Equal(before0Pos.X, after0.Position.X);
        Assert.Equal(before0Pos.Y, after0.Position.Y);
        Assert.Equal(before0Vel.X, after0.Velocity.X);
        Assert.Equal(before0Vel.Y, after0.Velocity.Y);
        Assert.Equal(before1Pos.X, after1.Position.X);
        Assert.Equal(before1Pos.Y, after1.Position.Y);
        Assert.Equal(before1Vel.X, after1.Velocity.X);
        Assert.Equal(before1Vel.Y, after1.Velocity.Y);
    }

    [Fact]
    [Trait("Category", "InputValidationCanonicalStep")]
    public void Step_ValidDt_StillWorks()
    {
        var world = CreateWorldWithTwoBoids();
        world.Step(1f / 60f);

        Assert.Equal(2, world.Count);
        foreach (var boid in world.Boids)
        {
            Assert.True(float.IsFinite(boid.Position.X));
            Assert.True(float.IsFinite(boid.Position.Y));
            Assert.True(float.IsFinite(boid.Velocity.X));
            Assert.True(float.IsFinite(boid.Velocity.Y));
        }
    }
}
