using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Canonical;

/// <summary>
/// Direct-scan toroidal implementation of <see cref="ISpatialIndex"/> used for equivalence checks.
/// </summary>
public sealed class NaiveSpatialIndex : ISpatialIndex
{
    private int _capacity;
    private readonly float _worldWidth;
    private readonly float _worldHeight;
    private bool _isRebuilt;

    /// <summary>
    /// Creates an index for a toroidal world.
    /// </summary>
    /// <param name="worldWidth">Positive world width.</param>
    /// <param name="worldHeight">Positive world height.</param>
    public NaiveSpatialIndex(float worldWidth, float worldHeight)
    {
        if (!float.IsFinite(worldWidth) || worldWidth <= 0f ||
            !float.IsFinite(worldHeight) || worldHeight <= 0f)
            throw new ArgumentException("World dimensions must be finite and positive.");

        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
    }

    /// <inheritdoc />
    public void Initialize(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

        _capacity = capacity;
        _isRebuilt = false;
    }

    /// <inheritdoc />
    public void Rebuild(ReadOnlySpan<Boid> boids)
    {
        if (_capacity == 0)
            throw new InvalidOperationException("NaiveSpatialIndex has not been initialized.");
        if (boids.Length > _capacity)
            throw new ArgumentException("Boid count exceeds initialized capacity.", nameof(boids));

        _isRebuilt = true;
    }

    /// <inheritdoc />
    public SpatialQueryResult QueryNeighbors(ReadOnlySpan<Boid> boids, int selfIndex, float radius, Span<int> results)
    {
        if (!_isRebuilt)
            throw new InvalidOperationException("NaiveSpatialIndex must be rebuilt before querying.");
        if (!float.IsFinite(radius) || radius < 0f)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be finite and non-negative.");
        if (boids.Length > _capacity)
            throw new ArgumentException("Boid count exceeds initialized capacity.", nameof(boids));
        if (selfIndex < 0 || selfIndex >= boids.Length)
            return new SpatialQueryResult(0, false);

        float radiusSq = radius * radius;
        int count = 0;
        bool isTruncated = false;

        // Normalise into [0, extent) and only then subtract, matching GridSpatialIndex exactly.
        // The grid stores wrapped positions at rebuild time, so subtracting raw coordinates here
        // and wrapping afterwards would round differently in float32 and the two indexes would
        // disagree about neighbours sitting on the inclusive radius boundary.
        float selfX = MathUtils.Wrap(boids[selfIndex].Position.X, _worldWidth);
        float selfY = MathUtils.Wrap(boids[selfIndex].Position.Y, _worldHeight);

        for (int i = 0; i < boids.Length; i++)
        {
            if (i == selfIndex)
                continue;

            float dx = MathUtils.MinimumImageDelta(
                MathUtils.Wrap(boids[i].Position.X, _worldWidth) - selfX, _worldWidth);
            float dy = MathUtils.MinimumImageDelta(
                MathUtils.Wrap(boids[i].Position.Y, _worldHeight) - selfY, _worldHeight);
            if (dx * dx + dy * dy <= radiusSq)
            {
                if (count < results.Length)
                {
                    results[count++] = i;
                }
                else
                {
                    isTruncated = true;
                }
            }
        }

        return new SpatialQueryResult(count, isTruncated);
    }
}
