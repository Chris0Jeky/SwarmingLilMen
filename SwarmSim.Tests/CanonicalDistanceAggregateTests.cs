using SwarmSim.Core.Canonical;

namespace SwarmSim.Tests;

public sealed class CanonicalDistanceAggregateTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (int exponent in new[] { 0, 126 })
            foreach (int count in new[] { 3, 5 })
                foreach (bool grid in new[] { false, true })
                    foreach (bool compatibilityCapture in new[] { false, true })
                        yield return [exponent, count, grid, compatibilityCapture];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void DistanceAverageUsesTheCurrentTickDespiteOverflow(
        int exponent, int count, bool grid, bool compatibilityCapture)
    {
        float distance = MathF.ScaleB(1f, exponent);
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = count,
            WorldWidth = 2f * distance,
            WorldHeight = 2f * distance,
            SenseRadius = distance,
            FieldOfView = 360f,
            TargetSpeed = distance * 0.5f,
            FixedDeltaTime = 1f,
            WanderStrength = 0f
        };
        ISpatialIndex index = grid
            ? new GridSpatialIndex(distance, settings.WorldWidth, settings.WorldHeight)
            : new NaiveSpatialIndex(settings.WorldWidth, settings.WorldHeight);
        var world = new CanonicalWorld(settings, index,
            new CanonicalPipeline([new ObserveOnlyPolicy()], new PriorityForceBudgetResolver()));

        Assert.True(world.TryAddBoid(Vec2.Zero, new Vec2(1f, 0f)));
        for (int i = 1; i < count; i++)
            Assert.True(world.TryAddBoid(new Vec2(distance, 0f), new Vec2(-1f, 0f)));

        // There are 2*(count-1) directed nonzero distances among count*(count-1)
        // samples. The exact mean is therefore 2*distance/count, regardless of index.
        // At exponent 126, count 3 overflows only the world total; count 5 also
        // overflows the first observer's local sum. Every individual distance is finite.
        float separatedMean = (2f / count) * distance;
        var telemetry = new CanonicalTelemetry(count);
        for (int tick = 1; tick <= 3; tick++)
        {
            world.Step(settings.FixedDeltaTime);
            world.CaptureTelemetryInto(telemetry);
            var compatibility = world.CapturePerceptionSnapshot();
            float actualMean = compatibilityCapture
                ? compatibility.AverageNeighborDistance : telemetry.AverageNeighborDistance;
            float actualMin = compatibilityCapture
                ? compatibility.MinNeighborDistance : telemetry.MinNeighborDistance;
            float actualMax = compatibilityCapture
                ? compatibility.MaxNeighborDistance : telemetry.MaxNeighborDistance;
            var neighborCounts = compatibilityCapture
                ? compatibility.NeighborCountStats : telemetry.NeighborCountStats;

            // The first step brings every boid to the same exactly representable position;
            // the second step separates the two groups again. This checks that an overflowed
            // total is neither carried into the zero-distance tick nor lost on the next tick.
            bool coincidentInputs = tick == 2;
            Assert.Equal(coincidentInputs ? 0f : separatedMean, actualMean);
            Assert.Equal(0f, actualMin);
            Assert.Equal(coincidentInputs ? 0f : distance, actualMax);
            Assert.Equal((count - 1, count - 1, (float)(count - 1)), neighborCounts);
            Assert.Equal((ulong)tick, compatibilityCapture ? compatibility.TickCount : telemetry.TickCount);
            Assert.Equal(count, compatibilityCapture ? compatibility.AgentCount : telemetry.AgentCount);
            Assert.Equal(0, telemetry.TruncatedCount);

            if (tick == 1)
                for (int i = 0; i < count; i++)
                    Assert.Equal(distance * 0.5f, world.Boids[i].Position.X);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void OrdinaryMeanRetainsItsTwoLevelFloatRounding(bool grid, bool compatibilityCapture)
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            WorldWidth = 67108864f,
            WorldHeight = 64f,
            SenseRadius = 33554432f,
            FieldOfView = 360f,
            TargetSpeed = 1f
        };
        ISpatialIndex index = grid
            ? new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight)
            : new NaiveSpatialIndex(settings.WorldWidth, settings.WorldHeight);
        var world = new CanonicalWorld(settings, index,
            new CanonicalPipeline([new ObserveOnlyPolicy()], new PriorityForceBudgetResolver()));
        foreach (float x in new[] { 0f, 1f, 2f, 16777220f })
            Assert.True(world.TryAddBoid(new Vec2(x, 0f), new Vec2(1f, 0f)));

        world.Step(settings.FixedDeltaTime);
        var telemetry = new CanonicalTelemetry(world.Count);
        world.CaptureTelemetryInto(telemetry);
        float actual = compatibilityCapture
            ? world.CapturePerceptionSnapshot().AverageNeighborDistance : telemetry.AverageNeighborDistance;

        // This non-overflowing layout pins the established local-then-world float result.
        // Summing its distance samples in double throughout instead would yield 8388610f.
        Assert.Equal(0x4B000003, BitConverter.SingleToInt32Bits(actual)); // 8388611f
    }

    private sealed class ObserveOnlyPolicy : ILocalSteeringPolicy
    {
        public PolicyMetadata Metadata { get; } = new("aggregate-observer");
        public int MaxIntents => 0;
        public void Emit(in LocalObservation observation, in LocalPolicyContext context,
            ref IntentWriter intents) { }
    }
}
