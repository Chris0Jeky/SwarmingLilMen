using SwarmSim.Core.Canonical;

namespace SwarmSim.Tests;

public sealed class CanonicalSpatialPrecisionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroRadius_OnlyReturnsOtherCoincidentBoids(bool useGrid)
    {
        var boids = new[]
        {
            At(0f),
            At(1e-30f), // Distinct, even though its float squared distance is zero.
            At(0f)
        };
        var index = CreateIndex(useGrid, boids);
        var results = new int[4];

        var query = index.QueryNeighbors(boids, 0, 0f, results);

        Assert.Equal(1, query.Count);
        Assert.Equal(2, results[0]);
        Assert.False(query.IsTruncated);
    }

    [Theory]
    [InlineData(false, 1e-30f)]
    [InlineData(true, 1e-30f)]
    [InlineData(false, 1e-22f)]
    [InlineData(true, 1e-22f)]
    public void TinyRadius_KeepsTheInclusiveBoundaryAndExcludesTheNextFloat(bool useGrid, float radius)
    {
        var boids = new[]
        {
            At(0f),
            At(radius),
            At(float.BitIncrement(radius)),
            At(0f)
        };
        var index = CreateIndex(useGrid, boids);
        var results = new int[4];

        var query = index.QueryNeighbors(boids, 0, radius, results);

        Assert.Equal(2, query.Count);
        Assert.Equal(new[] { 1, 3 }, results.AsSpan(0, query.Count).ToArray());
        Assert.False(query.IsTruncated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroRadius_EmptyBufferIsNotTruncatedByDistinctTinyOffsets(bool useGrid)
    {
        var boids = new[] { At(0f), At(1e-30f) };
        var index = CreateIndex(useGrid, boids);

        var query = index.QueryNeighbors(boids, 0, 0f, Span<int>.Empty);

        Assert.Equal(0, query.Count);
        Assert.False(query.IsTruncated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LargeFiniteRadius_ExcludesFartherBoidsWhenSquaresOverflow(bool useGrid)
    {
        var boids = new[] { At(0f), At(5e19f), At(1e20f), At(2e20f) };
        var index = CreateIndex(useGrid, boids, worldExtent: 1e30f, cellSize: 1e29f);
        var results = new int[4];

        var query = index.QueryNeighbors(boids, 0, 1e20f, results);

        Assert.Equal(2, query.Count);
        Assert.Equal(new[] { 1, 2 }, results.AsSpan(0, query.Count).ToArray());
        Assert.False(query.IsTruncated);
    }

    private static Boid At(float x) => new(new Vec2(x, 0f), new Vec2(1f, 0f));

    private static ISpatialIndex CreateIndex(bool useGrid, Boid[] boids, float worldExtent = 100f, float cellSize = 10f)
    {
        ISpatialIndex index = useGrid
            ? new GridSpatialIndex(cellSize, worldExtent, worldExtent)
            : new NaiveSpatialIndex(worldExtent, worldExtent);
        index.Initialize(boids.Length);
        index.Rebuild(boids);
        return index;
    }
}
