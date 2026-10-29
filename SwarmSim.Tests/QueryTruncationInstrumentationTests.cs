using System;
using System.Collections.Generic;
using SwarmSim.Core.Canonical;
using Xunit;

namespace SwarmSim.Tests;

public sealed class QueryTruncationInstrumentationTests
{
    [Fact]
    public void Step_DenseCluster_RecordsTruncationForCappedBoids()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 16,
            WorldWidth = 100f,
            WorldHeight = 100f,
            SenseRadius = 20f,
            FieldOfView = 360f,
            MaxNeighbors = 1,
            TargetSpeed = 1f,
            WanderStrength = 0f,
        };
        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        for (int i = 0; i < 10; i++)
            Assert.True(world.TryAddBoid(new Vec2(50f + i * 0.5f, 50f), new Vec2(1f, 0f)));

        Assert.Equal(4, world.EffectiveMaxNeighbors);

        world.Step(0.1f);

        ReadOnlySpan<bool> truncated = world.Instrumentation.QueryTruncated;
        Assert.Equal(world.Count, truncated.Length);
        int trueCount = 0;
        for (int i = 0; i < truncated.Length; i++)
        {
            Assert.True(truncated[i]);
            if (truncated[i])
                trueCount++;
        }
        Assert.Equal(world.Count, trueCount);
        Assert.Equal(trueCount, world.Instrumentation.TruncatedCount);
    }

    [Fact]
    public void Step_SparseWorld_RecordsNoTruncation()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 8,
            WorldWidth = 100f,
            WorldHeight = 100f,
            SenseRadius = 5f,
            FieldOfView = 360f,
            MaxNeighbors = 1,
            TargetSpeed = 1f,
            WanderStrength = 0f,
        };
        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        Assert.True(world.TryAddBoid(new Vec2(10f, 10f), new Vec2(1f, 0f)));
        Assert.True(world.TryAddBoid(new Vec2(50f, 50f), new Vec2(1f, 0f)));
        Assert.True(world.TryAddBoid(new Vec2(90f, 90f), new Vec2(1f, 0f)));

        world.Step(0.1f);

        ReadOnlySpan<bool> truncated = world.Instrumentation.QueryTruncated;
        Assert.Equal(world.Count, truncated.Length);
        int trueCount = 0;
        for (int i = 0; i < truncated.Length; i++)
        {
            Assert.False(truncated[i]);
            if (truncated[i])
                trueCount++;
        }
        Assert.Equal(0, trueCount);
        Assert.Equal(0, world.Instrumentation.TruncatedCount);
    }

    [Fact]
    public void Step_TruncationRecord_RefreshesEachTick()
    {
        // Zero steering weights keep motion ballistic so the fan-out below disperses the
        // cluster by a predictable distance; the query under test always reads pre-step
        // positions, so the assertions below do not depend on steering details.
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 8,
            WorldWidth = 200f,
            WorldHeight = 200f,
            SenseRadius = 6f,
            FieldOfView = 360f,
            MaxNeighbors = 2,
            TargetSpeed = 2f,
            SeparationWeight = 0f,
            AlignmentWeight = 0f,
            CohesionWeight = 0f,
            WanderStrength = 0f,
        };
        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        for (int i = 0; i < 6; i++)
            Assert.True(world.TryAddBoid(new Vec2(100f, 100f + i * 0.8f), new Vec2(1f, 0f)));

        Assert.Equal(4, world.EffectiveMaxNeighbors);

        world.Step(0.1f);
        Assert.Equal(1ul, world.TickCount);
        bool[] firstTick = world.Instrumentation.QueryTruncated.ToArray();
        Assert.Equal(world.Count, firstTick.Length);
        for (int i = 0; i < firstTick.Length; i++)
            Assert.True(firstTick[i]);
        Assert.Equal(world.Count, world.Instrumentation.TruncatedCount);

        // Fan the headings outward, take one long step to disperse well beyond SenseRadius,
        // then a normal step whose query observes the dispersed positions.
        for (int i = 0; i < world.Count; i++)
        {
            float angle = i * MathF.PI / 3f;
            world.SetVelocity(i, new Vec2(MathF.Cos(angle), MathF.Sin(angle)));
        }
        world.Step(15f);

        // The long step queried the dense cluster, then integrated to dispersed positions.
        // A current-position query now reports no cap; last-tick telemetry must still report it.
        var telemetry = new CanonicalTelemetry(world.Count);
        world.CaptureTelemetryInto(telemetry);
        Span<int> neighbors = stackalloc int[world.EffectiveMaxNeighbors];
        Span<float> weights = stackalloc float[world.EffectiveMaxNeighbors];
        Assert.False(world.QueryVisibleNeighbors(0, neighbors, weights).IsTruncated);
        Assert.True(world.Instrumentation.QueryTruncated[0]);
        Assert.True(telemetry.QueryTruncated[0]);
        Assert.Equal(world.Count, telemetry.TruncatedCount);
        Assert.Equal(2ul, telemetry.TickCount);

        world.Step(0.1f);

        Assert.Equal(3ul, world.TickCount);
        ReadOnlySpan<bool> truncated = world.Instrumentation.QueryTruncated;
        Assert.Equal(world.Count, truncated.Length);
        int trueCount = 0;
        for (int i = 0; i < truncated.Length; i++)
        {
            Assert.False(truncated[i]);
            if (truncated[i])
                trueCount++;
        }
        Assert.Equal(0, trueCount);
        Assert.Equal(0, world.Instrumentation.TruncatedCount);
        Assert.True(firstTick[0]);
        Assert.True(telemetry.QueryTruncated[0]);
    }

    [Fact]
    public void Step_NoRules_RecordsNoTruncationWithoutThrowing()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 8,
            WorldWidth = 100f,
            WorldHeight = 100f,
            SenseRadius = 20f,
            FieldOfView = 360f,
            MaxNeighbors = 1,
            TargetSpeed = 1f,
            WanderStrength = 0f,
        };
        var world = new CanonicalWorld(settings,
            new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight), CanonicalPipeline.Empty());

        for (int i = 0; i < 6; i++)
            Assert.True(world.TryAddBoid(new Vec2(50f + i * 0.5f, 50f), new Vec2(1f, 0f)));

        var exception = Record.Exception(() => world.Step(0.1f));
        Assert.Null(exception);

        ReadOnlySpan<bool> truncated = world.Instrumentation.QueryTruncated;
        Assert.Equal(world.Count, truncated.Length);
        int trueCount = 0;
        for (int i = 0; i < truncated.Length; i++)
        {
            Assert.False(truncated[i]);
            if (truncated[i])
                trueCount++;
        }
        Assert.Equal(0, trueCount);
        Assert.Equal(0, world.Instrumentation.TruncatedCount);
    }
}
