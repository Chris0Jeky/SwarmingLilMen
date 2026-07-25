namespace SwarmSim.Core.Canonical;

/// <summary>
/// Reports the portion of a spatial query written to its caller-provided buffer.
/// </summary>
public readonly struct SpatialQueryResult
{
    /// <summary>
    /// Initializes a spatial query result.
    /// </summary>
    /// <param name="count">The number of neighbor indices written.</param>
    /// <param name="isTruncated">Whether qualifying neighbors did not fit in the output buffer.</param>
    public SpatialQueryResult(int count, bool isTruncated)
    {
        Count = count;
        IsTruncated = isTruncated;
    }

    /// <summary>
    /// Gets the number of neighbor indices written to the output buffer.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets a value indicating whether qualifying neighbors were omitted because the output buffer was full.
    /// </summary>
    public bool IsTruncated { get; }
}

/// <summary>
/// Provides deterministic toroidal circular-neighborhood queries for canonical boids.
/// </summary>
public interface ISpatialIndex
{
    /// <summary>
    /// Allocates or resets this index for at most <paramref name="capacity"/> boids.
    /// </summary>
    /// <param name="capacity">Maximum active boid count accepted by <see cref="Rebuild"/>.</param>
    void Initialize(int capacity);

    /// <summary>
    /// Rebuilds the index from the current boid positions. A rebuild is required after initialization and before querying.
    /// </summary>
    /// <param name="boids">Current boids, whose length must not exceed the initialized capacity.</param>
    void Rebuild(ReadOnlySpan<Boid> boids);

    /// <summary>
    /// Finds boids in the inclusive circular <paramref name="radius"/> around <paramref name="selfIndex"/>.
    /// Distances use the world's toroidal minimum-image delta and the boid at <paramref name="selfIndex"/> is excluded.
    /// Results are the lowest qualifying indices in ascending order; if the buffer is too small,
    /// <see cref="SpatialQueryResult.IsTruncated"/> is set rather than silently hiding the omission.
    /// </summary>
    /// <param name="boids">The same current boid span supplied to <see cref="Rebuild"/>.</param>
    /// <param name="selfIndex">Index of the querying boid.</param>
    /// <param name="radius">Finite, non-negative query radius.</param>
    /// <param name="results">Caller-owned output buffer.</param>
    /// <returns>The written count and truncation status.</returns>
    SpatialQueryResult QueryNeighbors(ReadOnlySpan<Boid> boids, int selfIndex, float radius, Span<int> results);
}
