using SwarmSim.Core.Canonical;
using Xunit;

namespace SwarmSim.Tests;

public sealed class CanonicalIngestWrapTests
{
    [Fact]
    public void TryAddBoid_WrapsPositionAndDefaultsZeroVelocity()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            TargetSpeed = 10f,
            MaxForce = 2.5f,
            SenseRadius = 18f,
            FieldOfView = 270f,
            MaxNeighbors = 32,
            SeparationRadius = 8f,
            SeparationWeight = 1.5f,
            AlignmentWeight = 1f,
            CohesionWeight = 1f,
            WanderStrength = 0f,
            WorldWidth = 1000f,
            WorldHeight = 1000f,
            Seed = 1u
        };
        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));

        Assert.True(world.TryAddBoid(new Vec2(-5f, -5f), new Vec2(0f, 0f)));

        Assert.InRange(world.Boids[0].Position.X, 0f, 1000f);
        Assert.InRange(world.Boids[0].Position.Y, 0f, 1000f);
        Assert.Equal(995f, world.Boids[0].Position.X, 3);
        Assert.Equal(995f, world.Boids[0].Position.Y, 3);
        Assert.Equal(settings.TargetSpeed, world.Boids[0].Velocity.Length, 4);
    }
}
