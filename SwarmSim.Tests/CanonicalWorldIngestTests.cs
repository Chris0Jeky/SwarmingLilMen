using SwarmSim.Core.Canonical;
using Xunit;

namespace SwarmSim.Tests;

public class CanonicalWorldIngestTests
{
    [Fact]
    public void TryAddBoid_NormalizesSpeedAndWrapsPosition()
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
            WorldWidth = 100f,
            WorldHeight = 100f,
            Seed = 1u
        };

        CanonicalWorld world = CreateWorld(settings);

        Assert.True(world.TryAddBoid(new Vec2(103f, -2f), new Vec2(100f, 0f)));

        Assert.Equal(10f, world.Boids[0].Velocity.Length, 4);
        Assert.InRange(world.Boids[0].Position.X, 0f, 100f);
        Assert.InRange(world.Boids[0].Position.Y, 0f, 100f);
        Assert.Equal(3f, world.Boids[0].Position.X, 3);
        Assert.Equal(98f, world.Boids[0].Position.Y, 3);
    }

    private static CanonicalWorld CreateWorld(CanonicalWorldSettings settings) =>
        new(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
}
