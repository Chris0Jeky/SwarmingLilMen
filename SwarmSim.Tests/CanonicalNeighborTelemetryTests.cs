using SwarmSim.Core.Canonical;

namespace SwarmSim.Tests;

public sealed class CanonicalNeighborTelemetryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PeriodicDistanceTelemetryKeepsTheFirstEqualDistanceNeighbor(bool grid)
    {
        var settings = Settings(senseRadius: 60f);
        var world = Create(settings, grid);
        // At the seam, indices 2 and 3 are equally close to index 0 but lie on opposite sides.
        // Index 1 also exercises the exact half-world displacement in both directions.
        foreach (float x in new[] { 0f, 50f, 99f, 1f })
            Assert.True(world.TryAddBoid(new Vec2(x, 0f), new Vec2(1f, -0f)));

        world.Step(settings.FixedDeltaTime);
        var telemetry = new CanonicalTelemetry(world.Count);
        world.CaptureTelemetryInto(telemetry);

        Assert.Equal(new[] { 1f, 49f, 1f, 1f }, telemetry.NearestDistances.ToArray());
        Assert.Equal(new[] { 180f, 0f, 0f, 180f }, telemetry.NearestAngles.ToArray());
        Assert.Equal(304f / 12f, telemetry.AverageNeighborDistance);
        Assert.Equal(1f, telemetry.MinNeighborDistance);
        Assert.Equal(50f, telemetry.MaxNeighborDistance);
        Assert.Equal((3, 3, 3f), telemetry.NeighborCountStats);
        Assert.Equal(0, telemetry.TruncatedCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnObservedBoidWithoutNeighborsKeepsItsExistingSentinel(bool grid)
    {
        var settings = Settings(senseRadius: 1f);
        var world = Create(settings, grid);
        Assert.True(world.TryAddBoid(new Vec2(10f, 10f), new Vec2(1f, 0f)));
        Assert.True(world.TryAddBoid(new Vec2(50f, 50f), new Vec2(1f, 0f)));

        world.Step(settings.FixedDeltaTime);
        var telemetry = new CanonicalTelemetry(world.Count);
        world.CaptureTelemetryInto(telemetry);

        Assert.Equal(new[] { float.MaxValue, float.MaxValue }, telemetry.NearestDistances.ToArray());
        Assert.Equal(new[] { 0f, 0f }, telemetry.NearestAngles.ToArray());
        Assert.Equal(0f, telemetry.AverageNeighborDistance);
        Assert.Equal(0f, telemetry.MinNeighborDistance);
        Assert.Equal(0f, telemetry.MaxNeighborDistance);
        Assert.Equal((0, 0, 0f), telemetry.NeighborCountStats);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FiniteExtremeCoordinatesPreserveExistingOverflowDiagnostics(bool grid)
    {
        var settings = Settings(senseRadius: 1e25f, worldWidth: 1e25f);
        var world = Create(settings, grid);
        Assert.True(world.TryAddBoid(new Vec2(0f, 0f), new Vec2(1f, 0f)));
        Assert.True(world.TryAddBoid(new Vec2(5e24f, 0f), new Vec2(1f, 0f)));

        world.Step(settings.FixedDeltaTime);
        var telemetry = new CanonicalTelemetry(world.Count);
        world.CaptureTelemetryInto(telemetry);

        // The query compares squared distances in double for this radius, while the existing
        // float telemetry distance overflows. This optimization must not silently redefine it.
        Assert.Equal((1, 1, 1f), telemetry.NeighborCountStats);
        Assert.Equal(new[] { 0f, 0f }, telemetry.NearestDistances.ToArray());
        Assert.Equal(new[] { 0f, 0f }, telemetry.NearestAngles.ToArray());
        Assert.Equal(float.PositiveInfinity, telemetry.AverageNeighborDistance);
        Assert.Equal(0f, telemetry.MinNeighborDistance);
        Assert.Equal(float.PositiveInfinity, telemetry.MaxNeighborDistance);
    }

    private static CanonicalWorldSettings Settings(float senseRadius, float worldWidth = 100f) => new()
    {
        InitialCapacity = 8, WorldWidth = worldWidth, WorldHeight = 100f, SenseRadius = senseRadius,
        FieldOfView = 360f, TargetSpeed = 1f, WanderStrength = 0f
    };

    private static CanonicalWorld Create(CanonicalWorldSettings settings, bool grid)
    {
        ISpatialIndex index = grid
            ? new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight)
            : new NaiveSpatialIndex(settings.WorldWidth, settings.WorldHeight);
        return new CanonicalWorld(settings, index,
            new CanonicalPipeline([new ObserveOnlyPolicy()], new PriorityForceBudgetResolver()));
    }

    private sealed class ObserveOnlyPolicy : ILocalSteeringPolicy
    {
        public PolicyMetadata Metadata { get; } = new("observe-only");
        public int MaxIntents => 0;
        public void Emit(in LocalObservation observation, in LocalPolicyContext context,
            ref IntentWriter intents) { }
    }
}
