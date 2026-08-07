using System;
using SwarmSim.Core.Spatial;
using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Canonical;

/// <summary>
/// Toroidal uniform-grid implementation of <see cref="ISpatialIndex"/>.
/// </summary>
public sealed class GridSpatialIndex : ISpatialIndex
{
    private readonly float _cellSize;
    private readonly float _worldWidth;
    private readonly float _worldHeight;
    private UniformGrid? _grid;
    private float[] _xPositions = Array.Empty<float>();
    private float[] _yPositions = Array.Empty<float>();
    private int _capacity;
    private bool _isRebuilt;

    /// <summary>
    /// Creates a grid for a toroidal world.
    /// </summary>
    /// <param name="cellSize">Positive grid cell size.</param>
    /// <param name="worldWidth">Positive world width.</param>
    /// <param name="worldHeight">Positive world height.</param>
    public GridSpatialIndex(float cellSize, float worldWidth, float worldHeight)
    {
        if (!float.IsFinite(cellSize) || cellSize <= 0f)
            throw new ArgumentException("Cell size must be finite and positive", nameof(cellSize));

        if (!float.IsFinite(worldWidth) || worldWidth <= 0f ||
            !float.IsFinite(worldHeight) || worldHeight <= 0f)
            throw new ArgumentException("World dimensions must be finite and positive");

        _cellSize = cellSize;
        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
    }

    /// <inheritdoc />
    public void Initialize(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

        _capacity = capacity;
        _xPositions = new float[_capacity];
        _yPositions = new float[_capacity];
        _grid = new UniformGrid(_cellSize, _worldWidth, _worldHeight, _capacity);
        _isRebuilt = false;
    }

    /// <inheritdoc />
    public void Rebuild(ReadOnlySpan<Boid> boids)
    {
        if (_grid is null)
            throw new InvalidOperationException("GridSpatialIndex has not been initialized.");

        if (boids.Length > _capacity)
            throw new ArgumentException("Boid count exceeds initialized capacity.", nameof(boids));

        for (int i = 0; i < boids.Length; i++)
        {
            _xPositions[i] = MathUtils.Wrap(boids[i].Position.X, _worldWidth);
            _yPositions[i] = MathUtils.Wrap(boids[i].Position.Y, _worldHeight);
        }

        _grid.Rebuild(_xPositions, _yPositions, boids.Length);
        _isRebuilt = true;
    }

    /// <inheritdoc />
    public SpatialQueryResult QueryNeighbors(ReadOnlySpan<Boid> boids, int selfIndex, float radius, Span<int> results)
    {
        if (_grid is null)
            throw new InvalidOperationException("GridSpatialIndex has not been initialized.");

        if (!_isRebuilt)
            throw new InvalidOperationException("GridSpatialIndex must be rebuilt before querying.");

        if (!float.IsFinite(radius) || radius < 0f)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be finite and non-negative.");

        if (selfIndex < 0 || selfIndex >= boids.Length)
            return new SpatialQueryResult(0, false);

        if (boids.Length > _capacity)
            throw new ArgumentException("Boid count exceeds initialized capacity.", nameof(boids));

        int found = _grid.QueryRadiusToroidal(
            _xPositions[selfIndex],
            _yPositions[selfIndex],
            radius,
            selfIndex,
            _xPositions,
            _yPositions,
            boids.Length,
            results,
            out bool isTruncated);
        return new SpatialQueryResult(found, isTruncated);
    }
}
