using SwarmSim.Core.Canonical;
using Xunit;
using RenderProgram = SwarmSim.Render.Program;

namespace SwarmSim.Tests;

public sealed class SpatialIndexEquivalenceTests
{
    [Fact]
    public void SpatialIndexEquivalence_GridFindsSeamNeighborWithinRadius()
    {
        var boids = CreateBoids((1f, 50f), (99f, 50f));

        AssertEquivalent(boids, 100f, 100f, 10f, selfIndex: 0, radius: 3f, bufferLength: 4, new[] { 1 });
    }

    [Fact]
    public void SpatialIndexEquivalence_HonorsInclusiveCircleSelfExclusionAndCoincidentPositions()
    {
        var boids = CreateBoids((10f, 10f), (10f, 10f), (13f, 14f), (13.01f, 14f));

        AssertEquivalent(boids, 100f, 100f, 10f, selfIndex: 0, radius: 5f, bufferLength: 8, new[] { 1, 2 });
    }

    [Fact]
    public void SpatialIndexEquivalence_FindsCornerNeighborAcrossBothToroidalSeams()
    {
        var boids = CreateBoids((1f, 1f), (99f, 99f), (50f, 50f));

        AssertEquivalent(boids, 100f, 100f, 10f, selfIndex: 0, radius: 3f, bufferLength: 8, new[] { 1 });
    }

    [Fact]
    public void SpatialIndexEquivalence_NormalizesUnwrappedPositionsBeforeGridPlacement()
    {
        var boids = CreateBoids((50f, 50f), (150f, 50f), (-50f, 50f));

        AssertEquivalent(boids, 100f, 100f, 10f, selfIndex: 0, radius: 0f, bufferLength: 4, new[] { 1, 2 });
    }

    [Fact]
    public void SpatialIndexEquivalence_ScansPartialTerminalCellAcrossSeam()
    {
        var boids = CreateBoids((50f, 1f), (50f, 99f));

        AssertEquivalent(boids, 100f, 108f, 10f, selfIndex: 0, radius: 10f, bufferLength: 4, new[] { 1 });
    }

    [Theory]
    [InlineData(8f, 8f)]
    [InlineData(20f, 20f)]
    public void SpatialIndexEquivalence_OneAndTwoCellWorldsDoNotDuplicateCandidates(float worldWidth, float worldHeight)
    {
        var boids = CreateBoids((1f, 1f), (worldWidth - 1f, 1f), (1f, worldHeight - 1f), (worldWidth - 1f, worldHeight - 1f));

        AssertEquivalent(boids, worldWidth, worldHeight, 10f, selfIndex: 0, radius: 3f, bufferLength: 8, new[] { 1, 2, 3 });
    }

    [Fact]
    public void SpatialIndexEquivalence_DeterministicRandomizedScenariosMatchForTwoHundredSeeds()
    {
        for (int scenario = 0; scenario < 200; scenario++)
        {
            var random = new Random(7000 + scenario);
            float width = (scenario % 5) switch
            {
                0 => 8f,
                1 => 20f,
                _ => 31f + (scenario % 7) * 11f
            };
            float height = scenario % 4 == 0 ? 9f : 27f + (scenario % 9) * 7f;
            float cellSize = scenario % 3 == 0 ? 10f : 7f;
            int count = 2 + random.Next(1, 40);
            var boids = new Boid[count];
            for (int i = 0; i < boids.Length; i++)
            {
                float x = i == 0 ? 0.5f : (float)(random.NextDouble() * width);
                float y = i == 0 ? 0.5f : (float)(random.NextDouble() * height);
                boids[i] = new Boid(new Vec2(x, y), new Vec2(1f, 0f));
            }

            int selfIndex = random.Next(boids.Length);
            float radius = (float)(random.NextDouble() * Math.Max(width, height) * 1.25);
            AssertEquivalent(boids, width, height, cellSize, selfIndex, radius, bufferLength: boids.Length);
        }
    }

    [Fact]
    public void SpatialIndexEquivalence_DenseQueriesExposeDeterministicTruncation()
    {
        var boids = CreateBoids((10f, 10f), (10f, 10f), (10f, 10f), (10f, 10f), (10f, 10f), (10f, 10f));

        AssertEquivalent(boids, 100f, 100f, 10f, selfIndex: 0, radius: 0f, bufferLength: 2, new[] { 1, 2 }, expectedTruncation: true);
    }

    [Fact]
    public void SpatialIndexEquivalence_RequiresRebuildAndRejectsInvalidRadius()
    {
        var boids = CreateBoids((10f, 10f), (12f, 10f));
        var grid = new GridSpatialIndex(10f, 100f, 100f);
        var naive = new NaiveSpatialIndex(100f, 100f);
        var results = new int[2];
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Initialize(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => naive.Initialize(0));
        grid.Initialize(boids.Length);
        naive.Initialize(boids.Length);

        Assert.Throws<InvalidOperationException>(() => grid.QueryNeighbors(boids, 0, 5f, results));
        Assert.Throws<InvalidOperationException>(() => naive.QueryNeighbors(boids, 0, 5f, results));

        grid.Rebuild(boids);
        naive.Rebuild(boids);
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.QueryNeighbors(boids, 0, float.NaN, results));
        Assert.Throws<ArgumentOutOfRangeException>(() => naive.QueryNeighbors(boids, 0, -1f, results));
    }

    [Fact]
    public void SpatialIndexEquivalence_QueryHotPathDoesNotAllocateAfterWarmup()
    {
        var random = new Random(8181);
        var boids = new Boid[128];
        for (int i = 0; i < boids.Length; i++)
        {
            boids[i] = new Boid(
                new Vec2((float)random.NextDouble() * 200f, (float)random.NextDouble() * 150f),
                new Vec2(1f, 0f));
        }

        var grid = new GridSpatialIndex(12f, 200f, 150f);
        var naive = new NaiveSpatialIndex(200f, 150f);
        grid.Initialize(boids.Length);
        naive.Initialize(boids.Length);
        grid.Rebuild(boids);
        naive.Rebuild(boids);
        var gridResults = new int[boids.Length];
        var naiveResults = new int[boids.Length];

        // Cross the tiered-compilation threshold before measuring so one-time runtime work is not
        // mistaken for a steady-state query allocation.
        for (int i = 0; i < 4_096; i++)
        {
            int selfIndex = i % boids.Length;
            _ = grid.QueryNeighbors(boids, selfIndex, 25f, gridResults);
            _ = naive.QueryNeighbors(boids, selfIndex, 25f, naiveResults);
        }

        int observedNeighbors = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1_000; i++)
        {
            int selfIndex = i % boids.Length;
            observedNeighbors += grid.QueryNeighbors(boids, selfIndex, 25f, gridResults).Count;
            observedNeighbors += naive.QueryNeighbors(boids, selfIndex, 25f, naiveResults).Count;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(observedNeighbors > 0);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void SpatialIndexEquivalence_BuiltInRulesUseMinimumImageDeltaAcrossSeam()
    {
        var separationBoids = new[]
        {
            new Boid(new Vec2(1f, 50f), new Vec2(-1f, 0f)),
            new Boid(new Vec2(99f, 50f), new Vec2(-1f, 0f))
        };
        var context = CreateContext(100f, 100f);
        var neighbors = new[] { 1 };
        var weights = new[] { 1f };

        Vec2 separation = new SeparationRule(weight: 1f, radius: 5f)
            .Compute(0, separationBoids[0], separationBoids, neighbors, weights, context);
        Assert.True(separation.X > 0f, "Separation should move right, away from the minimum-image neighbor on the left.");

        var cohesionBoids = new[]
        {
            new Boid(new Vec2(99f, 50f), new Vec2(-1f, 0f)),
            new Boid(new Vec2(1f, 50f), new Vec2(1f, 0f))
        };
        Vec2 cohesion = new CohesionRule(weight: 1f)
            .Compute(0, cohesionBoids[0], cohesionBoids, neighbors, weights, context);
        Assert.True(cohesion.X > 0f, "Cohesion should move right across the seam toward its minimum-image neighbor.");
    }

    [Fact]
    public void SpatialIndexEquivalence_WorldFieldOfViewTreatsSeamNeighborAsAhead()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 4,
            WorldWidth = 100f,
            WorldHeight = 100f,
            SenseRadius = 5f,
            FieldOfView = 90f,
            TargetSpeed = 1f
        };
        var world = new CanonicalWorld(settings, new GridSpatialIndex(10f, settings.WorldWidth, settings.WorldHeight));
        world.TryAddBoid(new Vec2(1f, 50f), new Vec2(-1f, 0f));
        world.TryAddBoid(new Vec2(99f, 50f), new Vec2(-1f, 0f));
        Span<int> visible = stackalloc int[4];
        Span<float> weights = stackalloc float[4];

        SpatialQueryResult beforeFirstStep = world.QueryVisibleNeighbors(0, visible, weights);
        world.Step(settings.FixedDeltaTime);
        SpatialQueryResult afterStep = world.QueryVisibleNeighbors(0, visible, weights);

        Assert.Equal(1, beforeFirstStep.Count);
        Assert.Equal(1, afterStep.Count);
        Assert.False(beforeFirstStep.IsTruncated);
        Assert.False(afterStep.IsTruncated);
        Assert.Equal(1, visible[0]);
    }

    [Fact]
    public void SpatialIndexEquivalence_GridAndNaiveWorldsMatchForTwoHundredTicks()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 16,
            WorldWidth = 100f,
            WorldHeight = 80f,
            SenseRadius = 17f,
            SeparationRadius = 7f,
            FieldOfView = 360f,
            MaxNeighbors = 32,
            WanderStrength = 0f,
            TargetSpeed = 3f,
            MaxForce = 1f,
            Seed = 1234u
        };
        var gridWorld = new CanonicalWorld(settings, new GridSpatialIndex(10f, settings.WorldWidth, settings.WorldHeight));
        var naiveWorld = new CanonicalWorld(settings, new NaiveSpatialIndex(settings.WorldWidth, settings.WorldHeight));
        var random = new Random(9191);

        for (int i = 0; i < 12; i++)
        {
            var position = new Vec2((float)(random.NextDouble() * settings.WorldWidth), (float)(random.NextDouble() * settings.WorldHeight));
            var velocity = new Vec2((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f);
            Assert.True(gridWorld.TryAddBoid(position, velocity));
            Assert.True(naiveWorld.TryAddBoid(position, velocity));
        }

        for (int tick = 0; tick < 200; tick++)
        {
            gridWorld.Step(settings.FixedDeltaTime);
            naiveWorld.Step(settings.FixedDeltaTime);
        }

        for (int i = 0; i < gridWorld.Count; i++)
        {
            Boid grid = gridWorld.Boids[i];
            Boid naive = naiveWorld.Boids[i];
            Assert.InRange(MathF.Abs(grid.Position.X - naive.Position.X), 0f, 1e-4f);
            Assert.InRange(MathF.Abs(grid.Position.Y - naive.Position.Y), 0f, 1e-4f);
            Assert.InRange(MathF.Abs(grid.Velocity.X - naive.Velocity.X), 0f, 1e-4f);
            Assert.InRange(MathF.Abs(grid.Velocity.Y - naive.Velocity.Y), 0f, 1e-4f);
        }
    }

    [Fact]
    public void SpatialIndexEquivalence_ScansPartialTerminalCellWhenBothWrappedEndpointsLandInTheCentreCell()
    {
        // Width 18 with cell size 10 leaves a short terminal cell [10, 18). A query at x = 0.5
        // with radius 8.6 wraps to endpoints 9.9 and 9.1 - both inside centre cell 0 - while the
        // circular interval still crosses the whole of cell 1. Deriving the scan reach from the
        // endpoints' cell IDs therefore never visits cell 1 and loses the neighbour at x = 11.4,
        // which is only 7.1 units away across the seam.
        var boids = CreateBoids((0.5f, 50f), (11.4f, 50f));

        AssertEquivalent(boids, 18f, 100f, 10f, selfIndex: 0, radius: 8.6f, bufferLength: 4, new[] { 1 });
    }

    [Fact]
    public void SpatialIndexEquivalence_UnwrappedPositionsUseIdenticalArithmeticAtTheInclusiveBoundary()
    {
        // The grid normalises positions into [0, extent) when it rebuilds, then subtracts the
        // normalised values. The naive index must perform the same operations in the same order,
        // or float rounding diverges for far-out-of-range inputs and the two indexes disagree
        // about a neighbour sitting on the inclusive radius boundary.
        var boids = CreateBoids((-428.021515f, 10f), (159.694336f, 10f));

        AssertEquivalent(boids, 100f, 100f, 10f, selfIndex: 0, radius: 12.28416f, bufferLength: 4);
    }

    [Fact]
    public void SpatialIndexEquivalence_OverlayQueryCapacityMatchesTheCapSteeringApplies()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 16,
            WorldWidth = 100f,
            WorldHeight = 100f,
            SenseRadius = 20f,
            FieldOfView = 360f,
            MaxNeighbors = 4,
            TargetSpeed = 1f
        };
        var world = new CanonicalWorld(settings, new NaiveSpatialIndex(settings.WorldWidth, settings.WorldHeight));
        for (int i = 0; i < 10; i++)
            Assert.True(world.TryAddBoid(new Vec2(50f + i * 0.5f, 50f), new Vec2(1f, 0f)));

        Assert.Equal(4, world.EffectiveMaxNeighbors);
        Assert.Equal(4, RenderProgram.ComputeOverlayQueryCapacity(128, world.EffectiveMaxNeighbors));
        Assert.Equal(2, RenderProgram.ComputeOverlayQueryCapacity(2, world.EffectiveMaxNeighbors));

        // A diagnostic sized to its own buffer rather than to the simulation's cap sees more
        // neighbours than steering used, and reports no truncation in exactly the case where the
        // simulation truncated. Sizing to EffectiveMaxNeighbors reproduces what Step observed.
        Span<int> wide = stackalloc int[128];
        Span<float> wideWeights = stackalloc float[128];
        SpatialQueryResult wideQuery = world.QueryVisibleNeighbors(0, wide, wideWeights);

        int capacity = RenderProgram.ComputeOverlayQueryCapacity(128, world.EffectiveMaxNeighbors);
        Span<int> capped = stackalloc int[128];
        Span<float> cappedWeights = stackalloc float[128];
        SpatialQueryResult cappedQuery = world.QueryVisibleNeighbors(
            0, capped[..capacity], cappedWeights[..capacity]);

        Assert.Equal(9, wideQuery.Count);
        Assert.False(wideQuery.IsTruncated);
        Assert.Equal(4, cappedQuery.Count);
        Assert.True(cappedQuery.IsTruncated);
    }

    private static void AssertEquivalent(
        Boid[] boids,
        float worldWidth,
        float worldHeight,
        float cellSize,
        int selfIndex,
        float radius,
        int bufferLength,
        int[]? expected = null,
        bool expectedTruncation = false)
    {
        var grid = new GridSpatialIndex(cellSize, worldWidth, worldHeight);
        var naive = new NaiveSpatialIndex(worldWidth, worldHeight);
        grid.Initialize(boids.Length);
        naive.Initialize(boids.Length);
        grid.Rebuild(boids);
        naive.Rebuild(boids);
        var gridResults = new int[bufferLength];
        var naiveResults = new int[bufferLength];

        SpatialQueryResult gridQuery = grid.QueryNeighbors(boids, selfIndex, radius, gridResults);
        SpatialQueryResult naiveQuery = naive.QueryNeighbors(boids, selfIndex, radius, naiveResults);

        Assert.Equal(naiveQuery.Count, gridQuery.Count);
        Assert.Equal(naiveQuery.IsTruncated, gridQuery.IsTruncated);
        Assert.Equal(naiveResults.AsSpan(0, naiveQuery.Count).ToArray(), gridResults.AsSpan(0, gridQuery.Count).ToArray());
        Assert.Equal(expectedTruncation, gridQuery.IsTruncated);
        if (expected is not null)
            Assert.Equal(expected, gridResults.AsSpan(0, gridQuery.Count).ToArray());
    }

    private static Boid[] CreateBoids(params (float X, float Y)[] positions)
    {
        var boids = new Boid[positions.Length];
        for (int i = 0; i < positions.Length; i++)
            boids[i] = new Boid(new Vec2(positions[i].X, positions[i].Y), new Vec2(1f, 0f));
        return boids;
    }

    private static RuleContext CreateContext(float worldWidth, float worldHeight) => new(
        targetSpeed: 1f,
        maxForce: 1f,
        senseRadius: 5f,
        fieldOfViewCos: -1f,
        deltaTime: 1f / 60f,
        separationPriorityBoost: 1f,
        worldWidth: worldWidth,
        worldHeight: worldHeight);
}
