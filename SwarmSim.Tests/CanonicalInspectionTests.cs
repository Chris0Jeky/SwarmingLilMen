using System.Numerics;
using Raylib_cs;
using SwarmSim.Core.Canonical;
using SwarmSim.Render;
using RenderProgram = SwarmSim.Render.Program;

namespace SwarmSim.Tests;

public sealed class CanonicalInspectionTests
{
    [Theory]
    [InlineData(KeyboardKey.S, CanonicalPolicyIds.Separation)]
    [InlineData(KeyboardKey.A, CanonicalPolicyIds.Alignment)]
    [InlineData(KeyboardKey.C, CanonicalPolicyIds.Cohesion)]
    public void RendererKeysDisableExactlyOneNamedContributionAndToggleBack(KeyboardKey key, string policy)
    {
        var baseline = Cluster();
        var disabled = Cluster();
        var restored = Cluster();
        var input = RenderProgram.Controls.Capture(RendererMode.Canonical, new KeyInput(key));
        CanonicalInspectionOverlay.ApplyRuleSwitches(input, disabled);
        CanonicalInspectionOverlay.ApplyRuleSwitches(input, restored);
        CanonicalInspectionOverlay.ApplyRuleSwitches(input, restored);
        Assert.False(disabled.IsPolicyEnabled(policy));
        Assert.True(restored.IsPolicyEnabled(policy));
        baseline.Step(0.1f);
        disabled.Step(0.1f);
        restored.Step(0.1f);
        baseline.TryGetMetrics(0, out var original);
        disabled.TryGetMetrics(0, out var actual);
        Assert.True(original.SeparationMagnitude > 0f);
        Assert.True(original.AlignmentMagnitude > 0f);
        Assert.True(original.CohesionMagnitude > 0f);
        Assert.Equal(policy == CanonicalPolicyIds.Separation ? 0f : original.SeparationMagnitude, actual.SeparationMagnitude);
        Assert.Equal(policy == CanonicalPolicyIds.Alignment ? 0f : original.AlignmentMagnitude, actual.AlignmentMagnitude);
        Assert.Equal(policy == CanonicalPolicyIds.Cohesion ? 0f : original.CohesionMagnitude, actual.CohesionMagnitude);
        Assert.Equal(baseline.Boids.ToArray(), restored.Boids.ToArray());
        Assert.Equal(baseline.Instrumentation.SteeringVectors.ToArray(), restored.Instrumentation.SteeringVectors.ToArray());
    }

    [Fact]
    public void RuleSwitchesSurviveParameterRecreationAndTheResetStep()
    {
        var world = Cluster();
        world.SetPolicyEnabled(CanonicalPolicyIds.Separation, false);
        world.SetPolicyEnabled(CanonicalPolicyIds.Cohesion, false);
        var recreated = RenderProgram.RecreateCanonicalWorld(world, world.Settings);
        var reset = RenderProgram.ResetCanonicalWorld(world, world.Settings, 3);
        foreach (var replacement in new[] { recreated, reset })
        {
            Assert.False(replacement.IsPolicyEnabled(CanonicalPolicyIds.Separation));
            Assert.True(replacement.IsPolicyEnabled(CanonicalPolicyIds.Alignment));
            Assert.False(replacement.IsPolicyEnabled(CanonicalPolicyIds.Cohesion));
        }
        Assert.Equal(world.Boids.ToArray(), recreated.Boids.ToArray());
    }

    [Fact]
    public void StatusTextSeparatesRecordedTickFromDisplayedLinksAndCap()
    {
        Assert.Equal("Tick 7 recorded: 5 inputs, no truncation | Displayed: 5 links, cap 16 | Gray links: no applied rule",
            CanonicalInspectionOverlay.FormatStatus(7, true, true, 5, false, 5, 16, false));
        Assert.Equal("Tick 7 recorded: 139 inputs, no truncation | Displayed: 128 links, cap 128 reached, links omitted | Gray links: no applied rule",
            CanonicalInspectionOverlay.FormatStatus(7, true, true, 139, false, 128, 128, true));
        Assert.Equal("Tick 7 recorded: 16 inputs, simulation query truncated | Displayed: 16 links, cap 16 reached, links omitted | Gray links: no applied rule",
            CanonicalInspectionOverlay.FormatStatus(7, true, true, 16, true, 16, 16, true));
        Assert.Equal("Inspection: waiting for first completed tick", CanonicalInspectionOverlay.FormatStatus(0, false, false, 0, false, 0, 0, false));
        Assert.Equal("Tick 7: subject not recorded", CanonicalInspectionOverlay.FormatStatus(7, true, false, 0, false, 0, 0, false));
    }

    [Fact]
    public void DenseSceneAboveTheDisplayBufferReportsAllRecordedInputsButOnlyTheDisplayedLinks()
    {
        var settings = new CanonicalWorldSettings { InitialCapacity = 140, WorldWidth = 1920, WorldHeight = 1080,
            SenseRadius = 200, FieldOfView = 360, MaxNeighbors = 256 };
        var world = new CanonicalWorld(settings, new GridSpatialIndex(200, 1920, 1080));
        for (int i = 0; i < 140; i++) world.TryAddBoid(new Vec2(900 + i * 0.1f, 500), new Vec2(1, 0));
        var neighbors = new int[128];
        var weights = new float[128];
        Assert.Equal(256, world.EffectiveMaxNeighbors);
        Assert.Equal("Inspection: waiting for first completed tick", CanonicalInspectionOverlay.Inspect(world, 0, neighbors, weights).Status);

        world.Step(settings.FixedDeltaTime);
        var inspection = CanonicalInspectionOverlay.Inspect(world, 0, neighbors, weights);
        Assert.Equal(139, world.Instrumentation.NeighborCounts[0]);
        Assert.False(world.Instrumentation.QueryTruncated[0]);
        Assert.Equal(128, inspection.Result.Count);
        Assert.True(inspection.Result.IsTruncated);
        Assert.Equal("Tick 1 recorded: 139 inputs, no truncation | Displayed: 128 links, cap 128 reached, links omitted | Gray links: no applied rule",
            inspection.Status);
        world.TryAddBoid(new Vec2(100, 100), new Vec2(1, 0));
        Assert.Equal("Tick 1 recorded: 139 inputs, no truncation | Displayed: 128 links, cap 128 reached, links omitted | Gray links: no applied rule",
            CanonicalInspectionOverlay.Inspect(world, 0, neighbors, weights).Status);
    }

    [Fact]
    public void LateSpawnedSubjectIsReportedAsNotRecorded()
    {
        var world = Cluster();
        world.Step(0.1f);
        world.TryAddBoid(new Vec2(900, 900), new Vec2(1, 0));
        Assert.Equal("Tick 1: subject not recorded", CanonicalInspectionOverlay.Inspect(world, 2, new int[16], new float[16]).Status);
    }

    [Theory]
    [InlineData(0, 5, 0)]
    [InlineData(3, 5, 3)]
    [InlineData(4, 5, 4)]
    [InlineData(9, 5, 4)]
    [InlineData(2, 0, 0)]
    [InlineData(-1, 5, 0)]
    public void ParameterRecreationKeepsTheInspectedBoidWhenItStillExists(int subject, int count, int expected)
        => Assert.Equal(expected, CanonicalInspectionOverlay.KeepSubject(subject, count));

    [Fact]
    public void LegacyKeysDoNotToggleCanonicalPolicies()
    {
        var world = Cluster();
        foreach (var key in new[] { KeyboardKey.S, KeyboardKey.A, KeyboardKey.C })
            CanonicalInspectionOverlay.ApplyRuleSwitches(RenderProgram.Controls.Capture(RendererMode.Legacy, new KeyInput(key)), world);
        Assert.All(world.Policies, rule => Assert.True(world.IsPolicyEnabled(rule.Id)));
    }

    [Fact]
    public void RecordedInputsAndNeighborhoodStayAtTheTickThatProducedTheForces()
    {
        var settings = new CanonicalWorldSettings { InitialCapacity = 8, WorldWidth = 200, WorldHeight = 200,
            SenseRadius = 10, TargetSpeed = 2, FieldOfView = 360, Mode = CanonicalMode.ClassicReynolds };
        var world = new CanonicalWorld(settings, new GridSpatialIndex(10, 200, 200));
        world.TryAddBoid(new Vec2(50, 50), new Vec2(1, 0));
        world.TryAddBoid(new Vec2(54, 50), new Vec2(-1, 0));
        foreach (var rule in world.Policies) world.SetPolicyEnabled(rule.Id, false);
        var inputs = world.Boids.ToArray();
        var neighbors = new int[world.EffectiveMaxNeighbors];
        var weights = new float[neighbors.Length];
        Assert.True(world.LastTickInputs.IsEmpty);
        Assert.True(world.Instrumentation.SteeringVectors.IsEmpty);
        Assert.Equal(0, world.QueryLastTickVisibleNeighbors(0, neighbors, weights).Count);

        world.Step(10f);
        Assert.Equal(inputs, world.LastTickInputs.ToArray());
        Assert.Equal(0, world.QueryVisibleNeighbors(0, neighbors, weights).Count);
        Assert.Equal(1, world.QueryLastTickVisibleNeighbors(0, neighbors, weights).Count);
        Assert.Equal(1, neighbors[0]);
        Assert.Equal(1f, weights[0]);
        world.SetVelocity(0, new Vec2(0, 1));
        world.TryAddBoid(new Vec2(50, 51), new Vec2(0, 1));
        Assert.Equal(inputs, world.LastTickInputs.ToArray());
        Assert.Equal(0, world.QueryLastTickVisibleNeighbors(2, neighbors, weights).Count);
        Assert.Equal(0, world.QueryLastTickVisibleNeighbors(-1, neighbors, weights).Count);
        var nextInputs = world.Boids.ToArray();
        world.Step(0.1f);
        Assert.Equal(nextInputs, world.LastTickInputs.ToArray());
    }

    [Fact]
    public void LastTickQueryRetainsOriginalCapAndDoesNotChangeFuturePhysics()
    {
        var settings = new CanonicalWorldSettings { InitialCapacity = 10, WorldWidth = 100, WorldHeight = 100,
            SenseRadius = 30, MaxNeighbors = 4, FieldOfView = 90, WanderStrength = 0.1f };
        var observed = RenderProgram.CreateCanonicalWorld(settings, 10);
        var control = RenderProgram.CreateCanonicalWorld(settings, 10);
        var neighbors = new int[4];
        var weights = new float[4];
        for (int tick = 0; tick < 64; tick++)
        {
            var before = observed.QueryVisibleNeighbors(0, neighbors, weights);
            int[] indices = neighbors[..before.Count];
            float[] inputWeights = weights[..before.Count];
            observed.Step(settings.FixedDeltaTime);
            control.Step(settings.FixedDeltaTime);
            var last = observed.QueryLastTickVisibleNeighbors(0, neighbors, weights);
            Assert.Equal(before, last);
            Assert.Equal(indices, neighbors[..last.Count]);
            Assert.Equal(inputWeights, weights[..last.Count]);
            Assert.Equal(observed.Instrumentation.QueryTruncated[0], last.IsTruncated);
            Assert.Equal(control.Boids.ToArray(), observed.Boids.ToArray());
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void DenseLastTickQueryReportsTruncationAtTheRequestedCap(int capacity)
    {
        var settings = new CanonicalWorldSettings { InitialCapacity = 8, MaxNeighbors = 4, FieldOfView = 360 };
        var world = new CanonicalWorld(settings, new GridSpatialIndex(10, 1920, 1080));
        for (int i = 0; i < 8; i++) world.TryAddBoid(new Vec2(100 + i, 100), new Vec2(1, 0));
        world.Step(0.1f);
        var query = world.QueryLastTickVisibleNeighbors(0, new int[capacity], new float[capacity]);
        Assert.Equal(capacity, query.Count);
        Assert.True(query.IsTruncated);
        Assert.True(world.Instrumentation.QueryTruncated[0]);
    }

    [Fact]
    public void RepeatedInspectionQueriesAllocateNothingAfterWarmup()
    {
        var world = Cluster();
        var neighbors = new int[world.EffectiveMaxNeighbors];
        var weights = new float[neighbors.Length];
        for (int i = 0; i < 64; i++)
        {
            world.Step(0.01f);
            world.QueryLastTickVisibleNeighbors(0, neighbors, weights);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            world.Step(0.01f);
            world.QueryLastTickVisibleNeighbors(0, neighbors, weights);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void SteeringArrowUsesResolvedForceBeforeVelocityStages()
    {
        var settings = new CanonicalWorldSettings { InitialCapacity = 1, MaxForce = 10, TargetSpeed = 2 };
        var world = new CanonicalWorld(settings, new GridSpatialIndex(10, 1920, 1080),
            new CanonicalPipeline([new FixedForce()], new IndependentClampSumResolver(), [new TargetSpeedStage()]));
        world.TryAddBoid(new Vec2(50, 50), new Vec2(1, 0));
        world.Step(0.1f);
        Vec2 force = world.Instrumentation.SteeringVectors[0];
        Assert.Equal(new Vec2(3, 4), force);
        Assert.Equal(25f, world.Instrumentation.SteeringMagnitudesSquared[0]);
        Assert.NotEqual(force, (world.Boids[0].Velocity - world.LastTickInputs[0].Velocity) / 0.1f);
        Assert.Equal(new Vector2(19.2f, 25.6f), CanonicalInspectionOverlay.SteeringOffset(force, 10f));
        Assert.Equal(Vector2.Zero, CanonicalInspectionOverlay.SteeringOffset(Vec2.Zero, 10f));
        Assert.Equal(new Vector2(128, 0), CanonicalInspectionOverlay.SteeringOffset(new Vec2(100, 0), 10));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(90f)]
    [InlineData(270f)]
    [InlineData(360f)]
    public void FovArcUsesHeadingCenteredFullAngleAndCorrectRadius(float fov)
    {
        var boid = new Boid(new Vec2(100, 200), new Vec2(0, 1));
        var center = new Vector2(100, 200);
        for (int i = 0; i <= 64; i++)
        {
            Vector2 delta = CanonicalInspectionOverlay.FovPoint(boid, 50, fov, i / 64f) - center;
            Assert.InRange(Math.Abs(delta.Length() - 50f), 0f, 0.00003f);
        }
        Assert.InRange(Vector2.Distance(new Vector2(100, 250), CanonicalInspectionOverlay.FovPoint(boid, 50, fov, 0.5f)), 0, 0.00003f);
        Vector2 left = CanonicalInspectionOverlay.FovPoint(boid, 50, fov, 0f) - center;
        Vector2 right = CanonicalInspectionOverlay.FovPoint(boid, 50, fov, 1f) - center;
        float expectedY = 50f * MathF.Cos(fov * MathF.PI / 360f);
        Assert.InRange(Math.Abs(left.Y - expectedY), 0, 0.00003f);
        Assert.InRange(Math.Abs(right.Y - expectedY), 0, 0.00003f);
        Assert.InRange(Math.Abs(left.X + right.X), 0, 0.00003f);
    }

    [Fact]
    public void LinkColorsExcludeZeroWeightsZeroAppliedRulesAndNonseparatingDistances()
    {
        var allApplied = new RuleInstrumentation.Metrics(1, 1f, 1f, 2f, 3f);
        var all = ContributionLinks.Separation | ContributionLinks.Alignment | ContributionLinks.Cohesion;
        var far = ContributionLinks.Alignment | ContributionLinks.Cohesion;
        Assert.Equal(all, CanonicalInspectionOverlay.ClassifyLinks(new Vec2(-3, -4), 1f, 10f, allApplied));
        Assert.Equal(far, CanonicalInspectionOverlay.ClassifyLinks(new Vec2(-10, 0), 1f, 10f, allApplied));
        Assert.Equal(far, CanonicalInspectionOverlay.ClassifyLinks(Vec2.Zero, 1f, 10f, allApplied));
        Assert.Equal(far, CanonicalInspectionOverlay.ClassifyLinks(new Vec2(-0.00001f, 0), 1f, 10f, allApplied));
        Assert.Equal(ContributionLinks.None, CanonicalInspectionOverlay.ClassifyLinks(new Vec2(-3, -4), 0f, 10f, allApplied));
        Assert.Equal(ContributionLinks.None, CanonicalInspectionOverlay.ClassifyLinks(new Vec2(-3, -4), 1f, 10f, default));
        Assert.Equal(ContributionLinks.Alignment, CanonicalInspectionOverlay.ClassifyLinks(new Vec2(-3, -4), 1f, 10f, new(1, 1, 0, 2, 0)));
    }

    private static CanonicalWorld Cluster()
    {
        var settings = new CanonicalWorldSettings { Mode = CanonicalMode.ClassicReynolds, InitialCapacity = 8,
            WorldWidth = 1000, WorldHeight = 1000, SenseRadius = 100, SeparationRadius = 20, FieldOfView = 360, TargetSpeed = 2 };
        var world = new CanonicalWorld(settings, new GridSpatialIndex(100, 1000, 1000));
        world.TryAddBoid(new Vec2(500, 500), new Vec2(1, 0));
        world.TryAddBoid(new Vec2(510, 505), new Vec2(0, 1));
        return world;
    }

    private sealed class KeyInput(KeyboardKey key) : IRendererInput
    {
        public bool IsPressed(KeyboardKey candidate) => candidate == key;
        public bool IsDown(KeyboardKey candidate) => false;
        public bool IsPressed(MouseButton button) => false;
    }

    private sealed class FixedForce : ILocalSteeringPolicy
    {
        public PolicyMetadata Metadata { get; } = new("test.force");
        public int MaxIntents => 1;
        public void Emit(in LocalObservation observation, in LocalPolicyContext context, ref IntentWriter intents)
            => intents.AddForce(new Vec2(3, 4));
    }
}
