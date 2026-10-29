using SwarmSim.Core.Canonical;

namespace SwarmSim.Tests;

public sealed class CanonicalExtremeDistanceTests
{
    public static IEnumerable<object[]> DistanceCases()
    {
        // 80 overflows squared float intermediates; -75 yields rounded nonzero subnormals;
        // -90 squares to zero. Power-of-two scaling makes the 3-4-5 distance exact in float.
        foreach (int exponent in new[] { 0, 80, -75, -90 })
            foreach (bool grid in new[] { false, true })
                foreach (bool seam in new[] { false, true })
                    yield return [exponent, grid, seam];
    }

    [Theory]
    [MemberData(nameof(DistanceCases))]
    public void ScaledThreeFourFiveNeighborhoodHasExactRepresentableDistance(int exponent, bool grid, bool seam)
    {
        float scale = MathF.ScaleB(1f, exponent);
        var settings = Settings(scale);
        var recorder = new DistanceRecorder();
        var world = new CanonicalWorld(settings, Index(settings, grid),
            new CanonicalPipeline([recorder], new PriorityForceBudgetResolver()));
        AddPair(world, scale, seam);

        world.Step(settings.FixedDeltaTime);
        var telemetry = new CanonicalTelemetry(2);
        world.CaptureTelemetryInto(telemetry);

        float expected = 5f * scale;
        Assert.Equal(2, recorder.Calls);
        Assert.Equal(expected, recorder.FirstDistance);
        Assert.Equal(expected, recorder.SecondDistance);
        Assert.Equal(new[] { expected, expected }, telemetry.NearestDistances.ToArray());
        Assert.Equal(expected, telemetry.AverageNeighborDistance);
        Assert.Equal(expected, telemetry.MinNeighborDistance);
        Assert.Equal(expected, telemetry.MaxNeighborDistance);
        Assert.Equal((1, 1, 1f), telemetry.NeighborCountStats);

        if (exponent >= 0)
        {
            // atan2(4,3) and its opposite, with the ordinary float trig rounding band.
            Assert.InRange(telemetry.NearestAngles[0], 53.1299f, 53.1303f);
            Assert.InRange(telemetry.NearestAngles[1], 126.8697f, 126.8701f);
        }
        else
        {
            // The established geometric epsilon still treats these tiny directions as nearly
            // zero; correcting distance does not silently change the direction model's cutoff.
            Assert.Equal(new[] { 0f, 0f }, telemetry.NearestAngles.ToArray());
        }
    }

    [Theory]
    [InlineData(80, false)]
    [InlineData(80, true)]
    [InlineData(-90, false)]
    [InlineData(-90, true)]
    public void DistantVisibleNeighborDoesNotFalselyEnterSeparationPriority(int exponent, bool grid)
    {
        float scale = MathF.ScaleB(1f, exponent);
        var settings = Settings(scale);
        var world = new CanonicalWorld(settings, Index(settings, grid));
        AddPair(world, scale, seam: false);

        world.Step(settings.FixedDeltaTime);
        var telemetry = new CanonicalTelemetry(2);
        world.CaptureTelemetryInto(telemetry);

        // True distance is 5*scale. The default priority entry radius is only 1.6*scale.
        // Squared overflow/underflow used to report nearest distance 0 and activate speed droop.
        Assert.Equal((1, 1, 1f), telemetry.NeighborCountStats);
        Assert.False(telemetry.SeparationPriorityTriggered);
        Assert.Equal(5f * scale, telemetry.NearestDistances[0]);
        Assert.InRange(world.Boids[0].Velocity.Length, 0.99999f, 1.00001f);
        Assert.InRange(world.Boids[1].Velocity.Length, 0.99999f, 1.00001f);
    }

    [Theory]
    [MemberData(nameof(DistanceCases))]
    public void EqualExtremeDistancesKeepTheFirstCandidateDirection(int exponent, bool grid, bool seam)
    {
        float scale = MathF.ScaleB(1f, exponent);
        var settings = Settings(scale);
        var recorder = new NearestDirectionRecorder();
        var world = new CanonicalWorld(settings, Index(settings, grid),
            new CanonicalPipeline([], new PriorityForceBudgetResolver(), [recorder]));
        var origin = seam ? new Vec2(15f * scale, 15f * scale) : new Vec2(8f * scale, 8f * scale);
        var first = seam ? new Vec2(2f * scale, 3f * scale) : new Vec2(11f * scale, 12f * scale);
        var second = seam ? new Vec2(12f * scale, 11f * scale) : new Vec2(5f * scale, 4f * scale);
        foreach (Vec2 position in new[] { origin, first, second })
            Assert.True(world.TryAddBoid(position, new Vec2(1f, 0f)));

        world.Step(settings.FixedDeltaTime);
        var telemetry = new CanonicalTelemetry(3);
        world.CaptureTelemetryInto(telemetry);

        Assert.Equal(3, recorder.Calls);
        Assert.Equal(3f * scale, recorder.FirstDelta.X);
        Assert.Equal(4f * scale, recorder.FirstDelta.Y);
        Assert.Equal(5f * scale, telemetry.NearestDistances[0]);
        Assert.Equal(2, world.Instrumentation.NeighborCounts[0]);
    }

    private static CanonicalWorldSettings Settings(float scale) => new()
    {
        InitialCapacity = 4, WorldWidth = 16f * scale, WorldHeight = 16f * scale,
        SenseRadius = 8f * scale, FieldOfView = 360f, TargetSpeed = 1f, WanderStrength = 0f
    };

    private static ISpatialIndex Index(CanonicalWorldSettings settings, bool grid) => grid
        ? new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight)
        : new NaiveSpatialIndex(settings.WorldWidth, settings.WorldHeight);

    private static void AddPair(CanonicalWorld world, float scale, bool seam)
    {
        var first = seam ? new Vec2(15f * scale, 15f * scale) : Vec2.Zero;
        var second = seam ? new Vec2(2f * scale, 3f * scale) : new Vec2(3f * scale, 4f * scale);
        Assert.True(world.TryAddBoid(first, new Vec2(1f, 0f)));
        Assert.True(world.TryAddBoid(second, new Vec2(1f, 0f)));
    }

    private sealed class DistanceRecorder : ILocalSteeringPolicy
    {
        public PolicyMetadata Metadata { get; } = new("distance-recorder");
        public int MaxIntents => 0;
        public int Calls { get; private set; }
        public float FirstDistance { get; private set; }
        public float SecondDistance { get; private set; }
        public void Emit(in LocalObservation observation, in LocalPolicyContext context, ref IntentWriter intents)
        {
            Assert.Equal(1, observation.Neighbors.Length);
            if (Calls++ == 0) FirstDistance = observation.Neighbors[0].Distance;
            else SecondDistance = observation.Neighbors[0].Distance;
        }
    }

    private sealed class NearestDirectionRecorder : ICanonicalVelocityStage
    {
        public string Id => "nearest-direction-recorder";
        public int Calls { get; private set; }
        public Vec2 FirstDelta { get; private set; }
        public Vec2 Apply(in VelocityStageContext context, Vec2 velocity)
        {
            if (Calls++ == 0) FirstDelta = context.NearestDelta;
            return velocity;
        }
    }
}
