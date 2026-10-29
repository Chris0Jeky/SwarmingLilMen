using SwarmSim.Benchmarks;
using SwarmSim.Core.Canonical;

namespace SwarmSim.Tests;

public sealed class CanonicalTelemetryTests
{
    [Fact]
    public void StepWithoutTelemetryDoesNotAllocateOverOneThousandWarmTicks()
    {
        var world = CanonicalExperiment.CreateWorld(CanonicalExperiment.Workload.Mixed, 7919, 256, grid: true);
        for (int tick = 0; tick < 64; tick++)
            world.Step(world.Settings.FixedDeltaTime);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int tick = 0; tick < 1_000; tick++)
            world.Step(world.Settings.FixedDeltaTime);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(1_064ul, world.TickCount);
        Assert.Equal(256, world.Count);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void StepAndExplicitReusableCaptureDoNotAllocateOverOneThousandWarmTicks()
    {
        var world = CanonicalExperiment.CreateWorld(CanonicalExperiment.Workload.Mixed, 7919, 256, grid: true);
        var telemetry = new CanonicalTelemetry(world.Count);
        for (int tick = 0; tick < 64; tick++)
        {
            world.Step(world.Settings.FixedDeltaTime);
            world.CaptureTelemetryInto(telemetry);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int tick = 0; tick < 1_000; tick++)
        {
            world.Step(world.Settings.FixedDeltaTime);
            world.CaptureTelemetryInto(telemetry);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(world.TickCount, telemetry.TickCount);
        Assert.Equal(256, telemetry.AgentCount);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void CaptureCopiesLastTickDataIntoTheCallerBuffer()
    {
        var world = CreateTwoBoidWorld();
        var telemetry = new CanonicalTelemetry(8);

        world.Step(world.Settings.FixedDeltaTime);
        world.CaptureTelemetryInto(telemetry);

        Assert.Equal(1ul, telemetry.TickCount);
        Assert.Equal(2, telemetry.AgentCount);
        Assert.Equal(8, telemetry.Capacity);
        Assert.Equal((1, 1, 1f), telemetry.NeighborCountStats);
        Assert.Equal(1f, telemetry.AverageNeighborCount);
        Assert.Equal(1f, telemetry.AverageNeighborWeight);
        Assert.Equal(2f, telemetry.AverageNeighborDistance);
        Assert.Equal(2f, telemetry.MinNeighborDistance);
        Assert.Equal(2f, telemetry.MaxNeighborDistance);
        Assert.True(telemetry.SeparationPriorityTriggered);
        Assert.Equal(new[] { 2f, 2f }, telemetry.NearestDistances.ToArray());
        Assert.Equal(new[] { 0f, 180f }, telemetry.NearestAngles.ToArray());
        Assert.Equal(new[] { 1, 0 }, telemetry.WhiskerCounts.ToArray());
        Assert.Equal(new[] { false, false }, telemetry.QueryTruncated.ToArray());
        Assert.Equal(0, telemetry.TruncatedCount);

        var compatibility = world.CapturePerceptionSnapshot();
        Assert.Equal(compatibility.AverageSeparationMagnitude, telemetry.AverageSeparationMagnitude);
        Assert.Equal(compatibility.AverageAlignmentMagnitude, telemetry.AverageAlignmentMagnitude);
        Assert.Equal(compatibility.AverageCohesionMagnitude, telemetry.AverageCohesionMagnitude);

        // Advancing the world cannot mutate a capture retained by its caller.
        world.Step(world.Settings.FixedDeltaTime);
        Assert.Equal(1ul, telemetry.TickCount);
        Assert.Equal(new[] { 2f, 2f }, telemetry.NearestDistances.ToArray());
        Assert.Equal(new[] { 2f, 2f }, compatibility.NearestDistances);

        // Reusing this destination deliberately replaces its old contents.
        world.CaptureTelemetryInto(telemetry);
        Assert.Equal(2ul, telemetry.TickCount);
    }

    [Fact]
    public void CaptureKeepsLastTickAgentCountWhenABoidIsAddedBetweenTicks()
    {
        var world = CreateTwoBoidWorld();
        world.Step(world.Settings.FixedDeltaTime);
        Assert.True(world.TryAddBoid(new Vec2(70, 70), new Vec2(1, 0)));

        var telemetry = new CanonicalTelemetry(2);
        world.CaptureTelemetryInto(telemetry);
        var compatibility = world.CapturePerceptionSnapshot();

        Assert.Equal(3, world.Count);
        Assert.Equal(2, telemetry.AgentCount);
        Assert.Equal(2, telemetry.NearestDistances.Length);
        Assert.Equal(2, telemetry.QueryTruncated.Length);
        Assert.Equal(2, compatibility.AgentCount);
        Assert.Equal(2, compatibility.NearestDistances.Length);

        world.Step(world.Settings.FixedDeltaTime);
        Assert.Throws<ArgumentException>(() => world.CaptureTelemetryInto(telemetry));
        Assert.Equal(1ul, telemetry.TickCount);
        Assert.Equal(2, telemetry.AgentCount);
        Assert.Equal(new[] { 2f, 2f }, telemetry.NearestDistances.ToArray());
    }

    [Fact]
    public void CaptureBeforeTheFirstStepClearsAReusedDestinationToAnEmptyTick()
    {
        var populated = CreateTwoBoidWorld();
        var telemetry = new CanonicalTelemetry(2);
        populated.Step(populated.Settings.FixedDeltaTime);
        populated.CaptureTelemetryInto(telemetry);

        var unstepped = CreateTwoBoidWorld();
        unstepped.CaptureTelemetryInto(telemetry);

        Assert.Equal(0ul, telemetry.TickCount);
        Assert.Equal(0, telemetry.AgentCount);
        Assert.True(telemetry.NearestDistances.IsEmpty);
        Assert.True(telemetry.NearestAngles.IsEmpty);
        Assert.True(telemetry.WhiskerCounts.IsEmpty);
        Assert.True(telemetry.QueryTruncated.IsEmpty);
        Assert.Equal((0, 0, 0f), telemetry.NeighborCountStats);
        Assert.Equal(0f, telemetry.AverageNeighborCount);
        Assert.Equal(0f, telemetry.AverageNeighborDistance);
        Assert.Equal(0f, telemetry.MinNeighborDistance);
        Assert.Equal(0f, telemetry.MaxNeighborDistance);
        Assert.Equal(0f, telemetry.AverageNeighborWeight);
        Assert.Equal(0f, telemetry.AverageSeparationMagnitude);
        Assert.Equal(0f, telemetry.AverageAlignmentMagnitude);
        Assert.Equal(0f, telemetry.AverageCohesionMagnitude);
        Assert.False(telemetry.SeparationPriorityTriggered);
        Assert.Equal(0, telemetry.TruncatedCount);
    }

    [Fact]
    public void CaptureRejectsNullAndUndersizedDestinations()
    {
        var world = CreateTwoBoidWorld();
        world.Step(world.Settings.FixedDeltaTime);

        Assert.Throws<ArgumentNullException>(() => world.CaptureTelemetryInto(null!));
        Assert.Throws<ArgumentException>(() => world.CaptureTelemetryInto(new CanonicalTelemetry(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CanonicalTelemetry(-1));
    }

    [Theory]
    [InlineData(0, 1701u)]
    [InlineData(1, 1701u)]
    [InlineData(2, 1701u)]
    [InlineData(3, 7919u)]
    public void CapturingTelemetryDoesNotChangeAnyTrajectoryFrame(int workload, uint seed)
    {
        var scenario = (CanonicalExperiment.Workload)workload;
        var withoutCapture = CanonicalExperiment.CreateWorld(scenario, seed, 96, grid: true);
        var withCapture = CanonicalExperiment.CreateWorld(scenario, seed, 96, grid: true);
        var telemetry = new CanonicalTelemetry(96);

        for (int tick = 0; tick < 128; tick++)
        {
            withoutCapture.Step(withoutCapture.Settings.FixedDeltaTime);
            withCapture.Step(withCapture.Settings.FixedDeltaTime);
            withCapture.CaptureTelemetryInto(telemetry);

            Assert.Equal(withoutCapture.TickCount, telemetry.TickCount);
            for (int i = 0; i < withoutCapture.Count; i++)
            {
                var expected = withoutCapture.Boids[i];
                var actual = withCapture.Boids[i];
                Assert.Equal(BitConverter.SingleToInt32Bits(expected.Position.X), BitConverter.SingleToInt32Bits(actual.Position.X));
                Assert.Equal(BitConverter.SingleToInt32Bits(expected.Position.Y), BitConverter.SingleToInt32Bits(actual.Position.Y));
                Assert.Equal(BitConverter.SingleToInt32Bits(expected.Velocity.X), BitConverter.SingleToInt32Bits(actual.Velocity.X));
                Assert.Equal(BitConverter.SingleToInt32Bits(expected.Velocity.Y), BitConverter.SingleToInt32Bits(actual.Velocity.Y));
                Assert.Equal(expected.Group, actual.Group);
            }
        }

        // The first explicit capture of the previously unobserved world must also agree.
        CanonicalExperiment.AssertSameState(withoutCapture, withCapture);
    }

    private static CanonicalWorld CreateTwoBoidWorld()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 8,
            WorldWidth = 100,
            WorldHeight = 100,
            TargetSpeed = 5,
            MaxForce = 1,
            SenseRadius = 20,
            SeparationRadius = 5,
            FieldOfView = 360,
            WhiskerTimeHorizon = 0.5f
        };
        var world = new CanonicalWorld(settings, new GridSpatialIndex(20, 100, 100));
        Assert.True(world.TryAddBoid(new Vec2(50, 50), new Vec2(1, 0)));
        Assert.True(world.TryAddBoid(new Vec2(52, 50), new Vec2(1, 0)));
        return world;
    }
}
