using System;
using SwarmSim.Core.Canonical;
using Xunit;

namespace SwarmSim.Tests;

public class CanonicalWorldGuardTests
{
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
