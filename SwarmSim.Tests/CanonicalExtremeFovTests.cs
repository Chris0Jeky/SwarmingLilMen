using SwarmSim.Core.Canonical;

namespace SwarmSim.Tests;

public sealed class CanonicalExtremeFovTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (int exponent in new[] { 0, 80 })
            foreach (bool grid in new[] { false, true })
                foreach (bool seam in new[] { false, true })
                    foreach (float fov in new[] { 100f, 120f, 270f })
                        yield return [exponent, grid, seam, fov];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void PartialFovUsesDirectionWhenTheSquaredDistanceOverflows(int exponent, bool grid, bool seam, float fov)
    {
        float scale = MathF.ScaleB(1f, exponent);
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 2, WorldWidth = 16f * scale, WorldHeight = 16f * scale,
            SenseRadius = 8f * scale, FieldOfView = fov, TargetSpeed = 1f, WanderStrength = 0f
        };
        ISpatialIndex index = grid
            ? new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight)
            : new NaiveSpatialIndex(settings.WorldWidth, settings.WorldHeight);
        var observer = new ObservationCounts();
        var world = new CanonicalWorld(settings, index,
            new CanonicalPipeline([observer], new PriorityForceBudgetResolver()));
        Vec2 origin = seam ? new Vec2(15f * scale, 15f * scale) : Vec2.Zero;
        Vec2 neighbor = seam ? new Vec2(2f * scale, 3f * scale) : new Vec2(3f * scale, 4f * scale);
        Assert.True(world.TryAddBoid(origin, new Vec2(1f, 0f)));
        Assert.True(world.TryAddBoid(neighbor, new Vec2(1f, 0f)));
        var neighbors = new int[2];
        var weights = new float[2];

        // A 3-4-5 displacement has bearing 53.1301 degrees. Its reverse has bearing 126.8699.
        int expectedFront = fov >= 120f ? 1 : 0;
        int expectedRear = fov == 270f ? 1 : 0;
        var front = world.QueryVisibleNeighbors(0, neighbors, weights);
        Assert.Equal(expectedFront, front.Count);
        Assert.False(front.IsTruncated);
        if (expectedFront == 1)
        {
            Assert.Equal(1, neighbors[0]);
            // Fixed analytic direction cosine, independent of the normalization implementation.
            float halfCos = MathF.Cos(fov * MathF.PI / 180f * 0.5f);
            Assert.Equal((0.6f - halfCos) / (1f - halfCos), weights[0]);
        }
        Assert.Equal(expectedRear, world.QueryVisibleNeighbors(1, neighbors, weights).Count);

        world.Step(settings.FixedDeltaTime);

        Assert.Equal(new[] { expectedFront, expectedRear }, observer.Counts);
        Assert.Equal(expectedFront, world.Instrumentation.NeighborCounts[0]);
        Assert.Equal(expectedFront, world.QueryLastTickVisibleNeighbors(0, neighbors, weights).Count);
        Assert.Equal(expectedRear, world.QueryLastTickVisibleNeighbors(1, neighbors, weights).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NearlyZeroDirectionStillUsesTheEstablishedOmnidirectionalCutoff(bool grid)
    {
        float scale = MathF.ScaleB(1f, -90);
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 2, WorldWidth = 16f * scale, WorldHeight = 16f * scale,
            SenseRadius = 8f * scale, FieldOfView = 1f
        };
        ISpatialIndex index = grid
            ? new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight)
            : new NaiveSpatialIndex(settings.WorldWidth, settings.WorldHeight);
        var world = new CanonicalWorld(settings, index, CanonicalPipeline.Empty());
        Assert.True(world.TryAddBoid(new Vec2(4f * scale, 4f * scale), new Vec2(1f, 0f)));
        Assert.True(world.TryAddBoid(new Vec2(1f * scale, 0f), new Vec2(1f, 0f)));
        var neighbors = new int[2];
        var weights = new float[2];

        Assert.Equal(1, world.QueryVisibleNeighbors(0, neighbors, weights).Count);
        Assert.Equal(1f, weights[0]);
    }

    private sealed class ObservationCounts : ILocalSteeringPolicy
    {
        public PolicyMetadata Metadata { get; } = new("extreme-fov-observer");
        public int MaxIntents => 0;
        public List<int> Counts { get; } = [];
        public void Emit(in LocalObservation observation, in LocalPolicyContext context, ref IntentWriter intents)
            => Counts.Add(observation.Neighbors.Length);
    }
}
