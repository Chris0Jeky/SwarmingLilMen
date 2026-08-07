using SwarmSim.Core.Canonical;
using SwarmSim.Core.Utils;
using Xunit;
using Xunit.Abstractions;

namespace SwarmSim.Tests;

/// <summary>
/// Guards the <see cref="CanonicalWorldSettings.MaxForce"/> invariant: the total composed steering
/// magnitude one boid integrates in one tick never exceeds MaxForce, no matter which combination of
/// whisker avoidance, separation, alignment, cohesion and wander produced it (issue #19).
///
/// The composed steering vector is a method local inside <see cref="CanonicalWorld.Step"/> that is
/// then reshaped by avoidance blending and renormalized to the allowed speed, so no assertion on
/// <see cref="Boid.Velocity"/> can measure it. These tests read
/// <see cref="RuleInstrumentation.SteeringMagnitudesSquared"/>, which records the composed vector
/// immediately before integration.
///
/// Deliberately NOT tagged [Trait("Category", "Performance")]: CI filters that category out, which
/// would silently skip the whole point of these tests.
/// </summary>
public class CanonicalSteeringBudgetTests
{
    /// <summary>Tolerance from issue #19's acceptance criteria: ||steering|| &lt;= MaxForce * (1 + 1e-4).</summary>
    private const float BudgetTolerance = 1e-4f;

    private readonly ITestOutputHelper _output;

    public CanonicalSteeringBudgetTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Tight two-agent anchor on the exact defect: whisker avoidance spent from the per-tick
    /// remainder while separation clamped to a fresh MaxForce and added on top.
    ///
    /// Measured on this geometry with MaxForce = 1: before the fix the composed steering was
    /// 1.7620 (ratio 1.7620); after it, exactly 1.0000. The whisker path saturates the budget
    /// first here, so the post-fix separation contribution is zero by design - the control world
    /// below proves separation genuinely wants force in this same geometry, which is what makes
    /// the 1.0000 an enforced ceiling rather than a geometry accident.
    /// </summary>
    [Fact]
    public void CanonicalWorld_WhiskerAndSeparation_ShareOneForceBudget()
    {
        const float deltaTime = 1f / 60f;

        CanonicalWorld world = BuildTwoAgentWorld(whiskerTimeHorizon: 0.4f);
        world.Step(deltaTime);

        float composed = MathF.Sqrt(world.Instrumentation.SteeringMagnitudesSquared[0]);
        CanonicalWorld.PerceptionSnapshot snapshot = world.CapturePerceptionSnapshot();
        _output.WriteLine($"composed={composed:F6} ratio={composed / world.Settings.MaxForce:F6} whiskerHits={snapshot.WhiskerCounts[0]}");

        // Control: the same geometry with the whisker look-ahead collapsed below the neighbor
        // distance, so only separation can spend. It proves separation is a live contributor here.
        CanonicalWorld separationOnly = BuildTwoAgentWorld(whiskerTimeHorizon: 0f);
        separationOnly.Step(deltaTime);
        Assert.True(
            separationOnly.TryGetMetrics(0, out RuleInstrumentation.Metrics controlMetrics),
            "control world must expose metrics for agent 0");
        _output.WriteLine($"control(separation only) separationMagnitude={controlMetrics.SeparationMagnitude:F6}");

        Assert.Equal(0, separationOnly.CapturePerceptionSnapshot().WhiskerCounts[0]);
        Assert.True(
            controlMetrics.SeparationMagnitude > 0f,
            "separation must produce force in this geometry, otherwise the budget test is vacuous");
        Assert.True(
            snapshot.WhiskerCounts[0] > 0,
            "whisker avoidance must engage in this geometry, otherwise the budget test is vacuous");

        float maxAllowed = world.Settings.MaxForce * (1f + BudgetTolerance);
        Assert.True(
            composed <= maxAllowed,
            $"composed steering {composed:F6} exceeded the MaxForce budget {world.Settings.MaxForce:F6} (ratio {composed / world.Settings.MaxForce:F6})");

        // The budget is not merely respected, it is fully spent: both contributors want far more
        // force than MaxForce, so the enforced total sits at the ceiling.
        Assert.True(
            composed >= world.Settings.MaxForce * 0.99f,
            $"composed steering {composed:F6} should saturate the MaxForce budget {world.Settings.MaxForce:F6}");
    }

    /// <summary>
    /// Issue #19's dense-crowd acceptance criterion: 200 agents in a 100x100 region for 300 ticks,
    /// no per-agent per-tick steering magnitude above MaxForce * (1 + 1e-4).
    /// Measured on this scenario before the fix: max composed steering 5.000000 against a MaxForce
    /// of 2.5 - a ratio of exactly 2.0000, the worst case the defect allows - with 43588 of 60000
    /// agent-ticks over budget. After the fix: max 2.500000, ratio 1.000000, 0 over budget.
    /// </summary>
    [Fact]
    public void CanonicalWorld_DenseCrowd_NeverExceedsMaxForceBudget()
    {
        const int agentCount = 200;
        const int tickCount = 300;
        const float deltaTime = 1f / 60f;

        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = agentCount,
            TargetSpeed = 12f,
            MaxForce = 2.5f,
            SenseRadius = 18f,
            FieldOfView = 270f,
            MaxNeighbors = 32,
            SeparationRadius = 8f,
            SeparationWeight = 1.5f,
            AlignmentWeight = 1f,
            CohesionWeight = 1f,
            WanderStrength = 0.3f,
            WorldWidth = 100f,
            WorldHeight = 100f,
            Seed = 20260807u
        };

        CanonicalWorld world = CreateWorld(settings);
        var rng = new Rng(settings.Seed);
        for (int i = 0; i < agentCount; i++)
        {
            var position = new Vec2(
                rng.NextFloat(0f, settings.WorldWidth),
                rng.NextFloat(0f, settings.WorldHeight));
            (float vx, float vy) = rng.NextUnitVector();
            Assert.True(world.TryAddBoid(position, new Vec2(vx, vy)));
        }

        // Count against the same tolerated ceiling the assertion uses. A bare MaxForce^2 compare
        // also flags agent-ticks that land a few ULPs high purely from summing normalized
        // contributions in float, which is rounding noise and not a budget violation.
        float budgetCeiling = settings.MaxForce * (1f + BudgetTolerance);
        float budgetCeilingSq = budgetCeiling * budgetCeiling;
        float maxObservedSq = 0f;
        int overBudgetAgentTicks = 0;
        int totalAgentTicks = 0;

        for (int tick = 0; tick < tickCount; tick++)
        {
            world.Step(deltaTime);
            ReadOnlySpan<float> steeringSq = world.Instrumentation.SteeringMagnitudesSquared;
            for (int i = 0; i < steeringSq.Length; i++)
            {
                totalAgentTicks++;
                if (steeringSq[i] > maxObservedSq)
                    maxObservedSq = steeringSq[i];
                if (steeringSq[i] > budgetCeilingSq)
                    overBudgetAgentTicks++;
            }
        }

        float maxObserved = MathF.Sqrt(maxObservedSq);
        float ratio = maxObserved / settings.MaxForce;
        _output.WriteLine(
            $"maxSteering={maxObserved:F6} maxForce={settings.MaxForce:F6} ratio={ratio:F6} " +
            $"overBudgetAgentTicks={overBudgetAgentTicks}/{totalAgentTicks}");

        Assert.Equal(agentCount * tickCount, totalAgentTicks);
        Assert.True(maxObserved > 0f, "the scenario must actually generate steering force");
        Assert.Equal(0, overBudgetAgentTicks);
        Assert.True(
            maxObserved <= budgetCeiling,
            $"max composed steering {maxObserved:F6} exceeded MaxForce {settings.MaxForce:F6} (ratio {ratio:F6}); " +
            $"{overBudgetAgentTicks} of {totalAgentTicks} agent-ticks were over budget");
    }

    /// <summary>
    /// Over-constraint guard: enforcing one shared budget must not stop the flock forming.
    ///
    /// Issue #19 asks for "polarization &gt; 0.7 after 600 ticks in an open-field scenario". Read
    /// naively as a uniformly scattered field with balanced weights, that number is NOT reachable
    /// on this engine - it tops out around 0.49 on UNFIXED code, so such a test would fail both
    /// before and after the fix and prove nothing. This uses an alignment-dominant blob start,
    /// where the criterion is genuinely reachable, and averages over six pinned seeds rather than
    /// resting the guard on one lucky initial condition: this scenario forms one flock on most
    /// seeds and splits into competing sub-flocks on some.
    ///
    /// Measured mean polarization over these six seeds: 0.8926 before the fix, 0.8913 after it
    /// (per-seed post-fix: 0.9891 / 0.9624 / 0.9469 / 0.9693 / 0.4944 / 0.9855 - seed 20260807 is
    /// the sub-flock case, both before and after). The thresholds below are set from those
    /// measurements; the fix was not tuned to reach them.
    /// </summary>
    [Fact]
    public void CanonicalWorld_OpenField_StillFlocksAfterBudgetFix()
    {
        uint[] seeds = { 1u, 7u, 42u, 4242u, 20260807u, 99999u };

        float polarizationSum = 0f;
        int flockedSeeds = 0;
        foreach (uint seed in seeds)
        {
            (float initial, float final) = RunOpenFieldFlock(seed);
            _output.WriteLine($"seed={seed} initialPolarization={initial:F4} finalPolarization={final:F4}");

            Assert.True(
                initial < 0.3f,
                $"seed {seed} must start unaligned for this to measure flocking, got {initial:F4}");

            polarizationSum += final;
            if (final > 0.7f)
                flockedSeeds++;
        }

        float meanPolarization = polarizationSum / seeds.Length;
        _output.WriteLine($"meanPolarization={meanPolarization:F4} flockedSeeds={flockedSeeds}/{seeds.Length}");

        Assert.True(
            meanPolarization > 0.7f,
            $"flocking degraded after the budget fix: mean polarization {meanPolarization:F4} over {seeds.Length} seeds");
        Assert.True(
            flockedSeeds >= 4,
            $"only {flockedSeeds} of {seeds.Length} seeds reached polarization 0.7 after the budget fix");
    }

    /// <summary>
    /// Alignment-dominant open field: 100 agents released as an unaligned blob at the centre of a
    /// 400x400 toroidal world, run for 600 ticks. Returns polarization before and after the run.
    /// </summary>
    private static (float Initial, float Final) RunOpenFieldFlock(uint seed)
    {
        const int agentCount = 100;
        const int tickCount = 600;
        const float deltaTime = 1f / 60f;
        const float blobRadius = 40f;

        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = agentCount,
            TargetSpeed = 20f,
            MaxForce = 25f,
            SenseRadius = 60f,
            FieldOfView = 300f,
            MaxNeighbors = 32,
            SeparationRadius = 2f,
            SeparationWeight = 0.5f,
            AlignmentWeight = 3f,
            CohesionWeight = 0.5f,
            WanderStrength = 0f,
            WorldWidth = 400f,
            WorldHeight = 400f,
            Seed = seed
        };

        CanonicalWorld world = CreateWorld(settings);
        var rng = new Rng(seed);
        float centerX = settings.WorldWidth * 0.5f;
        float centerY = settings.WorldHeight * 0.5f;
        for (int i = 0; i < agentCount; i++)
        {
            (float offsetX, float offsetY) = rng.NextPointInCircle(blobRadius);
            (float velocityX, float velocityY) = rng.NextUnitVector();
            Assert.True(world.TryAddBoid(
                new Vec2(centerX + offsetX, centerY + offsetY),
                new Vec2(velocityX, velocityY)));
        }

        float initial = Polarization(world);
        for (int tick = 0; tick < tickCount; tick++)
        {
            world.Step(deltaTime);
        }

        return (initial, Polarization(world));
    }

    /// <summary>
    /// Two agents on a near head-on approach: agent 1 sits inside agent 0's whisker corridor
    /// (3 units ahead, 0.5 to the side) and inside its separation radius, so both contributors
    /// demand far more than MaxForce in the same tick.
    /// </summary>
    private static CanonicalWorld BuildTwoAgentWorld(float whiskerTimeHorizon)
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            TargetSpeed = 10f,
            MaxForce = 1f,
            SenseRadius = 20f,
            FieldOfView = 360f,
            SeparationRadius = 5f,
            WhiskerTimeHorizon = whiskerTimeHorizon
        };

        CanonicalWorld world = CreateWorld(settings);
        Assert.True(world.TryAddBoid(new Vec2(500f, 500f), new Vec2(1f, 0f)));
        Assert.True(world.TryAddBoid(new Vec2(503f, 500.5f), new Vec2(1f, 0f)));
        return world;
    }

    [Fact]
    public void CanonicalWorld_SteeringSeam_CapturesContributionsAddedAfterTheRuleBlock()
    {
        // Every other assertion in this file is an UPPER bound on the recorded steering, so a seam
        // that under-reports would make all of them vacuous rather than failing. Moving the
        // RecordSteering call above the wander block survives the whole suite otherwise. A lone
        // boid with no neighbours receives force from wander and nothing else, so a seam placed
        // before wander records exactly zero here.
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            WorldWidth = 1000f,
            WorldHeight = 1000f,
            TargetSpeed = 10f,
            MaxForce = 1f,
            SenseRadius = 20f,
            FieldOfView = 360f,
            WanderStrength = 1f,
            WanderRate = 1f,
            Seed = 20260808u
        };

        CanonicalWorld world = CreateWorld(settings);
        Assert.True(world.TryAddBoid(new Vec2(500f, 500f), new Vec2(1f, 0f)));

        world.Step(settings.FixedDeltaTime);

        ReadOnlySpan<float> recorded = world.Instrumentation.SteeringMagnitudesSquared;
        Assert.Equal(1, recorded.Length);
        Assert.True(
            recorded[0] > 0f,
            $"Wander-only steering must reach the seam; recorded {recorded[0]}. " +
            "A zero here means RecordSteering runs before every contribution has landed.");
    }

    [Fact]
    public void CanonicalWorld_SeparationWithholdsTheRemainingBudgetFromLaterRules()
    {
        // The fix's comment claims separation's priority semantics are unchanged: once separation
        // spends, the remainder is withheld from alignment, cohesion and wander rather than shared.
        // Nothing pinned that -- deleting `remainingForce = 0f` leaves the whole suite green while
        // alignment and cohesion silently start contributing. This geometry gives separation a
        // small share of a large budget, so a leaked remainder is unmistakable.
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            WorldWidth = 1000f,
            WorldHeight = 1000f,
            TargetSpeed = 10f,
            MaxForce = 50f,
            SenseRadius = 40f,
            SeparationRadius = 20f,
            FieldOfView = 360f,
            WhiskerWeight = 0f,
            WanderStrength = 0f,
            AlignmentWeight = 1f,
            CohesionWeight = 1f,
            Seed = 20260808u
        };

        CanonicalWorld world = CreateWorld(settings);
        Assert.True(world.TryAddBoid(new Vec2(500f, 500f), new Vec2(1f, 0f)));
        Assert.True(world.TryAddBoid(new Vec2(515f, 500f), new Vec2(-1f, 0f)));

        world.Step(settings.FixedDeltaTime);

        Assert.True(world.Instrumentation.TryGetMetrics(0, out RuleInstrumentation.Metrics metrics));
        Assert.True(
            metrics.SeparationMagnitude > 0f,
            $"Precondition: separation must fire in this geometry; got {metrics.SeparationMagnitude}.");
        Assert.Equal(0f, metrics.AlignmentMagnitude);
        Assert.Equal(0f, metrics.CohesionMagnitude);
    }

    private static CanonicalWorld CreateWorld(CanonicalWorldSettings settings) =>
        new(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));

    /// <summary>Mean-heading order parameter: 0 is fully scattered, 1 is perfectly aligned.</summary>
    private static float Polarization(CanonicalWorld world)
    {
        ReadOnlySpan<Boid> boids = world.Boids;
        if (boids.Length == 0)
            return 0f;

        float sumX = 0f;
        float sumY = 0f;
        for (int i = 0; i < boids.Length; i++)
        {
            Vec2 heading = boids[i].Forward;
            sumX += heading.X;
            sumY += heading.Y;
        }

        return new Vec2(sumX / boids.Length, sumY / boids.Length).Length;
    }
}
