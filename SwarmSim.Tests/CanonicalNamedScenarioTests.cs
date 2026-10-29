using SwarmSim.Core.Canonical;
using Xunit.Abstractions;

namespace SwarmSim.Tests;

/// <summary>Milestone 6 coverage using the supported named classic composition.</summary>
public sealed class CanonicalNamedScenarioTests(ITestOutputHelper output)
{
    private static readonly string[] RuleIds =
        [CanonicalPolicyIds.Separation, CanonicalPolicyIds.Alignment, CanonicalPolicyIds.Cohesion];

    [Theory]
    [InlineData(CanonicalPolicyIds.Separation)]
    [InlineData(CanonicalPolicyIds.Alignment)]
    [InlineData(CanonicalPolicyIds.Cohesion)]
    public void NamedIsolationMatchesTheRuleLevelUpdateOverMultipleTicks(string enabled)
    {
        // All four agents remain inside the full-circle, untruncated neighborhood. This checks
        // the named enable surface all the way through arbitration/integration, not just Emit.
        CanonicalWorldSettings settings = Settings(force: 0.4f, separation: 20f);
        CanonicalWorld world = Create(settings, enabled);
        Add(world, new Vec2(60f, 55f), new Vec2(1f, 0.2f));
        Add(world, new Vec2(61f, 55.5f), new Vec2(-0.4f, 1f));
        Add(world, new Vec2(57f, 58f), new Vec2(0.7f, -0.6f));
        Add(world, new Vec2(65f, 57f), new Vec2(1f, -0.3f));
        IRule rule = enabled switch
        {
            CanonicalPolicyIds.Separation => new SeparationRule(settings.SeparationWeight, settings.SeparationRadius),
            CanonicalPolicyIds.Alignment => new AlignmentRule(settings.AlignmentWeight),
            _ => new CohesionRule(settings.CohesionWeight)
        };
        var context = new RuleContext(settings.TargetSpeed, settings.MaxForce, settings.SenseRadius,
            -1f, settings.FixedDeltaTime, 1f, worldWidth: settings.WorldWidth, worldHeight: settings.WorldHeight);
        float[] weights = [1f, 1f, 1f];
        bool contributionObserved = false;
        for (int tick = 0; tick < 120; tick++)
        {
            Boid[] before = world.Boids.ToArray();
            var expectedVelocity = new Vec2[before.Length];
            var expectedPosition = new Vec2[before.Length];
            var expectedForce = new Vec2[before.Length];
            for (int i = 0; i < before.Length; i++)
            {
                int[] neighbors = Enumerable.Range(0, before.Length).Where(index => index != i).ToArray();
                foreach (int index in neighbors)
                    Assert.True((before[index].Position - before[i].Position).Length < settings.SenseRadius);
                expectedForce[i] = rule.Compute(i, before[i], before, neighbors, weights, context)
                    .ClampMagnitude(settings.MaxForce);
                expectedVelocity[i] = (before[i].Velocity + expectedForce[i] * settings.FixedDeltaTime)
                    .WithLength(settings.TargetSpeed);
                expectedPosition[i] = before[i].Position + expectedVelocity[i] * settings.FixedDeltaTime;
                Assert.InRange(expectedPosition[i].X, 10f, settings.WorldWidth - 10f);
                Assert.InRange(expectedPosition[i].Y, 10f, settings.WorldHeight - 10f);
                contributionObserved |= expectedForce[i].LengthSquared > 0.01f;
            }

            world.Step(settings.FixedDeltaTime);

            AssertOnlyRule(world, enabled);
            Assert.Equal(0, world.Instrumentation.TruncatedCount);
            for (int i = 0; i < world.Count; i++)
            {
                EqualBits(expectedVelocity[i], world.Boids[i].Velocity);
                EqualBits(expectedPosition[i], world.Boids[i].Position);
                Assert.Equal(expectedForce[i].LengthSquared, world.Instrumentation.SteeringMagnitudesSquared[i]);
                Assert.Equal(3, world.Instrumentation.NeighborCounts[i]);
            }
        }
        Assert.True(contributionObserved, "The isolated-rule comparison must contain active steering.");
    }

    [Fact]
    public void SeparationOnlyKeepsAnApproachingOffsetPairClearAndThenSeparatesIt()
    {
        // A one-unit lateral offset makes the head-on encounter non-collinear. With separation
        // disabled, straight-line motion reaches exactly one unit of clearance. The bound below
        // requires an actual avoidance margin, so passing through and separating later cannot pass.
        CanonicalWorldSettings settings = Settings(force: 12f, separation: 8f);
        CanonicalWorld world = Create(settings, CanonicalPolicyIds.Separation);
        Add(world, new Vec2(60f, 55f), new Vec2(1f, 0f));
        Add(world, new Vec2(66f, 56f), new Vec2(-1f, 0f));
        float before = Distance(world.Boids[0].Position, world.Boids[1].Position, settings);
        float closest = before;
        for (int tick = 0; tick < 360; tick++)
        {
            world.Step(settings.FixedDeltaTime);
            AssertOnlyRule(world, CanonicalPolicyIds.Separation);
            closest = MathF.Min(closest, Distance(world.Boids[0].Position, world.Boids[1].Position, settings));
        }
        float after = Distance(world.Boids[0].Position, world.Boids[1].Position, settings);
        output.WriteLine($"Separation only: closest={closest:R}, initial={before:R}, final={after:R}.");
        Assert.True(closest > 1.5f, $"Closest approach {closest} must exceed straight-line clearance 1 by at least 0.5.");
        Assert.True(after > before, $"The pair must be farther apart after the encounter: {before} -> {after}.");
    }

    [Fact]
    public void AlignmentOnlyReducesTripletHeadingVariance()
    {
        CanonicalWorldSettings settings = Settings(force: 0.45f);
        CanonicalWorld world = Create(settings, CanonicalPolicyIds.Alignment);
        float[] degrees = [-60f, 0f, 60f];
        for (int i = 0; i < degrees.Length; i++)
        {
            float angle = degrees[i] * MathF.PI / 180f;
            Add(world, new Vec2(45f + i * 6f, 55f + i * 3f), new Vec2(MathF.Cos(angle), MathF.Sin(angle)));
        }
        float before = HeadingVariance(world);
        for (int tick = 0; tick < 300; tick++)
        {
            world.Step(settings.FixedDeltaTime);
            AssertOnlyRule(world, CanonicalPolicyIds.Alignment);
        }
        float after = HeadingVariance(world);
        output.WriteLine($"Alignment only: circular variance {before:R} -> {after:R}.");
        Assert.True(before > 0.3f);
        Assert.True(after <= before * 0.1f, $"Expected at least 90% heading-variance reduction: {before} -> {after}.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CohesionOnlyPullsTwoClustersTogetherWithAlignmentDisabled(bool acrossSeam)
    {
        CanonicalWorldSettings settings = Settings(force: 4f, separation: 0.1f, radius: 40f);
        CanonicalWorld world = Create(settings, CanonicalPolicyIds.Cohesion);
        float left = acrossSeam ? 15f : 49f;
        float right = acrossSeam ? 113f : 79f;
        foreach (float center in new[] { left, right })
            foreach (float offset in new[] { -1f, 0f, 1f })
                Add(world, new Vec2(center, 48f + offset), new Vec2(0f, 1f));
        float before = ClusterDistance(world);
        Assert.Equal(30f, before);
        for (int tick = 0; tick < 180; tick++)
        {
            world.Step(settings.FixedDeltaTime);
            AssertOnlyRule(world, CanonicalPolicyIds.Cohesion);
        }
        float after = ClusterDistance(world);
        output.WriteLine($"Cohesion only (seam={acrossSeam}): center distance {before:R} -> {after:R}.");
        Assert.True(after <= before * 0.8f, $"Expected at least 20% cluster approach: {before} -> {after}.");
    }

    [Fact]
    public void ClassicCombinedFlockRemainsFiniteAndAtTargetSpeedWhileEveryRuleContributes()
    {
        CanonicalWorldSettings settings = Settings(force: 1.5f, separation: 6f, radius: 18f);
        CanonicalWorld world = Create(settings);
        for (int row = 0; row < 4; row++)
            for (int column = 0; column < 4; column++)
            {
                float angle = (row * 4 + column) * 0.61f;
                Add(world, new Vec2(45f + column * 3f, 40f + row * 3f),
                    new Vec2(MathF.Cos(angle), MathF.Sin(angle)));
            }
        bool separation = false, alignment = false, cohesion = false;
        float minimum = float.PositiveInfinity, maximum = 0f;
        for (int tick = 0; tick < 1200; tick++)
        {
            world.Step(settings.FixedDeltaTime);
            AssertNoEnhancedStages(world);
            foreach (string id in RuleIds) Assert.Equal(world.Count, world.GetPolicyExecutionCount(id));
            Assert.Equal(0, world.Instrumentation.TruncatedCount);
            separation |= world.Instrumentation.AverageSeparationMagnitude > 0f;
            alignment |= world.Instrumentation.AverageAlignmentMagnitude > 0f;
            cohesion |= world.Instrumentation.AverageCohesionMagnitude > 0f;
            foreach (Boid boid in world.Boids)
            {
                Assert.True(float.IsFinite(boid.Position.X) && float.IsFinite(boid.Position.Y));
                Assert.True(float.IsFinite(boid.Velocity.X) && float.IsFinite(boid.Velocity.Y));
                float speed = boid.Velocity.Length;
                Assert.InRange(speed, settings.TargetSpeed - 1e-5f, settings.TargetSpeed + 1e-5f);
                minimum = MathF.Min(minimum, speed);
                maximum = MathF.Max(maximum, speed);
            }
        }
        output.WriteLine($"Classic combined: speed range [{minimum:R}, {maximum:R}] over 1200 ticks.");
        Assert.True(separation && alignment && cohesion, "All three rule contributions must occur in the interacting scene.");
    }

    private static CanonicalWorldSettings Settings(float force, float separation = 1.5f, float radius = 60f) => new()
    {
        Mode = CanonicalMode.ClassicReynolds, InitialCapacity = 32,
        WorldWidth = 128f, WorldHeight = 96f, TargetSpeed = 2f,
        MaxForce = force, SenseRadius = radius, SeparationRadius = separation,
        FieldOfView = 360f, SeparationWeight = 1.5f, AlignmentWeight = 1f, CohesionWeight = 1f,
        WanderStrength = 0f, MaxNeighbors = 32, Seed = 41u
    };

    private static CanonicalWorld Create(CanonicalWorldSettings settings, string? enabled = null)
    {
        var world = new CanonicalWorld(settings,
            new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        if (enabled is not null)
            foreach (string id in RuleIds) world.SetPolicyEnabled(id, id == enabled);
        return world;
    }

    private static void Add(CanonicalWorld world, Vec2 position, Vec2 velocity)
        => Assert.True(world.TryAddBoid(position, velocity));

    private static void AssertOnlyRule(CanonicalWorld world, string enabled)
    {
        foreach (string id in RuleIds)
            Assert.Equal(id == enabled ? world.Count : 0, world.GetPolicyExecutionCount(id));
        if (enabled != CanonicalPolicyIds.Separation) Assert.Equal(0f, world.Instrumentation.AverageSeparationMagnitude);
        if (enabled != CanonicalPolicyIds.Alignment) Assert.Equal(0f, world.Instrumentation.AverageAlignmentMagnitude);
        if (enabled != CanonicalPolicyIds.Cohesion) Assert.Equal(0f, world.Instrumentation.AverageCohesionMagnitude);
        AssertNoEnhancedStages(world);
    }

    private static void AssertNoEnhancedStages(CanonicalWorld world)
    {
        Assert.Equal(0, world.GetPolicyExecutionCount(CanonicalPolicyIds.Whisker));
        Assert.Equal(0, world.GetPolicyExecutionCount(CanonicalPolicyIds.Wander));
        Assert.Equal(0, world.GetStageExecutionCount(CanonicalStageIds.Priority));
        Assert.Equal(0, world.GetStageExecutionCount(CanonicalStageIds.ShapedAvoidance));
        Assert.Equal(0, world.GetStageExecutionCount(CanonicalStageIds.TurnRate));
        Assert.Equal(world.Count, world.GetStageExecutionCount(CanonicalStageIds.TargetSpeed));
    }

    private static float Distance(Vec2 a, Vec2 b, CanonicalWorldSettings settings)
        => Vec2.MinimumImageDelta(a, b, settings.WorldWidth, settings.WorldHeight).Length;

    private static float HeadingVariance(CanonicalWorld world)
    {
        Vec2 sum = Vec2.Zero;
        foreach (Boid boid in world.Boids) sum += boid.Velocity.Normalized;
        return 1f - sum.Length / world.Count;
    }

    private static float ClusterDistance(CanonicalWorld world)
    {
        Vec2 Center(int first)
        {
            Vec2 anchor = world.Boids[first].Position;
            Vec2 total = Vec2.Zero;
            for (int i = first; i < first + 3; i++)
                total += Vec2.MinimumImageDelta(anchor, world.Boids[i].Position,
                    world.Settings.WorldWidth, world.Settings.WorldHeight);
            return anchor + total / 3f;
        }
        return Distance(Center(0), Center(3), world.Settings);
    }

    private static void EqualBits(Vec2 expected, Vec2 actual)
    {
        Assert.Equal(BitConverter.SingleToInt32Bits(expected.X), BitConverter.SingleToInt32Bits(actual.X));
        Assert.Equal(BitConverter.SingleToInt32Bits(expected.Y), BitConverter.SingleToInt32Bits(actual.Y));
    }
}
