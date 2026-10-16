using SwarmSim.Core.Canonical;
using Xunit;

namespace SwarmSim.Tests;

public class CanonicalBoidsTests
{
    [Fact]
    public void Vec2_NormalizeClampDotBehaveAsExpected()
    {
        var vector = new Vec2(3f, 4f);
        Assert.InRange(vector.Length, 4.9999f, 5.0001f);

        var normalized = vector.Normalized;
        Assert.InRange(normalized.Length, 0.9999f, 1.0001f);
        Assert.InRange(normalized.X, 0.599f, 0.601f);
        Assert.InRange(normalized.Y, 0.799f, 0.801f);

        var clamped = new Vec2(10f, 0f).ClampMagnitude(5f);
        Assert.InRange(clamped.Length, 4.9999f, 5.0001f);

        float dot = Vec2.Dot(vector, new Vec2(0f, 1f));
        Assert.Equal(4f, dot);
    }

    [Fact]
    public void CanonicalWorld_SingleBoidMaintainsTargetSpeed()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            TargetSpeed = 2f,
            MaxForce = 1f,
            FieldOfView = 360f,
            SenseRadius = 5f
        };

        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        world.TryAddBoid(Vec2.Zero, new Vec2(3f, 0f));

        world.Step(0.5f);

        var boid = world.Boids[0];
        var expectedSpeed = 2f;
        Assert.InRange(boid.Position.X, 0.99f, 1.01f);
        Assert.InRange(boid.Velocity.Length, expectedSpeed - 0.01f, expectedSpeed + 0.01f);
    }

    [Fact]
    public void CanonicalWorld_FieldOfViewFiltersBehindNeighbors()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            TargetSpeed = 1f,
            MaxForce = 1f,
            FieldOfView = 90f,
            SenseRadius = 5f
        };

        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        var spy = new NeighborSpyRule();
        world.AddRule(spy);

        world.TryAddBoid(Vec2.Zero, new Vec2(1f, 0f));
        world.TryAddBoid(new Vec2(-5f, 0f), new Vec2(0f, 1f));
        world.Step(0.1f);

        Assert.True(spy.ObservedNeighborCounts.TryGetValue(0, out int behindCount));
        Assert.Equal(0, behindCount);

        var worldAhead = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        var spyAhead = new NeighborSpyRule();
        worldAhead.AddRule(spyAhead);

        worldAhead.TryAddBoid(Vec2.Zero, new Vec2(1f, 0f));
        worldAhead.TryAddBoid(new Vec2(5f, 0f), new Vec2(0f, 1f));
        worldAhead.Step(0.1f);

        Assert.True(spyAhead.ObservedNeighborCounts.TryGetValue(0, out int frontCount));
        Assert.Equal(1, frontCount);
    }

    [Fact]
    public void CanonicalWorld_StepsAreDeterministic()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 8,
            TargetSpeed = 2f,
            MaxForce = 0.5f,
            FieldOfView = 360f,
            SenseRadius = 10f
        };

        CanonicalWorld CreateWorld()
        {
            var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
            for (int i = 0; i < 3; i++)
            {
                var position = new Vec2(i * 2f, i * 1.5f);
                var velocity = new Vec2(1f, 0.5f);
                world.TryAddBoid(position, velocity);
            }

            return world;
        }

        var worldA = CreateWorld();
        var worldB = CreateWorld();

        worldA.Step(0.25f);
        worldB.Step(0.25f);

        for (int i = 0; i < 3; i++)
        {
            var boidA = worldA.Boids[i];
            var boidB = worldB.Boids[i];

            Assert.Equal(boidA.Position.X, boidB.Position.X, 4);
            Assert.Equal(boidA.Position.Y, boidB.Position.Y, 4);
            Assert.Equal(boidA.Velocity.X, boidB.Velocity.X, 4);
            Assert.Equal(boidA.Velocity.Y, boidB.Velocity.Y, 4);
        }
    }

    [Fact]
    public void CanonicalWorld_SeparationPriorityEngagesWhenOvercrowded()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            TargetSpeed = 1f,
            MaxForce = 1f,
            FieldOfView = 360f,
            SenseRadius = 6f,
            SeparationRadius = 3f,
            SeparationPriorityRadiusFactor = 0.33f
        };

        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        world.TryAddBoid(Vec2.Zero, new Vec2(1f, 0f));
        world.TryAddBoid(new Vec2(1.5f, 0f), new Vec2(-1f, 0f));

        world.Step(0.1f);
        var snapshot = world.CapturePerceptionSnapshot();

        Assert.True(snapshot.SeparationPriorityTriggered, "Separation priority should engage when neighbors are closer than 1/3 of the sense radius");
    }

    [Fact]
    public void CanonicalWorld_PriorityDroop_ReducesSpeed()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            TargetSpeed = 2f,
            MaxForce = 1f,
            FieldOfView = 360f,
            SenseRadius = 6f,
            SeparationRadius = 3f,
            SeparationPriorityRadiusFactor = 0.33f,
            SeparationSpeedDroop = 0.5f,
            SeparationPriorityRampInTime = 0.08f,
            WanderStrength = 0f
        };

        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        world.TryAddBoid(Vec2.Zero, new Vec2(1f, 0f));
        world.TryAddBoid(new Vec2(1.5f, 0f), new Vec2(-1f, 0f));

        float deltaTime = 0.1f;
        world.Step(deltaTime);

        float blend = MathF.Min(1f, deltaTime / settings.SeparationPriorityRampInTime);
        float expectedSpeed = settings.TargetSpeed * (1f - settings.SeparationSpeedDroop * blend);

        var boid = world.Boids[0];
        Assert.InRange(boid.Velocity.Length, expectedSpeed - 0.05f, expectedSpeed + 0.05f);
    }

    [Fact]
    public void SeparationRule_RepelsCloseNeighbor()
    {
        var rule = new SeparationRule(weight: 1f, radius: 5f);
        var boids = new[]
        {
            new Boid(Vec2.Zero, new Vec2(1f, 0f)),
            new Boid(new Vec2(2f, 0f), new Vec2(-1f, 0f))
        };
        var context = CreateTestContext();
        var neighborIndices = new[] { 1 };
        var neighborWeights = new[] { 1f };

        Vec2 steer = rule.Compute(0, boids[0], boids, neighborIndices, neighborWeights, context);
        Assert.True(steer.X < 0f, "Steering should push away from the neighbor");
    }

    [Fact]
    public void AlignmentRule_MatchesNeighborHeading()
    {
        var rule = new AlignmentRule(weight: 1f);
        var boids = new[]
        {
            new Boid(Vec2.Zero, new Vec2(0f, 0f)),
            new Boid(new Vec2(0f, 0f), new Vec2(0f, 1f))
        };
        var context = CreateTestContext();
        var neighborIndices = new[] { 1 };
        var neighborWeights = new[] { 1f };

        Vec2 steer = rule.Compute(0, boids[0], boids, neighborIndices, neighborWeights, context);
        Assert.True(steer.Y > 0f, "Steering should encourage upward heading");
    }

    [Fact]
    public void CohesionRule_PullsTowardGroupCenter()
    {
        var rule = new CohesionRule(weight: 1f);
        var boids = new[]
        {
            new Boid(new Vec2(-2f, 0f), new Vec2(1f, 0f)),
            new Boid(new Vec2(2f, 0f), new Vec2(1f, 0f)),
            new Boid(new Vec2(2f, 2f), new Vec2(1f, 0f))
        };
        var context = CreateTestContext();
        var neighborIndices = new[] { 1, 2 };
        var neighborWeights = new[] { 1f, 1f };

        Vec2 steer = rule.Compute(0, boids[0], boids, neighborIndices, neighborWeights, context);
        Vec2 centroid = (boids[1].Position + boids[2].Position) / 2f;
        Vec2 toCentroid = centroid - boids[0].Position;
        Assert.True(Vec2.Dot(steer, toCentroid) > 0f, "Steering should move toward the centroid");
    }

    private sealed class NeighborSpyRule : IRule
    {
        private readonly Dictionary<int, int> _observed = new();

        public IReadOnlyDictionary<int, int> ObservedNeighborCounts => _observed;

        public Vec2 Compute(int selfIndex, Boid self, ReadOnlySpan<Boid> boids, ReadOnlySpan<int> neighborIndices, ReadOnlySpan<float> neighborWeights, RuleContext context)
        {
            _observed[selfIndex] = neighborIndices.Length;
            return Vec2.Zero;
        }
    }

        private static RuleContext CreateTestContext() => new(
            targetSpeed: 1f,
            maxForce: 0.5f,
            senseRadius: 10f,
            fieldOfViewCos: -1f,
            deltaTime: 0.016f,
            separationPriorityBoost: 1f);

    [Fact]
    public void CanonicalWorld_AngularRateLimiter_ClampsTurningSpeed()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            TargetSpeed = 10f,
            MaxForce = 100f,
            MaxTurnRateDegPerSecond = 180f,
            FieldOfView = 360f,
            SenseRadius = 50f,
            SeparationRadius = 20f,
            FixedDeltaTime = 1f / 60f
        };

        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        world.TryAddBoid(new Vec2(0f, 0f), new Vec2(1f, 0f));

        float initialAngle = MathF.Atan2(0f, 1f);
        world.Step(settings.FixedDeltaTime);

        var boid = world.Boids[0];
        float finalAngle = MathF.Atan2(boid.Velocity.Y, boid.Velocity.X);
        float angleDelta = MathF.Abs(finalAngle - initialAngle) * 180f / MathF.PI;

        float maxAllowedTurn = settings.MaxTurnRateDegPerSecond * settings.FixedDeltaTime;
        Assert.True(angleDelta <= maxAllowedTurn + 0.1f,
            $"Angular rate limiter failed: turned {angleDelta}° in one step, max allowed was {maxAllowedTurn}°");
    }

    [Fact]
    public void CanonicalWorld_PriorityHysteresis_PreventsPingPong()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            TargetSpeed = 1f,
            MaxForce = 1f,
            FieldOfView = 360f,
            SenseRadius = 10f,
            SeparationRadius = 5f,
            SeparationPriorityRadiusFactor = 0.5f,
            SeparationPriorityExitFactor = 0.6f,
            SeparationPriorityHoldTime = 0.1f,
            AlignmentWeight = 0f,
            CohesionWeight = 0f,
            WanderStrength = 0f,
            FixedDeltaTime = 1f / 60f
        };

        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        world.TryAddBoid(new Vec2(100f, 100f), new Vec2(1f, 0f));
        world.TryAddBoid(new Vec2(104.5f, 100f), new Vec2(-1f, 0f));

        world.Step(settings.FixedDeltaTime);
        var snapshot1 = world.CapturePerceptionSnapshot();
        Assert.True(snapshot1.SeparationPriorityTriggered, "Priority should engage at close distance");

        world.SetVelocity(0, new Vec2(1f, 0f));
        world.SetVelocity(1, new Vec2(-1f, 0f));

        // Allow the agents to cross the exit threshold, then complete the hold and ramp-out.
        for (int i = 0; i < 90; i++)
        {
            world.Step(settings.FixedDeltaTime);
        }

        var snapshot2 = world.CapturePerceptionSnapshot();
        float distance = (world.Boids[1].Position - world.Boids[0].Position).Length;
        float exitThreshold = settings.SeparationPriorityExitFactor * settings.SenseRadius;

        Assert.True(distance >= exitThreshold, "Agents should separate beyond the priority exit threshold");
        Assert.False(snapshot2.SeparationPriorityTriggered,
            "Priority should exit after agents separate beyond exit threshold and hold/ramp-out times expire");
    }

    [Fact]
    public void CanonicalWorld_WhiskerCounts_MatchNeighborsInCapsule()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 8,
            TargetSpeed = 5f,
            MaxForce = 1f,
            FieldOfView = 360f,
            SenseRadius = 20f,
            SeparationRadius = 5f,
            WhiskerTimeHorizon = 0.5f,
            FixedDeltaTime = 1f / 60f
        };

        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        world.TryAddBoid(new Vec2(100f, 100f), new Vec2(1f, 0f));
        world.TryAddBoid(new Vec2(110f, 100f), new Vec2(1f, 0f));
        world.TryAddBoid(new Vec2(115f, 101f), new Vec2(1f, 0f));

        world.Step(settings.FixedDeltaTime);
        var snapshot = world.CapturePerceptionSnapshot();

        Assert.True(snapshot.WhiskerCounts.Length == 3, "Whisker counts should be captured for all agents");
        // lookAhead = TargetSpeed (5) * WhiskerTimeHorizon (0.5) = 2.5, so neighbors at
        // along ~10/15 lie outside the whisker corridor and the correct count is 0.
        Assert.Equal(0, snapshot.WhiskerCounts[0]);
    }

    [Fact]
    public void CanonicalWorld_WhiskerCounts_CountsHeadOnNeighbor()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 8,
            TargetSpeed = 5f,
            MaxForce = 1f,
            FieldOfView = 360f,
            SenseRadius = 20f,
            SeparationRadius = 5f,
            WhiskerTimeHorizon = 0.5f,
            FixedDeltaTime = 1f / 60f
        };

        // Head-on neighbor inside the corridor: along = 2 <= lookAhead (2.5), lateral = 0.
        var headOn = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        headOn.TryAddBoid(new Vec2(100f, 100f), new Vec2(1f, 0f));
        headOn.TryAddBoid(new Vec2(102f, 100f), new Vec2(1f, 0f));

        headOn.Step(settings.FixedDeltaTime);
        var headOnSnapshot = headOn.CapturePerceptionSnapshot();

        Assert.Equal(2, headOnSnapshot.WhiskerCounts.Length);
        Assert.Equal(1, headOnSnapshot.WhiskerCounts[0]);

        // Lateral-outside neighbor: (100,108) is 8 off to the side (whiskerRadius 5)
        // and not ahead, so the corridor check excludes it.
        var lateral = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        lateral.TryAddBoid(new Vec2(100f, 100f), new Vec2(1f, 0f));
        lateral.TryAddBoid(new Vec2(100f, 108f), new Vec2(1f, 0f));

        lateral.Step(settings.FixedDeltaTime);
        var lateralSnapshot = lateral.CapturePerceptionSnapshot();

        Assert.Equal(2, lateralSnapshot.WhiskerCounts.Length);
        Assert.Equal(0, lateralSnapshot.WhiskerCounts[0]);
    }

    [Fact]
    public void CanonicalWorld_PerceptionSnapshot_ContainsPerAgentData()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 8,
            TargetSpeed = 2f,
            MaxForce = 0.5f,
            FieldOfView = 360f,
            SenseRadius = 10f
        };

        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        world.TryAddBoid(new Vec2(0f, 0f), new Vec2(1f, 0f));
        world.TryAddBoid(new Vec2(5f, 0f), new Vec2(1f, 0f));

        world.Step(0.1f);
        var snapshot = world.CapturePerceptionSnapshot();

        Assert.NotNull(snapshot.NearestDistances);
        Assert.NotNull(snapshot.NearestAngles);
        Assert.NotNull(snapshot.WhiskerCounts);
        Assert.Equal(2, snapshot.NearestDistances.Length);
        Assert.Equal(2, snapshot.NearestAngles.Length);
        Assert.Equal(2, snapshot.WhiskerCounts.Length);
        Assert.True(snapshot.NearestDistances[0] > 0f, "Nearest distance should be populated");
    }

    [Fact]
    public void CanonicalWorld_TryAddBoid_ReturnsFalseAtCapacity()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 1
        };

        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));

        Assert.True(world.TryAddBoid(Vec2.Zero, new Vec2(1f, 0f)));
        Assert.False(world.TryAddBoid(new Vec2(5f, 0f), new Vec2(1f, 0f)));
        Assert.Equal(1, world.Count);
    }

    [Fact]
    public void CanonicalWorld_Step_RejectsNonPositiveDeltaTime()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            TargetSpeed = 1f,
            MaxForce = 1f,
            FieldOfView = 360f,
            SenseRadius = 5f
        };

        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        world.TryAddBoid(Vec2.Zero, new Vec2(1f, 0f));

        Assert.Throws<ArgumentOutOfRangeException>(() => world.Step(0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Step(-1f));
    }
}
