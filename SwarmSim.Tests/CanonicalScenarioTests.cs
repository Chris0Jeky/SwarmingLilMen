using SwarmSim.Core.Canonical;
using SwarmSim.Core.Utils;

namespace SwarmSim.Tests;

/// <summary>
/// Multi-tick behavioral scenarios for the canonical boids milestones.
/// Initial states and horizons are explicit. There is no wall clock.
/// </summary>
public class CanonicalScenarioTests
{
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

        const float ballisticMinimum = 0f;
        const float dt = 0.25f;
        const int ticks = 16;
        float minimum = Distance(world);
        for (int i = 0; i < ticks; i++)
        {
            world.Step(dt);
            minimum = MathF.Min(minimum, Distance(world));
        }

        float after = Distance(world);
        Assert.True(after > ballisticMinimum, $"Head-on distance {after} should exceed the unsteered minimum {ballisticMinimum}.");
        Assert.True(minimum > 4.8f, $"Closest approach {minimum} should stay above 4.8.");
        Assert.True(after > 13.5f, $"Distance after {ticks} ticks was {after}, expected above 13.5.");
    }

    [Fact]
    public void Alignment_DistinctHeadingsIncreaseMeanResultantLength()
    {
        // Milestone 4 (alignment). A loose flock with distinct headings. Separation never
        // engages (neighbors start ~6 apart, radius 1.5). MaxForce is below a typical
        // alignment steer, so alignment spends the budget before the zero-weight cohesion slot.
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
        float[] degrees = { -40f, -15f, 10f, 35f, 70f };
        for (int i = 0; i < degrees.Length; i++)
        {
            float radians = degrees[i] * MathF.PI / 180f;
            var position = new Vec2(480f + i * 6f, 400f + (i - 2) * 3f);
            var velocity = new Vec2(MathF.Cos(radians), MathF.Sin(radians));
            Assert.True(world.TryAddBoid(position, velocity));
        }

        float before = MeanResultantLength(world);
        const float dt = 1f / 60f;
        for (int i = 0; i < 300; i++)
            world.Step(dt);

        float after = MeanResultantLength(world);
        Assert.True(after > before, $"Mean resultant length went from {before} to {after}.");
    }

    [Fact]
    public void Alignment_IsolatedBoidKeepsVelocity()
    {
        // Milestone 4 (alignment). A boid with no neighbors has no alignment steer.
        // Integration renormalizes the same heading through trig round trips, so the assertion uses a
        // tolerance (1e-4 over 90 steps) rather than bit equality, which can differ across platforms.
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
        Assert.True(world.TryAddBoid(new Vec2(500f, 400f), new Vec2(0.6f, 0.8f)));
        Vec2 before = world.Boids[0].Velocity;

        for (int i = 0; i < 90; i++)
            world.Step(1f / 60f);

        Vec2 after = world.Boids[0].Velocity;
        Assert.Equal(before.X, after.X, 1e-4f);
        Assert.Equal(before.Y, after.Y, 1e-4f);
    }

    [Fact]
    public void Cohesion_LoneBoidMovesCloserToNeighbor()
    {
        // Milestone 5 (cohesion). One boid sits inside sense range (40) and outside
        // separation range (3) of a single same-heading neighbor, so only cohesion steers.
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
        Assert.True(after < before, $"Distance went from {before} to {after}.");
    }

    [Fact]
    public void Cohesion_ScatteredClusterRadiusContracts()
    {
        // Milestone 5 (cohesion). A rank of same-heading boids, far from the world edge.
        // Spacing (8) is outside the separation radius (2), so the radius to the centroid
        // contracts by cohesion rather than by a pass-through. Minimum-image deltas keep
        // the measure toroidal if a later horizon wraps.
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
        for (int i = 0; i < 240; i++)
            world.Step(1f / 60f);

        float after = ClusterRadius(world);
        Assert.True(after < before, $"Cluster radius went from {before} to {after}.");
    }

    [Fact]
    public void CombinedRules_SeededFlockStaysFiniteAndMobile()
    {
        // Composition stability. All three rules come from CanonicalWorld's default
        // registration, using the settings weights, over a few hundred ticks of a flock
        // placed by the repo Rng. Speed is renormalized to the target (droop can only
        // lower it), so the upper bound is that target plus a small float tolerance.
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
        var rng = new Rng(settings.Seed);
        const int count = 16;
        for (int i = 0; i < count; i++)
        {
            float x = 300f + rng.NextFloat() * 200f;
            float y = 200f + rng.NextFloat() * 200f;
            float angle = rng.NextFloat() * MathF.PI * 2f;
            Assert.True(world.TryAddBoid(new Vec2(x, y), new Vec2(MathF.Cos(angle), MathF.Sin(angle))));
        }

        const float stuckEpsilon = 1e-3f;
        float speedCeiling = settings.TargetSpeed + 1e-3f;
        float dt = settings.FixedDeltaTime;
        for (int tick = 0; tick < 400; tick++)
        {
            world.Step(dt);
            for (int i = 0; i < world.Count; i++)
            {
                Boid boid = world.Boids[i];
                Assert.True(float.IsFinite(boid.Position.X) && float.IsFinite(boid.Position.Y), $"Non-finite position at tick {tick}, boid {i}.");
                Assert.True(float.IsFinite(boid.Velocity.X) && float.IsFinite(boid.Velocity.Y), $"Non-finite velocity at tick {tick}, boid {i}.");
                float speed = boid.Velocity.Length;
                Assert.True(speed >= stuckEpsilon, $"Boid {i} speed {speed} is stuck at tick {tick}.");
                Assert.True(speed <= speedCeiling, $"Boid {i} speed {speed} exceeds {speedCeiling} at tick {tick}.");
            }
        }
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
