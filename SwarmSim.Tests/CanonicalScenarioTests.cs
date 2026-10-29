using SwarmSim.Core.Canonical;
using Xunit.Abstractions;

namespace SwarmSim.Tests;

/// <summary>
/// Multi-tick behavioral scenarios for the canonical boids milestones.
/// Initial states and horizons are explicit. There is no wall clock.
/// </summary>
public class CanonicalScenarioTests
{
    private readonly ITestOutputHelper _output;

    public CanonicalScenarioTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Separation_HeadOnClosestApproachStaysAbovePassThrough()
    {
        // Milestone 3 (separation). Two boids on a head-on collision course, 7 units
        // apart, inside the separation radius (8). The straight-line minimum is 0.
        // Shaped avoidance alone still lets them close to about 4.55 over this horizon;
        // the separation steer holds the closest approach above 4.8 and finishes farther
        // apart (about 14 versus about 12.4). N = 16 ticks of 0.25s.
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            TargetSpeed = 1.5f,
            MaxForce = 12f,
            FieldOfView = 360f,
            SenseRadius = 30f,
            SeparationRadius = 8f,
            SeparationWeight = 1.5f,
            AlignmentWeight = 0f,
            CohesionWeight = 0f,
            SeparationPriorityRadiusFactor = 0f,
            SeparationPriorityExitFactor = 0f,
            SeparationPriorityBoost = 1f,
            SeparationSpeedDroop = 0f,
            WanderStrength = 0f,
            WhiskerTimeHorizon = 0f,
            WhiskerWeight = 0f,
            MaxTurnRateDegPerSecond = 1440f,
            WorldWidth = 800f,
            WorldHeight = 600f,
            Seed = 41u
        };

        var world = CreateWorld(settings);
        Assert.True(world.TryAddBoid(new Vec2(400f, 300f), new Vec2(1f, 0f)));
        Assert.True(world.TryAddBoid(new Vec2(407f, 300f), new Vec2(-1f, 0f)));

        const float dt = 0.25f;
        const int ticks = 16;
        float minimum = Distance(world);
        for (int i = 0; i < ticks; i++)
        {
            world.Step(dt);
            minimum = MathF.Min(minimum, Distance(world));
        }

        float after = Distance(world);
        _output.WriteLine($"Head-on: closest={minimum:R}, final={after:R}, ticks={ticks}.");
        Assert.True(minimum > 4.8f, $"Closest approach {minimum} should stay above 4.8.");
        Assert.True(after > 13.5f, $"Distance after {ticks} ticks was {after}, expected above 13.5.");
    }

    [Fact]
    public void Alignment_TripletReducesCircularHeadingVariance()
    {
        // Milestone 4: three different headings converge. Circular variance is 1 - the
        // mean resultant length, so it handles angle wrapping without an arbitrary mean angle.
        // Require at least 90% reduction over five simulated seconds, not a rounding-sized gain.
        // This is the current weighted default pipeline, not the named isolation promised by #27.
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 8,
            TargetSpeed = 2f,
            MaxForce = 0.45f,
            FieldOfView = 360f,
            SenseRadius = 40f,
            SeparationRadius = 1.5f,
            SeparationWeight = 1.5f,
            AlignmentWeight = 1f,
            CohesionWeight = 0f,
            SeparationPriorityRadiusFactor = 0f,
            WanderStrength = 0f,
            WhiskerTimeHorizon = 0f,
            WhiskerWeight = 0f,
            MaxTurnRateDegPerSecond = 720f,
            WorldWidth = 800f,
            WorldHeight = 600f,
            Seed = 41u
        };

        var world = CreateWorld(settings);
        float[] degrees = { -60f, 0f, 60f };
        for (int i = 0; i < degrees.Length; i++)
        {
            float radians = degrees[i] * MathF.PI / 180f;
            var position = new Vec2(480f + i * 6f, 400f + (i - 2) * 3f);
            var velocity = new Vec2(MathF.Cos(radians), MathF.Sin(radians));
            Assert.True(world.TryAddBoid(position, velocity));
        }

        float before = 1f - MeanResultantLength(world);
        const float dt = 1f / 60f;
        for (int i = 0; i < 300; i++)
            world.Step(dt);

        float after = 1f - MeanResultantLength(world);
        _output.WriteLine($"Alignment: circular variance {before:R} -> {after:R} over 300 ticks.");
        Assert.True(before > 0.3f, $"Initial circular variance {before} must make convergence observable.");
        Assert.True(after <= before * 0.1f, $"Circular variance {before} -> {after}; expected at least 90% reduction.");
    }

    [Theory]
    [InlineData(1f, 0f)]
    [InlineData(0f, 1f)]
    [InlineData(0.6f, 0.8f)]
    public void Alignment_IsolatedBoidKeepsVelocity(float headingX, float headingY)
    {
        // Milestone 4 (alignment). A boid with no neighbors has no alignment steer.
        // Integration renormalizes the same heading through trig round trips, so the assertion uses a
        // tolerance (1e-4 at speed 2 over 600 steps) rather than bit equality, which can differ
        // across platforms. Axis-aligned and oblique headings cover both trig paths.
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            TargetSpeed = 2f,
            MaxForce = 0.45f,
            FieldOfView = 360f,
            SenseRadius = 40f,
            SeparationRadius = 1.5f,
            AlignmentWeight = 1f,
            CohesionWeight = 0f,
            WanderStrength = 0f,
            WorldWidth = 800f,
            WorldHeight = 600f
        };

        var world = CreateWorld(settings);
        Assert.True(world.TryAddBoid(new Vec2(500f, 400f), new Vec2(headingX, headingY)));
        Vec2 before = world.Boids[0].Velocity;

        for (int i = 0; i < 600; i++)
        {
            world.Step(1f / 60f);
            Assert.Equal(0f, world.Instrumentation.SteeringMagnitudesSquared[0]);
            Assert.Equal(before.X, world.Boids[0].Velocity.X, 1e-4f);
            Assert.Equal(before.Y, world.Boids[0].Velocity.Y, 1e-4f);
        }

        Vec2 after = world.Boids[0].Velocity;
        Assert.Equal(before.X, after.X, 1e-4f);
        Assert.Equal(before.Y, after.Y, 1e-4f);
    }

    [Fact]
    public void Cohesion_LoneBoidMovesCloserToNeighbor()
    {
        // Milestone 5 (cohesion). One boid sits inside sense range (40) and outside
        // separation range (3) of a single same-heading neighbor, so initially only cohesion steers.
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            TargetSpeed = 2f,
            MaxForce = 3f,
            FieldOfView = 360f,
            SenseRadius = 40f,
            SeparationRadius = 3f,
            SeparationWeight = 1.5f,
            AlignmentWeight = 1f,
            CohesionWeight = 1f,
            SeparationPriorityRadiusFactor = 0f,
            WanderStrength = 0f,
            WhiskerTimeHorizon = 0f,
            WhiskerWeight = 0f,
            MaxTurnRateDegPerSecond = 720f,
            WorldWidth = 800f,
            WorldHeight = 600f,
            Seed = 41u
        };

        var world = CreateWorld(settings);
        Assert.True(world.TryAddBoid(new Vec2(500f, 400f), new Vec2(1f, 0f)));
        Assert.True(world.TryAddBoid(new Vec2(500f, 418f), new Vec2(1f, 0f)));
        float before = Distance(world);

        for (int i = 0; i < 180; i++)
            world.Step(1f / 60f);

        float after = Distance(world);
        _output.WriteLine($"Cohesion pair: distance {before:R} -> {after:R} over 180 ticks.");
        Assert.True(after <= before * 0.8f, $"Distance {before} -> {after}; expected at least 20% contraction.");
    }

    [Fact]
    public void Cohesion_ScatteredClusterRadiusContracts()
    {
        // Milestone 5 (cohesion). A rank of same-heading boids, far from the world edge.
        // Spacing (8) is outside the separation radius (2), so the radius to the centroid
        // contracts by cohesion rather than by a pass-through. This finite horizon stays
        // far from the edges, where the ordinary centroid is valid; seam geometry has its
        // own two-cluster case below.
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 8,
            TargetSpeed = 1.5f,
            MaxForce = 2.5f,
            FieldOfView = 360f,
            SenseRadius = 50f,
            SeparationRadius = 2f,
            SeparationWeight = 1.5f,
            AlignmentWeight = 1f,
            CohesionWeight = 1f,
            SeparationPriorityRadiusFactor = 0f,
            WanderStrength = 0f,
            WhiskerTimeHorizon = 0f,
            WhiskerWeight = 0f,
            MaxTurnRateDegPerSecond = 720f,
            WorldWidth = 800f,
            WorldHeight = 600f,
            Seed = 41u
        };

        var world = CreateWorld(settings);
        float[] offsets = { -16f, -8f, 0f, 8f, 16f };
        for (int i = 0; i < offsets.Length; i++)
            Assert.True(world.TryAddBoid(new Vec2(500f, 400f + offsets[i]), new Vec2(1f, 0f)));

        float before = ClusterRadius(world);
        // Six seconds permits the outer boids to turn and translate; the target is a
        // quarter-radius contraction, not an instantaneous heading change.
        for (int i = 0; i < 360; i++)
            world.Step(1f / 60f);

        float after = ClusterRadius(world);
        _output.WriteLine($"Cohesion rank: radius {before:R} -> {after:R} over 360 ticks.");
        Assert.True(after <= before * 0.75f, $"Cluster radius {before} -> {after}; expected at least 25% contraction.");
    }

    [Fact]
    public void CombinedRules_ExplicitInteractingFlockStaysWithinPrioritySpeedBounds()
    {
        // Default priority-mode stability, not classic-Reynolds composition. An explicit
        // layout puts a close pair among a loose 4x4 flock so this cannot pass as an empty
        // neighborhood test. The allowed speed lies between target*(1-droop) and target.
        // No initialization or wander RNG is involved; named-stream changes cannot move it.
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 32,
            TargetSpeed = 2f,
            MaxForce = 1.5f,
            FieldOfView = 270f,
            SenseRadius = 18f,
            SeparationRadius = 6f,
            SeparationWeight = 1.5f,
            AlignmentWeight = 1f,
            CohesionWeight = 1f,
            WanderStrength = 0f,
            Seed = 41u,
            WorldWidth = 800f,
            WorldHeight = 600f
        };

        var world = CreateWorld(settings);
        const int count = 16;
        Vec2[] headings = { new(1f, 0f), new(0f, 1f), new(-1f, 0f), new(0f, -1f) };
        for (int i = 0; i < count; i++)
        {
            var position = i == 1 ? new Vec2(352f, 250f) : new Vec2(350f + (i % 4) * 8f, 250f + (i / 4) * 8f);
            Assert.True(world.TryAddBoid(position, headings[i % headings.Length]));
        }

        const float speedTolerance = 1e-4f;
        float speedFloor = settings.TargetSpeed * (1f - settings.SeparationSpeedDroop) - speedTolerance;
        float speedCeiling = settings.TargetSpeed + speedTolerance;
        float dt = settings.FixedDeltaTime;
        bool separationObserved = false, alignmentObserved = false, cohesionObserved = false, droopObserved = false;
        for (int tick = 0; tick < 400; tick++)
        {
            world.Step(dt);
            separationObserved |= world.Instrumentation.AverageSeparationMagnitude > 0f;
            alignmentObserved |= world.Instrumentation.AverageAlignmentMagnitude > 0f;
            cohesionObserved |= world.Instrumentation.AverageCohesionMagnitude > 0f;
            for (int i = 0; i < world.Count; i++)
            {
                Boid boid = world.Boids[i];
                Assert.True(float.IsFinite(boid.Position.X) && float.IsFinite(boid.Position.Y), $"Non-finite position at tick {tick}, boid {i}.");
                Assert.True(float.IsFinite(boid.Velocity.X) && float.IsFinite(boid.Velocity.Y), $"Non-finite velocity at tick {tick}, boid {i}.");
                float speed = boid.Velocity.Length;
                Assert.True(speed >= speedFloor, $"Boid {i} speed {speed} is below the priority-mode floor {speedFloor} at tick {tick}.");
                Assert.True(speed <= speedCeiling, $"Boid {i} speed {speed} exceeds {speedCeiling} at tick {tick}.");
                droopObserved |= speed < settings.TargetSpeed - 0.01f;
            }
        }
        Assert.True(separationObserved && alignmentObserved && cohesionObserved,
            $"All three rules must contribute: separation={separationObserved}, alignment={alignmentObserved}, cohesion={cohesionObserved}.");
        Assert.True(droopObserved, "This scene must exercise the intentional default speed droop, not only the target-speed ceiling.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cohesion_TwoCompactClustersApproachAcrossTheShortestImage(bool acrossSeam)
    {
        // Milestone 5's two-cluster geometry. The current weighted pipeline has no named
        // rule-off switch, so this checks integrated cohesion, not alignment-off isolation.
        // Both centers are 30 units apart in the periodic metric and within sense range.
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 6, WorldWidth = 128f, WorldHeight = 96f,
            TargetSpeed = 2f, MaxForce = 4f, SenseRadius = 40f, SeparationRadius = 0.1f,
            FieldOfView = 360f, SeparationWeight = 1.5f, AlignmentWeight = 1f, CohesionWeight = 1f,
            SeparationPriorityRadiusFactor = 0f, SeparationPriorityExitFactor = 0f,
            WanderStrength = 0f, WhiskerTimeHorizon = 0f, WhiskerWeight = 0f,
            MaxTurnRateDegPerSecond = 720f, Seed = 41u
        };
        var world = CreateWorld(settings);
        float left = acrossSeam ? 15f : 49f;
        float right = acrossSeam ? 113f : 79f;
        foreach (float center in new[] { left, right })
            foreach (float offset in new[] { -1f, 0f, 1f })
                Assert.True(world.TryAddBoid(new Vec2(center, 48f + offset), new Vec2(0f, 1f)));

        float before = CenterDistance(world);
        Assert.Equal(30f, before);
        for (int tick = 0; tick < 180; tick++) world.Step(1f / 60f);
        float after = CenterDistance(world);
        _output.WriteLine($"Two clusters (seam={acrossSeam}): center distance {before:R} -> {after:R} over 180 ticks.");
        Assert.True(after <= before * 0.8f, $"Cluster centers {before} -> {after}; expected at least 20% contraction along the shortest image.");
    }

    [Fact]
    public void PriorityMode_DefaultDroopHasAFloorAndRecoversWhenThePairSeparates()
    {
        // Deliberate default speed semantics: priority reduces speed by at most 3%, then
        // releases it after separation. This is not the classic constant-speed acceptance.
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 2, TargetSpeed = 2f, MaxForce = 1.5f, SenseRadius = 18f,
            SeparationRadius = 6f, FieldOfView = 360f, WanderStrength = 0f,
            WorldWidth = 128f, WorldHeight = 96f
        };
        var world = CreateWorld(settings);
        Assert.True(world.TryAddBoid(new Vec2(60.5f, 48f), new Vec2(-1f, 0f)));
        Assert.True(world.TryAddBoid(new Vec2(62f, 48f), new Vec2(1f, 0f)));
        var telemetry = new CanonicalTelemetry(2);
        float floor = settings.TargetSpeed * (1f - settings.SeparationSpeedDroop);
        float minimumSpeed = settings.TargetSpeed;
        bool priorityObserved = false;
        for (int tick = 0; tick < 360; tick++)
        {
            world.Step(settings.FixedDeltaTime);
            world.CaptureTelemetryInto(telemetry);
            priorityObserved |= telemetry.SeparationPriorityTriggered;
            foreach (Boid boid in world.Boids)
            {
                float speed = boid.Velocity.Length;
                Assert.InRange(speed, floor - 1e-4f, settings.TargetSpeed + 1e-4f);
                minimumSpeed = MathF.Min(minimumSpeed, speed);
            }
        }
        _output.WriteLine($"Priority droop: minimum={minimumSpeed:R}, expected floor={floor:R}, final gap={Distance(world):R}.");
        Assert.True(priorityObserved, "The close pair must engage priority before leaving the threshold.");
        Assert.Equal(floor, minimumSpeed, 1e-4f);
        Assert.True(Distance(world) > settings.SeparationPriorityExitFactor * settings.SenseRadius);
        Assert.False(telemetry.SeparationPriorityTriggered);
        foreach (Boid boid in world.Boids) Assert.Equal(settings.TargetSpeed, boid.Velocity.Length, 1e-4f);
    }

    private static float CenterDistance(CanonicalWorld world)
    {
        Vec2 Center(int start)
        {
            Vec2 anchor = world.Boids[start].Position;
            Vec2 sum = Vec2.Zero;
            for (int i = start; i < start + 3; i++)
                sum += Vec2.MinimumImageDelta(anchor, world.Boids[i].Position, world.Settings.WorldWidth, world.Settings.WorldHeight);
            return anchor + sum / 3f;
        }
        return Vec2.MinimumImageDelta(Center(0), Center(3), world.Settings.WorldWidth, world.Settings.WorldHeight).Length;
    }

    private static CanonicalWorld CreateWorld(CanonicalWorldSettings settings)
        => new(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));

    private static float Distance(CanonicalWorld world)
    {
        CanonicalWorldSettings settings = world.Settings;
        return Vec2.MinimumImageDelta(world.Boids[0].Position, world.Boids[1].Position, settings.WorldWidth, settings.WorldHeight).Length;
    }

    private static float MeanResultantLength(CanonicalWorld world)
    {
        float x = 0f;
        float y = 0f;
        int count = world.Count;
        for (int i = 0; i < count; i++)
        {
            Vec2 heading = world.Boids[i].Velocity.Normalized;
            x += heading.X;
            y += heading.Y;
        }

        return MathF.Sqrt(x * x + y * y) / count;
    }

    private static float ClusterRadius(CanonicalWorld world)
    {
        CanonicalWorldSettings settings = world.Settings;
        float sumX = 0f;
        float sumY = 0f;
        int count = world.Count;
        for (int i = 0; i < count; i++)
        {
            sumX += world.Boids[i].Position.X;
            sumY += world.Boids[i].Position.Y;
        }

        var centroid = new Vec2(sumX / count, sumY / count);
        float radius = 0f;
        for (int i = 0; i < count; i++)
        {
            float distance = Vec2.MinimumImageDelta(centroid, world.Boids[i].Position, settings.WorldWidth, settings.WorldHeight).Length;
            radius = MathF.Max(radius, distance);
        }

        return radius;
    }
}
