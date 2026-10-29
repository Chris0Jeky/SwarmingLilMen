using SwarmSim.Core.Canonical;
using SwarmSim.Core.Utils;
using System.Runtime.CompilerServices;

namespace SwarmSim.Tests;

public sealed class CanonicalCompositionTests
{
    [Fact]
    public void AdditionalLegacyRuleActuallyContributesInsteadOfDiscardingItsOutput()
    {
        CanonicalWorldSettings settings = Settings();
        var baseline = Create(settings);
        var extended = Create(settings);
        extended.AddRule(new ConstantLegacyForce(new Vec2(0f, 1f)));
        Assert.True(baseline.TryAddBoid(new Vec2(50f, 50f), new Vec2(1f, 0f)));
        Assert.True(extended.TryAddBoid(new Vec2(50f, 50f), new Vec2(1f, 0f)));

        baseline.Step(0.1f);
        extended.Step(0.1f);

        Assert.True(extended.Instrumentation.SteeringMagnitudesSquared[0] > 0f,
            "An appended rule must enter arbitration, not have its returned force discarded.");
        Assert.NotEqual(baseline.Boids[0].Position.Y, extended.Boids[0].Position.Y);
    }

    [Fact]
    public void EmptyPipelinePreservesVelocityBitsAndMovesInAStraightLine()
    {
        CanonicalWorldSettings settings = Settings();
        var world = Create(settings, CanonicalPipeline.Empty());
        Assert.True(world.TryAddBoid(new Vec2(30f, 30f), new Vec2(0.6f, -0.8f)));
        Boid initial = world.Boids[0];
        Vec2 expected = initial.Position;
        for (int tick = 0; tick < 120; tick++)
        {
            expected += initial.Velocity * settings.FixedDeltaTime;
            world.Step(settings.FixedDeltaTime);
            EqualBits(initial.Velocity, world.Boids[0].Velocity);
            EqualBits(expected, world.Boids[0].Position);
            Assert.Equal(0f, world.Instrumentation.SteeringMagnitudesSquared[0]);
            Assert.Equal(0, world.Instrumentation.NeighborCounts[0]);
        }
    }

    [Fact]
    public void AdditionalBoundedPolicyChangesTrajectory()
    {
        CanonicalWorldSettings settings = Settings();
        var world = Create(settings, CanonicalPipeline.ClassicReynolds(settings));
        world.AddPolicy(new ConstantPolicy("constant", new Vec2(0f, 1f)));
        Assert.Equal(4, world.Policies.Count);
        Assert.True(world.TryAddBoid(new Vec2(50f, 50f), new Vec2(1f, 0f)));

        world.Step(0.1f);

        Assert.Equal(1f, world.Instrumentation.SteeringMagnitudesSquared[0]);
        Assert.True(world.Boids[0].Position.Y > 50f);
    }

    [Fact]
    public void TagsDriveWeightPriorityAndInstrumentationWithoutPositionalRuleMeaning()
    {
        CanonicalWorldSettings settings = Settings();
        var cohesion = new TaggedPolicy(new PolicyMetadata("custom-cohesion", SteeringRole.Cohesion, priority: 5, weight: 2f), new Vec2(0f, 0.25f));
        var alignment = new TaggedPolicy(new PolicyMetadata("custom-alignment", SteeringRole.Alignment, priority: 1, weight: 2f), new Vec2(0.25f, 0f));
        var world = Create(settings, new CanonicalPipeline([cohesion, alignment], new PriorityForceBudgetResolver()));
        Assert.True(world.TryAddBoid(new Vec2(50f, 50f), new Vec2(1f, 0f)));

        world.Step(0.1f);

        Assert.Equal(new[] { "custom-alignment", "custom-cohesion" }, world.Policies.Select(policy => policy.Id));
        Assert.True(world.TryGetMetrics(0, out var metrics));
        Assert.Equal(0f, metrics.SeparationMagnitude);
        Assert.Equal(0.5f, metrics.AlignmentMagnitude);
        Assert.Equal(0.5f, metrics.CohesionMagnitude);
        EqualBits(new Vec2(2.05f, 0.05f), world.Boids[0].Velocity);
    }

    [Fact]
    public void LocalPoliciesSeeOnlyTheBoundedVisibleRelativeNeighborhood()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4, WorldWidth = 100f, WorldHeight = 100f,
            SenseRadius = 10f, TargetSpeed = 2f, FieldOfView = 90f
        };
        var observer = new ObservationProbe();
        var world = Create(settings, new CanonicalPipeline([observer], new PriorityForceBudgetResolver()));
        Assert.True(world.TryAddBoid(new Vec2(5f, 5f), new Vec2(1f, 0f), 0));
        Assert.True(world.TryAddBoid(new Vec2(9f, 5f), new Vec2(0f, 1f), 1));
        Assert.True(world.TryAddBoid(new Vec2(2f, 5f), new Vec2(0f, -1f), 2));
        Assert.True(world.TryAddBoid(new Vec2(30f, 5f), new Vec2(-1f, 0f), 3));

        world.Step(settings.FixedDeltaTime);

        NeighborView neighbor = Assert.Single(observer.Seen);
        Assert.Equal((byte)1, neighbor.Group);
        EqualBits(new Vec2(4f, 0f), neighbor.RelativePosition);
        EqualBits(new Vec2(-4f, 0f), neighbor.ReverseRelativePosition);
        EqualBits(new Vec2(-2f, 2f), neighbor.RelativeVelocity);
        EqualBits(new Vec2(0f, 2f), neighbor.Velocity);
        Assert.Equal(4f, neighbor.Distance);
        Assert.Equal(1f, neighbor.FieldOfViewWeight);
    }

    [Fact]
    public void EqualPrioritiesKeepRegistrationOrderWhenBudgetRunsOut()
    {
        CanonicalWorldSettings settings = Settings();
        var world = Create(settings, new CanonicalPipeline(
            [new ConstantPolicy("first", new Vec2(0f, 1f)), new ConstantPolicy("second", new Vec2(1f, 0f))],
            new PriorityForceBudgetResolver()));
        Assert.True(world.TryAddBoid(new Vec2(50f, 50f), new Vec2(1f, 0f)));

        world.Step(0.1f);

        EqualBits(new Vec2(2f, 0.1f), world.Boids[0].Velocity);
        Assert.Equal(1, world.GetPolicyExecutionCount("first"));
        Assert.Equal(1, world.GetPolicyExecutionCount("second"));
    }

    [Fact]
    public void IntentWriterRejectsOverflowAndNonfinitePayloadsWithoutSilentTruncation()
    {
        var metadata = new PolicyMetadata("bounded");
        Assert.Throws<InvalidOperationException>(() =>
        {
            var writer = new IntentWriter(metadata, new SteeringIntent[1]);
            writer.AddForce(new Vec2(1f, 0f));
            writer.AddForce(new Vec2(0f, 1f));
        });
        Assert.Throws<ArgumentException>(() =>
        {
            var writer = new IntentWriter(metadata, new SteeringIntent[1]);
            writer.AddForce(new Vec2(float.NaN, 0f));
        });
        Assert.Throws<ArgumentException>(() => new PolicyMetadata("invalid", weight: float.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => new CanonicalPipeline(
            [new ConstantPolicy("duplicate", Vec2.Zero), new ConstantPolicy("duplicate", Vec2.Zero)],
            new PriorityForceBudgetResolver()));
    }

    [Fact]
    public void MutablePipelineComponentsBelongToOneWorld()
    {
        CanonicalWorldSettings settings = Settings();
        var policy = new ConstantPolicy("owned", new Vec2(0f, 1f));
        var pipeline = new CanonicalPipeline([policy], new PriorityForceBudgetResolver());
        _ = Create(settings, pipeline);
        Assert.Throws<InvalidOperationException>(() => Create(settings, pipeline));
        Assert.Throws<InvalidOperationException>(() => Create(settings,
            new CanonicalPipeline([policy], new PriorityForceBudgetResolver())));
    }

    [Fact]
    public void ClassicCompositionClampsEachRuleThenSumsWithoutASharedPostClamp()
    {
        CanonicalWorldSettings settings = ClassicSettings();
        var world = ClassicWorld(settings);
        Boid[] before = world.Boids.ToArray();
        int[] neighbors = [1, 2];
        float[] weights = [1f, 1f];
        var context = new RuleContext(settings.TargetSpeed, settings.MaxForce, settings.SenseRadius,
            -1f, settings.FixedDeltaTime, 1f, worldWidth: settings.WorldWidth, worldHeight: settings.WorldHeight);
        Vec2 expected = new SeparationRule(settings.SeparationWeight, settings.SeparationRadius)
            .Compute(0, before[0], before, neighbors, weights, context).ClampMagnitude(settings.MaxForce);
        expected += new AlignmentRule(settings.AlignmentWeight)
            .Compute(0, before[0], before, neighbors, weights, context).ClampMagnitude(settings.MaxForce);
        expected += new CohesionRule(settings.CohesionWeight)
            .Compute(0, before[0], before, neighbors, weights, context).ClampMagnitude(settings.MaxForce);

        world.Step(settings.FixedDeltaTime);

        Assert.True(expected.Length > settings.MaxForce);
        Assert.Equal(expected.LengthSquared, world.Instrumentation.SteeringMagnitudesSquared[0], 1e-6f);
        foreach (string id in new[] { CanonicalPolicyIds.Whisker, CanonicalPolicyIds.Wander })
            Assert.Equal(0, world.GetPolicyExecutionCount(id));
        foreach (string id in new[] { CanonicalStageIds.Priority, CanonicalStageIds.ShapedAvoidance, CanonicalStageIds.TurnRate })
            Assert.Equal(0, world.GetStageExecutionCount(id));
        var telemetry = new CanonicalTelemetry(world.Count);
        world.CaptureTelemetryInto(telemetry);
        Assert.All(telemetry.WhiskerCounts.ToArray(), count => Assert.Equal(0, count));
        Assert.False(telemetry.SeparationPriorityTriggered);
    }

    [Theory]
    [InlineData("separation")]
    [InlineData("alignment")]
    [InlineData("cohesion")]
    public void NamedDisableRemovesThatContributionAndPreservesTheOtherClassicContributions(string disabled)
    {
        CanonicalWorldSettings settings = ClassicSettings();
        var baseline = ClassicWorld(settings);
        var changed = ClassicWorld(settings);
        changed.SetPolicyEnabled(disabled, false);

        baseline.Step(settings.FixedDeltaTime);
        changed.Step(settings.FixedDeltaTime);

        Assert.True(baseline.TryGetMetrics(0, out var expected));
        Assert.True(changed.TryGetMetrics(0, out var actual));
        Assert.True(expected.SeparationMagnitude > 0f && expected.AlignmentMagnitude > 0f && expected.CohesionMagnitude > 0f);
        Assert.Equal(disabled == "separation" ? 0f : expected.SeparationMagnitude, actual.SeparationMagnitude);
        Assert.Equal(disabled == "alignment" ? 0f : expected.AlignmentMagnitude, actual.AlignmentMagnitude);
        Assert.Equal(disabled == "cohesion" ? 0f : expected.CohesionMagnitude, actual.CohesionMagnitude);
        Assert.Equal(0, changed.GetPolicyExecutionCount(disabled));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InjectedWanderOwnsAProperNamedStreamEvenWhenSettingsWanderIsZero(bool addAfterSpawn)
    {
        CanonicalWorldSettings settings = Settings();
        const float strength = 0.25f;
        const float rate = 0.5f;
        var policy = new WanderPolicy(strength, rate);
        var world = Create(settings, addAfterSpawn ? CanonicalPipeline.Empty()
            : new CanonicalPipeline([policy], new PriorityForceBudgetResolver()));
        Assert.True(world.TryAddBoid(new Vec2(50f, 50f), new Vec2(1f, 0f)));
        if (addAfterSpawn) world.AddPolicy(policy);
        Vec2 before = world.Boids[0].Velocity;
        Pcg32Rng random = world.RandomStreams.CreateStream("canonical.wander", 0);
        float angle = random.NextFloat() * (MathF.PI * 2f);
        angle += (-1f + random.NextFloat() * 2f) * rate * settings.FixedDeltaTime;
        Vec2 force = new Vec2(MathF.Cos(angle), MathF.Sin(angle)) * strength * settings.TargetSpeed;
        var writer = new IntentWriter(policy.Metadata, new SteeringIntent[1]);
        writer.AddForce(force);
        Vec2 expected = Vec2.Zero;
        float remaining = settings.MaxForce;
        _ = new PriorityForceBudgetResolver().Accumulate(writer.Written[0], 0f, settings.MaxForce, ref expected, ref remaining);
        expected = before + expected * settings.FixedDeltaTime;

        world.Step(settings.FixedDeltaTime);

        EqualBits(expected, world.Boids[0].Velocity);
    }

    [Fact]
    public void EmptyPipelinePreservesNegativeZeroVelocityComponent()
    {
        CanonicalWorldSettings settings = Settings();
        var world = Create(settings, CanonicalPipeline.Empty());
        Assert.True(world.TryAddBoid(new Vec2(50f, 50f), new Vec2(-0f, 1f)));
        Vec2 initial = world.Boids[0].Velocity;
        Assert.Equal(unchecked((int)0x80000000), BitConverter.SingleToInt32Bits(initial.X));
        world.Step(settings.FixedDeltaTime);
        EqualBits(initial, world.Boids[0].Velocity);
    }

    [Fact]
    public void BoundedRulesMatchTheirLegacyArithmeticBitForBitAcrossSeamsAndHalfWorldTies()
    {
        Boid[][] cases =
        [
            [new(new Vec2(0f, 0f), new Vec2(1f, -0f)), new(new Vec2(5f, 0f), new Vec2(-1f, 0.3f)), new(new Vec2(9.75f, 9.5f), new Vec2(0.2f, 1f))],
            [new(new Vec2(9.9f, 0.1f), new Vec2(-0.7f, 0.3f)), new(new Vec2(0.1f, 9.9f), new Vec2(0.8f, -0.2f)), new(new Vec2(9.9f, 0.1f), new Vec2(0f, 1f))],
            [new(new Vec2(1f, 1f), new Vec2(16_777_216f, 0.25f)), new(new Vec2(2f, 2f), new Vec2(1f, 2f)), new(new Vec2(3f, 3f), new Vec2(-2f, 0.3f))]
        ];
        IRule[] rules = [new SeparationRule(1.3f, 7f), new AlignmentRule(0.7f), new CohesionRule(0.4f)];
        int[] indices = [1, 2];
        float[] weights = [0.3f, 0.9f];
        var legacyContext = new RuleContext(2.3f, 0.2f, 10f, -1f, 0.016f, 1.7f, worldWidth: 10f, worldHeight: 10f);
        var localContext = new LocalPolicyContext(2.3f, 0.2f, 10f, -1f, 0.016f, 1.7f);
        foreach (Boid[] boids in cases)
        {
            var neighbors = new NeighborView[2];
            for (int i = 0; i < neighbors.Length; i++)
            {
                Boid neighbor = boids[indices[i]];
                Vec2 delta = legacyContext.MinimumImageDelta(boids[0].Position, neighbor.Position);
                neighbors[i] = new NeighborView(delta, legacyContext.MinimumImageDelta(neighbor.Position, boids[0].Position),
                    neighbor.Velocity - boids[0].Velocity, neighbor.Velocity, neighbor.Group, weights[i], delta.Length);
            }
            var observation = new LocalObservation(boids[0], neighbors);
            foreach (IRule rule in rules)
            {
                var policy = Assert.IsAssignableFrom<ILocalSteeringPolicy>(rule);
                var writer = new IntentWriter(policy.Metadata, new SteeringIntent[1]);
                policy.Emit(observation, localContext, ref writer);
                Assert.Equal(1, writer.Count);
                EqualBits(rule.Compute(0, boids[0], boids, indices, weights, legacyContext), writer.Written[0].Payload);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PoliciesCannotReplaceTheBorrowedWriterToForgeMetadataOrExpandCapacity(bool forgeMetadata)
    {
        CanonicalWorldSettings settings = Settings();
        var world = Create(settings, new CanonicalPipeline([new ReplacingWriterPolicy(forgeMetadata)], new IndependentClampSumResolver()));
        Assert.True(world.TryAddBoid(new Vec2(50f, 50f), new Vec2(1f, 0f)));
        Boid before = world.Boids[0];

        Assert.Throws<InvalidOperationException>(() => world.Step(settings.FixedDeltaTime));

        Assert.Equal(0ul, world.TickCount);
        EqualBits(before.Position, world.Boids[0].Position);
        EqualBits(before.Velocity, world.Boids[0].Velocity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PoliciesCannotMutatePopulationOrVelocityDuringSynchronousStep(bool spawn)
    {
        CanonicalWorldSettings settings = Settings();
        var policy = new ReentrantPolicy(spawn);
        var world = Create(settings, new CanonicalPipeline([policy], new PriorityForceBudgetResolver()));
        policy.World = world;
        Assert.True(world.TryAddBoid(new Vec2(50f, 50f), new Vec2(1f, 0f)));
        Boid before = world.Boids[0];

        Assert.Throws<InvalidOperationException>(() => world.Step(settings.FixedDeltaTime));

        Assert.Equal(1, world.Count);
        Assert.Equal(0ul, world.TickCount);
        EqualBits(before.Position, world.Boids[0].Position);
        EqualBits(before.Velocity, world.Boids[0].Velocity);
    }

    [Fact]
    public void RejectedLegacyRegistrationDoesNotConsumeAnIdOrChangeThePipeline()
    {
        CanonicalWorldSettings settings = Settings();
        var policy = new RegisteringPolicy();
        var world = Create(settings, new CanonicalPipeline([policy], new PriorityForceBudgetResolver()));
        policy.World = world;
        Assert.True(world.TryAddBoid(new Vec2(50f, 50f), new Vec2(1f, 0f)));

        Assert.Throws<InvalidOperationException>(() => world.Step(settings.FixedDeltaTime));

        Assert.Single(world.Policies);
        Assert.Equal(0ul, world.TickCount);
        world.SetPolicyEnabled(policy.Metadata.Id, false);
        world.AddRule(new ConstantLegacyForce(Vec2.Zero));
        Assert.Equal("legacy-rule-0", world.Policies[1].Id);
        world.Step(settings.FixedDeltaTime);
        Assert.Equal(1ul, world.TickCount);
    }

    private static CanonicalWorldSettings Settings() => new()
    {
        InitialCapacity = 8, WorldWidth = 100f, WorldHeight = 100f,
        TargetSpeed = 2f, MaxForce = 1f, SenseRadius = 10f, SeparationRadius = 2f,
        FieldOfView = 360f, WanderStrength = 0f, Seed = 42u
    };

    private static CanonicalWorld Create(CanonicalWorldSettings settings) =>
        new(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));

    private static CanonicalWorld Create(CanonicalWorldSettings settings, CanonicalPipeline pipeline) =>
        new(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight), pipeline);

    private static CanonicalWorldSettings ClassicSettings() => new()
    {
        Mode = CanonicalMode.ClassicReynolds, InitialCapacity = 3, WorldWidth = 100f, WorldHeight = 100f,
        TargetSpeed = 2f, MaxForce = 0.2f, SenseRadius = 10f, SeparationRadius = 5f, FieldOfView = 360f,
        // Structural inactivity is stronger than merely setting these enhancement knobs to zero.
        WanderStrength = 10f, WhiskerWeight = 10f, WhiskerTimeHorizon = 10f, MaxTurnRateDegPerSecond = 0.01f
    };

    private static CanonicalWorld ClassicWorld(CanonicalWorldSettings settings)
    {
        var world = Create(settings);
        Assert.True(world.TryAddBoid(new Vec2(50f, 50f), new Vec2(1f, 0f)));
        Assert.True(world.TryAddBoid(new Vec2(51f, 51f), new Vec2(-1f, 0f)));
        Assert.True(world.TryAddBoid(new Vec2(49f, 53f), new Vec2(0f, 1f)));
        return world;
    }

    private static void EqualBits(Vec2 expected, Vec2 actual)
    {
        Assert.Equal(BitConverter.SingleToInt32Bits(expected.X), BitConverter.SingleToInt32Bits(actual.X));
        Assert.Equal(BitConverter.SingleToInt32Bits(expected.Y), BitConverter.SingleToInt32Bits(actual.Y));
    }

    private sealed class ConstantPolicy(string id, Vec2 force) : ILocalSteeringPolicy
    {
        public PolicyMetadata Metadata { get; } = new(id);
        public int MaxIntents => 1;
        public void Emit(in LocalObservation observation, in LocalPolicyContext context, ref IntentWriter intents)
            => intents.AddForce(force);
    }

    private sealed class TaggedPolicy(PolicyMetadata metadata, Vec2 force) : ILocalSteeringPolicy
    {
        public PolicyMetadata Metadata => metadata;
        public int MaxIntents => 1;
        public void Emit(in LocalObservation observation, in LocalPolicyContext context, ref IntentWriter intents)
            => intents.AddForce(force);
    }

    private sealed class ObservationProbe : ILocalSteeringPolicy
    {
        public PolicyMetadata Metadata { get; } = new("observer");
        public int MaxIntents => 0;
        internal NeighborView[] Seen { get; private set; } = [];
        public void Emit(in LocalObservation observation, in LocalPolicyContext context, ref IntentWriter intents)
        {
            if (observation.Self.Group == 0) Seen = observation.Neighbors.ToArray();
        }
    }

    private sealed class ReplacingWriterPolicy(bool forgeMetadata) : ILocalSteeringPolicy
    {
        public PolicyMetadata Metadata { get; } = new("registered", weight: 0f);
        private readonly PolicyMetadata _forged = new("forged", SteeringRole.Separation, weight: 1f);
        private readonly SteeringIntent[] _foreign = new SteeringIntent[3];
        public int MaxIntents => 1;
        public void Emit(in LocalObservation observation, in LocalPolicyContext context, ref IntentWriter intents)
        {
            intents = new IntentWriter(forgeMetadata ? _forged : Metadata, _foreign);
            for (int i = 0; i < _foreign.Length; i++) intents.AddForce(new Vec2(0f, 1f));
        }
    }

    private sealed class ReentrantPolicy(bool spawn) : ILocalSteeringPolicy
    {
        public PolicyMetadata Metadata { get; } = new("reentrant");
        public int MaxIntents => 0;
        internal CanonicalWorld World { get; set; } = null!;
        public void Emit(in LocalObservation observation, in LocalPolicyContext context, ref IntentWriter intents)
        {
            if (spawn) World.TryAddBoid(new Vec2(60f, 60f), new Vec2(0f, 1f));
            else World.SetVelocity(0, new Vec2(0f, 1f));
        }
    }

    private sealed class RegisteringPolicy : ILocalSteeringPolicy
    {
        public PolicyMetadata Metadata { get; } = new("attempt-registration");
        public int MaxIntents => 0;
        internal CanonicalWorld World { get; set; } = null!;
        public void Emit(in LocalObservation observation, in LocalPolicyContext context, ref IntentWriter intents)
            => World.AddRule(new ConstantLegacyForce(Vec2.Zero));
    }

    private sealed class ConstantLegacyForce(Vec2 force) : IRule
    {
        public Vec2 Compute(int selfIndex, Boid self, ReadOnlySpan<Boid> boids,
            ReadOnlySpan<int> neighborIndices, ReadOnlySpan<float> neighborWeights, RuleContext context) => force;
    }
}

// Allocation assertions run without unrelated test collections so runner/runtime callbacks do not
// enter the measured thread. The warmup and measured physics loop remain identical to the probe.
[CollectionDefinition("Canonical composition allocations", DisableParallelization = true)]
public sealed class CanonicalCompositionAllocationCollection { }

[Collection("Canonical composition allocations")]
public sealed class CanonicalCompositionAllocationTests
{
    [Theory]
    [InlineData("policy")]
    [InlineData("resolver")]
    [InlineData("stage")]
    [InlineData("pipeline")]
    public void RetainingAComponentDoesNotKeepItsDiscardedWorldAlive(string retainedComponent)
    {
        (WeakReference<CanonicalWorld> weak, object retained) = MakeCollectibleWorld(retainedComponent);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(weak.TryGetTarget(out _));
        GC.KeepAlive(retained);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<CanonicalWorld>, object) MakeCollectibleWorld(string retainedComponent)
    {
        var settings = new CanonicalWorldSettings { InitialCapacity = 1, WorldWidth = 20f, WorldHeight = 20f };
        var policy = new NoOutputPolicy();
        var resolver = new PriorityForceBudgetResolver();
        var stage = new TargetSpeedStage();
        var pipeline = new CanonicalPipeline([policy], resolver, [stage]);
        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight), pipeline);
        object retained = retainedComponent switch
        {
            "policy" => policy, "resolver" => resolver, "stage" => stage, "pipeline" => pipeline,
            _ => throw new ArgumentException("Unknown retained component.", nameof(retainedComponent))
        };
        return (new WeakReference<CanonicalWorld>(world), retained);
    }

    private sealed class NoOutputPolicy : ILocalSteeringPolicy
    {
        public PolicyMetadata Metadata { get; } = new("retained");
        public int MaxIntents => 0;
        public void Emit(in LocalObservation observation, in LocalPolicyContext context, ref IntentWriter intents) { }
    }

    [Theory]
    [InlineData("enhanced")]
    [InlineData("classic")]
    [InlineData("empty")]
    public void BuiltInPipelinesAllocateZeroBytesOverOneThousandWarmTicks(string mode)
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 128, WorldWidth = 1000f, WorldHeight = 1000f, SenseRadius = 25f,
            FieldOfView = 360f, TargetSpeed = 5f, WanderStrength = 0.25f
        };
        CanonicalPipeline pipeline = mode switch
        {
            "classic" => CanonicalPipeline.ClassicReynolds(settings),
            "empty" => CanonicalPipeline.Empty(),
            _ => CanonicalPipeline.Enhanced(settings)
        };
        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight), pipeline);
        for (int i = 0; i < 128; i++)
            Assert.True(world.TryAddBoid(new Vec2(10f + (i % 16) * 45f, 10f + (i / 16) * 45f), new Vec2(1f, 0.3f)));
        for (int i = 0; i < 100; i++) world.Step(settings.FixedDeltaTime);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) world.Step(settings.FixedDeltaTime);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

}
