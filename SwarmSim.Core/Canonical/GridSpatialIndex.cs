using System;
using SwarmSim.Core.Spatial;

namespace SwarmSim.Core.Canonical;

public sealed class GridSpatialIndex : ISpatialIndex
{
    private readonly float _cellSize;
    private readonly float _worldWidth;
    private readonly float _worldHeight;
    private UniformGrid? _grid;
    private float[] _xPositions = Array.Empty<float>();
    private float[] _yPositions = Array.Empty<float>();
    private int _capacity;

    public GridSpatialIndex(float cellSize, float worldWidth, float worldHeight)
    {
        if (cellSize <= 0f)
            throw new ArgumentException("Cell size must be positive", nameof(cellSize));

        if (worldWidth <= 0f || worldHeight <= 0f)
            throw new ArgumentException("World dimensions must be positive");

        _cellSize = cellSize;
        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
    }

    public void Initialize(int capacity)
    {
        _capacity = Math.Max(capacity, 1);
        _xPositions = new float[_capacity];
        _yPositions = new float[_capacity];
        _grid = new UniformGrid(_cellSize, _worldWidth, _worldHeight, _capacity);
    }

    public void Rebuild(ReadOnlySpan<Boid> boids)
    {
        if (_grid is null)
            throw new InvalidOperationException("GridSpatialIndex has not been initialized.");

        int count = Math.Min(boids.Length, _capacity);
        for (int i = 0; i < count; i++)
        {
            _xPositions[i] = boids[i].Position.X;
            _yPositions[i] = boids[i].Position.Y;
        }

        _grid.Rebuild(_xPositions, _yPositions, count);
    }

    public int QueryNeighbors(ReadOnlySpan<Boid> boids, int selfIndex, float radius, Span<int> results)
    {
        if (_grid is null)
            return 0;

        if (selfIndex < 0 || selfIndex >= boids.Length)
            return 0;

        if (results.IsEmpty)
            return 0;

        Vec2 position = boids[selfIndex].Position;
        int found = _grid.Query3x3(position.X, position.Y, results, results.Length);
        return Math.Clamp(found, 0, results.Length);
    }
}
