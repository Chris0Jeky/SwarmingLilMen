using SwarmSim.Core.Canonical;
using RenderProgram = SwarmSim.Render.Program;

namespace SwarmSim.Tests;

public sealed class CanonicalRendererTelemetryTests
{
    [Fact]
    public void ReusedFrameCaptureDoesNotAllocateOverOneThousandWarmTicks()
    {
        var world = RenderProgram.CreateCanonicalWorld(CreateSettings(256), 256);
        var telemetry = RenderProgram.CaptureCanonicalTelemetry(world, null);
        for (int i = 0; i < 64; i++)
        {
            world.Step(world.Settings.FixedDeltaTime);
            telemetry = RenderProgram.CaptureCanonicalTelemetry(world, telemetry);
        }

        var original = telemetry;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            world.Step(world.Settings.FixedDeltaTime);
            telemetry = RenderProgram.CaptureCanonicalTelemetry(world, telemetry);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Same(original, telemetry);
        Assert.Equal(1064ul, telemetry.TickCount);
        Assert.Equal(256, telemetry.AgentCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void FrameCapturePreservesExistingHudValues(int ticks)
    {
        var world = CreateCluster();
        for (int i = 0; i < ticks; i++)
            world.Step(world.Settings.FixedDeltaTime);

        var oldSnapshot = world.CapturePerceptionSnapshot();
        var telemetry = RenderProgram.CaptureCanonicalTelemetry(world, null);

        Assert.Equal(oldSnapshot.TickCount, telemetry.TickCount);
        Assert.Equal(oldSnapshot.AgentCount, telemetry.AgentCount);
        Assert.Equal(oldSnapshot.NeighborCountStats, telemetry.NeighborCountStats);
        Assert.Equal(oldSnapshot.AverageNeighborWeight, telemetry.AverageNeighborWeight);
        Assert.Equal(oldSnapshot.AverageSeparationMagnitude, telemetry.AverageSeparationMagnitude);
        Assert.Equal(oldSnapshot.AverageAlignmentMagnitude, telemetry.AverageAlignmentMagnitude);
        Assert.Equal(oldSnapshot.AverageCohesionMagnitude, telemetry.AverageCohesionMagnitude);
        Assert.Equal(oldSnapshot.AverageNeighborDistance, telemetry.AverageNeighborDistance);
        Assert.Equal(oldSnapshot.MinNeighborDistance, telemetry.MinNeighborDistance);
        Assert.Equal(oldSnapshot.MaxNeighborDistance, telemetry.MaxNeighborDistance);
        Assert.Equal(oldSnapshot.SeparationPriorityTriggered, telemetry.SeparationPriorityTriggered);
    }

    [Fact]
    public void RespawnClearsPriorTelemetryAndReusesItsBuffers()
    {
        var first = CreateCluster();
        first.Step(first.Settings.FixedDeltaTime);
        var telemetry = RenderProgram.CaptureCanonicalTelemetry(first, null);
        Assert.True(telemetry.QueryTruncated[0]);

        var respawned = CreateWorld(CreateSettings(8));
        Assert.True(respawned.TryAddBoid(new Vec2(10f, 10f), new Vec2(1f, 0f)));
        var reused = RenderProgram.CaptureCanonicalTelemetry(respawned, telemetry);
        Assert.Same(telemetry, reused);
        Assert.Equal(0ul, reused.TickCount);
        Assert.Equal(0, reused.AgentCount);
        Assert.Equal(0f, reused.AverageNeighborDistance);
        Assert.Equal(0, reused.TruncatedCount);
        Assert.True(reused.QueryTruncated.IsEmpty);

        respawned.Step(respawned.Settings.FixedDeltaTime);
        reused = RenderProgram.CaptureCanonicalTelemetry(respawned, reused);
        Assert.Equal(first.TickCount, reused.TickCount);
        Assert.Equal(1, reused.AgentCount);
        Assert.False(reused.QueryTruncated[0]);
        Assert.Equal(0f, reused.AverageNeighborCount);
    }

    [Fact]
    public void BufferOnlyGrowsWhenAReplacementWorldNeedsMoreCapacity()
    {
        var first = CreateWorld(CreateSettings(8));
        var original = RenderProgram.CaptureCanonicalTelemetry(first, null);
        var larger = RenderProgram.CreateCanonicalWorld(CreateSettings(32), 12);
        larger.Step(larger.Settings.FixedDeltaTime);

        var grown = RenderProgram.CaptureCanonicalTelemetry(larger, original);
        Assert.NotSame(original, grown);
        Assert.True(grown.Capacity >= larger.Settings.InitialCapacity);
        Assert.Equal(larger.Count, grown.AgentCount);
        Assert.Same(grown, RenderProgram.CaptureCanonicalTelemetry(larger, grown));
        Assert.Same(grown, RenderProgram.CaptureCanonicalTelemetry(first, grown));
        Assert.Equal(0, grown.AgentCount);
    }

    [Fact]
    public void OverlayKeepsRecordedCapWhenTheCurrentPositionsAreNoLongerCapped()
    {
        var world = CreateCluster();
        world.Step(0.1f);
        for (int i = 0; i < world.Count; i++)
        {
            float angle = i * MathF.PI / 3f;
            world.SetVelocity(i, new Vec2(MathF.Cos(angle), MathF.Sin(angle)));
        }
        world.Step(15f);

        var telemetry = RenderProgram.CaptureCanonicalTelemetry(world, null);
        var current = QueryCurrent(world, world.EffectiveMaxNeighbors);
        Assert.True(telemetry.QueryTruncated[0]);
        Assert.False(current.IsTruncated);
        string status = RenderProgram.FormatCanonicalInteractionStatus(
            telemetry, 0, 0, current.IsTruncated, world.EffectiveMaxNeighbors, world.EffectiveMaxNeighbors);

        Assert.Equal("Now: whisker hits 0 | Tick 2: query capped at 4", status);
    }

    [Fact]
    public void OverlayDoesNotAttributeANewCurrentPositionCapToTheCompletedTick()
    {
        var world = CreateWorld(CreateSettings(8));
        for (int i = 0; i < 6; i++)
        {
            float angle = i * MathF.PI / 3f;
            var direction = new Vec2(MathF.Cos(angle), MathF.Sin(angle));
            Assert.True(world.TryAddBoid(new Vec2(100f, 100f) - direction * 30f, direction));
        }
        world.Step(15f);

        var telemetry = RenderProgram.CaptureCanonicalTelemetry(world, null);
        var current = QueryCurrent(world, world.EffectiveMaxNeighbors);
        Assert.False(telemetry.QueryTruncated[0]);
        Assert.True(current.IsTruncated);
        string status = RenderProgram.FormatCanonicalInteractionStatus(
            telemetry, 0, 0, current.IsTruncated, world.EffectiveMaxNeighbors, world.EffectiveMaxNeighbors);

        Assert.Equal("Now: whisker hits 0 | query capped at 4 | Tick 1: no truncation", status);
    }

    [Fact]
    public void OverlaySeparatesItsSmallerBufferCapFromTheSteeringCap()
    {
        var world = CreateCluster();
        world.Step(0.1f);
        var telemetry = RenderProgram.CaptureCanonicalTelemetry(world, null);
        var current = QueryCurrent(world, 2);
        Assert.True(current.IsTruncated);

        string status = RenderProgram.FormatCanonicalInteractionStatus(
            telemetry, 0, 2, current.IsTruncated, 2, world.EffectiveMaxNeighbors);

        Assert.Equal("Now: whisker hits 2 | query capped at 2 | Tick 1: query capped at 4", status);
    }

    [Fact]
    public void OverlayShowsPendingBeforeAnyTickHasCompleted()
    {
        var telemetry = RenderProgram.CaptureCanonicalTelemetry(CreateCluster(), null);

        string status = RenderProgram.FormatCanonicalInteractionStatus(telemetry, 0, 1, false, 4, 4);

        Assert.Equal("Now: whisker hits 1 | Tick: pending", status);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void OverlayDoesNotInventARecordForAnUncapturedSubject(int subject)
    {
        var world = CreateCluster();
        world.Step(0.1f);
        Assert.True(world.TryAddBoid(new Vec2(10f, 10f), new Vec2(1f, 0f)));
        var telemetry = RenderProgram.CaptureCanonicalTelemetry(world, null);

        string status = RenderProgram.FormatCanonicalInteractionStatus(telemetry, subject, 0, false, 4, 4);

        Assert.Equal("Now: whisker hits 0 | Tick 1: subject not recorded", status);
    }

    private static CanonicalWorldSettings CreateSettings(int capacity) => new()
    {
        InitialCapacity = capacity,
        Seed = 7919,
        WorldWidth = 200f,
        WorldHeight = 200f,
        SenseRadius = 6f,
        FieldOfView = 360f,
        MaxNeighbors = 2,
        TargetSpeed = 2f,
        SeparationWeight = 0f,
        AlignmentWeight = 0f,
        CohesionWeight = 0f,
        WanderStrength = 0f
    };

    private static CanonicalWorld CreateWorld(CanonicalWorldSettings settings) => new(
        settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));

    private static CanonicalWorld CreateCluster()
    {
        var world = CreateWorld(CreateSettings(8));
        for (int i = 0; i < 6; i++)
            Assert.True(world.TryAddBoid(new Vec2(100f, 100f + i * 0.8f), new Vec2(1f, 0f)));
        return world;
    }

    private static SpatialQueryResult QueryCurrent(CanonicalWorld world, int capacity)
    {
        Span<int> neighbors = stackalloc int[capacity];
        Span<float> weights = stackalloc float[capacity];
        return world.QueryVisibleNeighbors(0, neighbors, weights);
    }
}
