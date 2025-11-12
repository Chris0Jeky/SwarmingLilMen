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

        var world = new CanonicalWorld(settings, new NaiveSpatialIndex());
        world.TryAddBoid(Vec2.Zero, new Vec2(3f, 0f));

        world.Step(0.5f);

        var boid = world.Boids[0];
        Assert.Equal(1f, boid.Position.X, 3);
        Assert.InRange(boid.Velocity.Length, 1.999f, 2.001f);
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

        var world = new CanonicalWorld(settings, new NaiveSpatialIndex());
        var spy = new NeighborSpyRule();
        world.AddRule(spy);

        world.TryAddBoid(Vec2.Zero, new Vec2(1f, 0f));
        world.TryAddBoid(new Vec2(-5f, 0f), new Vec2(0f, 1f));
        world.Step(0.1f);

        Assert.True(spy.ObservedNeighborCounts.TryGetValue(0, out int behindCount));
        Assert.Equal(0, behindCount);

        var worldAhead = new CanonicalWorld(settings, new NaiveSpatialIndex());
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
            var world = new CanonicalWorld(settings, new NaiveSpatialIndex());
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
        fieldOfViewDegrees: 360f,
        deltaTime: 0.016f);
}
