using SwarmSim.Core.Utils;

namespace SwarmSim.Core;

/// <summary>
/// One toroidal scalar value per cell of a fixed grid over the world: the first non-agent state
/// in the engine. Nothing allocates after construction. Reads and writes use the cell containing
/// a point (nearest-cell, no interpolation); see docs/ACO.md for what a deposit means.
/// </summary>
public sealed class ScalarField
{
    /// <summary>
    /// Evaporation flushes smaller values to zero. Repeated multiplication otherwise stalls at the
    /// smallest subnormal and a dead cell would stay nonzero for ever.
    /// </summary>
    public const float FlushThreshold = 1e-30f;

    private readonly float[] _cells;
    private readonly float _worldWidth;
    private readonly float _worldHeight;

    public ScalarField(int columns, int rows, float worldWidth, float worldHeight)
    {
        if (columns <= 0 || rows <= 0)
            throw new ArgumentOutOfRangeException(nameof(columns), "A field needs at least one cell on each axis.");
        if (!float.IsFinite(worldWidth) || worldWidth <= 0f || !float.IsFinite(worldHeight) || worldHeight <= 0f)
            throw new ArgumentOutOfRangeException(nameof(worldWidth), "World extents must be finite and positive.");
        _cells = new float[checked(columns * rows)];
        Columns = columns;
        Rows = rows;
        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
    }

    public int Columns { get; }
    public int Rows { get; }
    /// <summary>Row-major cell values.</summary>
    public ReadOnlySpan<float> Cells => _cells;

    /// <summary>Sum of all cells, in cell order so the rounding is reproducible.</summary>
    public double Total()
    {
        double total = 0d;
        foreach (float cell in _cells) total += cell;
        return total;
    }

    public float Sample(float x, float y) => _cells[CellIndex(x, y)];

    /// <summary>Adds a finite, non-negative amount to the cell containing the point.</summary>
    public void Deposit(float x, float y, float amount)
    {
        if (!float.IsFinite(amount) || amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount), "A deposit must be finite and non-negative.");
        _cells[CellIndex(x, y)] += amount;
    }

    /// <summary>Multiplies every cell by 1 - rate, then flushes values below the threshold.</summary>
    public void Evaporate(float rate)
    {
        if (!(rate >= 0f && rate <= 1f))
            throw new ArgumentOutOfRangeException(nameof(rate), "An evaporation rate must lie in [0, 1].");
        float keep = 1f - rate;
        Span<float> cells = _cells;
        for (int i = 0; i < cells.Length; i++)
        {
            float value = cells[i] * keep;
            cells[i] = value < FlushThreshold ? 0f : value;
        }
    }

    private int CellIndex(float x, float y)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y))
            throw new ArgumentException("Field coordinates must be finite.");
        (float wrappedX, float wrappedY) = MathUtils.WrapPosition(x, y, _worldWidth, _worldHeight);
        // Rounding can land a coordinate just below an extent on the extent's own cell boundary.
        int column = Math.Min((int)(wrappedX / _worldWidth * Columns), Columns - 1);
        int row = Math.Min((int)(wrappedY / _worldHeight * Rows), Rows - 1);
        return row * Columns + column;
    }
}
