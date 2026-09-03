using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Spatial;

/// <summary>
/// Uniform spatial grid for fast neighbor queries using linked lists.
/// Uses Head[cell] + Next[agent] arrays for O(1) insertion and efficient queries.
///
/// INVARIANTS:
/// - Cell size should be approximately equal to interaction radius for optimal performance
/// - Rebuild() must be called every tick before queries
/// - All queries are bounded by world dimensions
/// - No allocations during Rebuild() or Query after initialization
///
/// ALGORITHM:
/// - Grid cells are indexed as: cellIdx = col + row * Cols
/// - Each cell has a linked list: Head[cell] points to first agent, Next[agent] points to next agent
/// - Query scans 3×3 neighborhood for local agents
/// </summary>
public sealed class UniformGrid
{
    // Grid configuration
    public float CellSize { get; private set; }
    public int Cols { get; private set; }
    public int Rows { get; private set; }
    public int TotalCells { get; private set; }

    // World bounds
    private float _worldWidth;
    private float _worldHeight;

    // Linked list structure
    // Head[cell] = index of first agent in cell (-1 if empty)
    // Next[agent] = index of next agent in same cell (-1 if last)
    private int[] _head = null!;
    private int[] _next = null!;

    private int _capacity;

    /// <summary>
    /// Slack, as a fraction of the world extent, that the directional cell walk adds to the query
    /// radius, so the walk always reaches at least as far as the float acceptance test it feeds.
    ///
    /// Error budget for two coordinates in <c>[0, extent)</c>, worst case about
    /// <c>1.5 * extent * 2^-24</c>:
    /// <list type="bullet">
    /// <item>the subtraction of two extent-magnitude coordinates is correctly rounded, so at most
    /// <c>extent * 2^-24</c>;</item>
    /// <item><c>delta % extent</c> is exact, and so is the fold by <c>+/- extent</c> -- the folded
    /// value lies in <c>(extent/2, extent)</c>, where Sterbenz's lemma makes the subtraction
    /// exact. These terms contribute nothing;</item>
    /// <item>the squared comparison <c>fl(dx*dx) &lt;= fl(r*r)</c> admits <c>|dx|</c> up to about
    /// <c>r * (1 + 2^-24)</c>, and <c>r &lt; extent/2</c> on this path, so under
    /// <c>extent * 2^-25</c>.</item>
    /// </list>
    ///
    /// The same slack absorbs the other way a neighbour was dropped: <see cref="GetCellIndex"/>
    /// can file an agent one cell above the one that geometrically contains it when the float
    /// quotient rounds across an edge. Such an agent sits within one ulp of that edge, and
    /// whenever the float test accepts it the walk's coverage exceeds the radius by no more than
    /// this slack, so the widened walk always reaches its cell.
    ///
    /// The constant below is <c>8 * 2^-24</c> -- roughly five times that bound. The margin is
    /// deliberate: it costs at most one extra cell per side, and only when the radius lands within
    /// a few ulps of a cell edge. **Do not tighten it to the derived figure.** Losing the margin
    /// is how the dropped neighbours this constant exists to prevent come back.
    /// </summary>
    private const double MinimumImageUlpSlack = 8.0 * 5.9604644775390625e-8;

    /// <summary>
    /// Creates a new uniform grid with the specified cell size and world dimensions.
    /// </summary>
    /// <param name="cellSize">Size of each grid cell (should be ~= interaction radius)</param>
    /// <param name="worldWidth">Width of the world</param>
    /// <param name="worldHeight">Height of the world</param>
    /// <param name="capacity">Maximum number of agents</param>
    public UniformGrid(float cellSize, float worldWidth, float worldHeight, int capacity)
    {
        if (cellSize <= 0f)
            throw new ArgumentException("Cell size must be positive", nameof(cellSize));
        if (worldWidth <= 0f || worldHeight <= 0f)
            throw new ArgumentException("World dimensions must be positive");
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be positive", nameof(capacity));

        CellSize = cellSize;
        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
        _capacity = capacity;

        // Calculate grid dimensions (round up to cover entire world)
        Cols = (int)MathF.Ceiling(worldWidth / cellSize);
        Rows = (int)MathF.Ceiling(worldHeight / cellSize);
        TotalCells = Cols * Rows;

        // Allocate arrays
        _head = new int[TotalCells];
        _next = new int[capacity];

        // Initialize to empty (-1 = no agent)
        Array.Fill(_head, -1);
    }

    /// <summary>
    /// Rebuilds the grid from agent positions. Must be called every tick.
    /// O(n) where n = agent count.
    /// </summary>
    /// <param name="x">Agent X positions</param>
    /// <param name="y">Agent Y positions</param>
    /// <param name="count">Number of active agents</param>
    public void Rebuild(float[] x, float[] y, int count)
    {
        // Clear all cells
        Array.Fill(_head, -1);

        // Insert each agent into its cell
        for (int i = 0; i < count; i++)
        {
            int cellIdx = GetCellIndex(x[i], y[i]);

            // Push agent to front of cell's linked list
            _next[i] = _head[cellIdx];
            _head[cellIdx] = i;
        }
    }

    /// <summary>
    /// Queries all agents in the 3×3 neighborhood around the given position.
    /// Calls the callback for each agent found (including the center cell).
    /// </summary>
    /// <param name="x">Query position X</param>
    /// <param name="y">Query position Y</param>
    /// <param name="callback">Called for each agent index found</param>
    public void Query3x3(float x, float y, Action<int> callback)
    {
        // Get center cell coordinates
        int centerCol = (int)(x / CellSize);
        int centerRow = (int)(y / CellSize);

        // Clamp to valid range
        centerCol = Math.Clamp(centerCol, 0, Cols - 1);
        centerRow = Math.Clamp(centerRow, 0, Rows - 1);

        // Scan 3×3 neighborhood
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int col = centerCol + dx;
                int row = centerRow + dy;

                // Skip out-of-bounds cells
                if (col < 0 || col >= Cols || row < 0 || row >= Rows)
                    continue;

                int cellIdx = col + row * Cols;

                // Walk the linked list for this cell
                int agentIdx = _head[cellIdx];
                while (agentIdx != -1)
                {
                    callback(agentIdx);
                    agentIdx = _next[agentIdx];
                }
            }
        }
    }

    /// <summary>
    /// Queries agents in 3×3 neighborhood and writes indices to a buffer.
    /// Returns the number of agents found.
    /// </summary>
    /// <param name="x">Query position X</param>
    /// <param name="y">Query position Y</param>
    /// <param name="buffer">Output buffer for agent indices</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <returns>Number of agents found (may exceed maxResults)</returns>
    public int Query3x3(float x, float y, Span<int> buffer, int maxResults)
    {
        int count = 0;

        // Get center cell coordinates
        int centerCol = (int)(x / CellSize);
        int centerRow = (int)(y / CellSize);

        // Clamp to valid range
        centerCol = Math.Clamp(centerCol, 0, Cols - 1);
        centerRow = Math.Clamp(centerRow, 0, Rows - 1);

        // Scan 3×3 neighborhood
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int col = centerCol + dx;
                int row = centerRow + dy;

                // Skip out-of-bounds cells
                if (col < 0 || col >= Cols || row < 0 || row >= Rows)
                    continue;

                int cellIdx = col + row * Cols;

                // Walk the linked list for this cell
                int agentIdx = _head[cellIdx];
                while (agentIdx != -1)
                {
                    if (count < maxResults)
                    {
                        buffer[count] = agentIdx;
                    }
                    count++;
                    agentIdx = _next[agentIdx];
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Queries a toroidal circular neighborhood for the canonical boids path without allocating.
    /// The legacy <see cref="Query3x3(float, float, Span{int}, int)"/> behavior is intentionally unchanged.
    /// </summary>
    /// <param name="x">Query X position.</param>
    /// <param name="y">Query Y position.</param>
    /// <param name="radius">Finite, non-negative query radius.</param>
    /// <param name="selfIndex">Index excluded from results.</param>
    /// <param name="xPositions">Current X positions.</param>
    /// <param name="yPositions">Current Y positions.</param>
    /// <param name="count">Number of active positions.</param>
    /// <param name="buffer">Caller-owned output buffer.</param>
    /// <param name="truncated">Set when qualifying agents do not fit in <paramref name="buffer"/>.</param>
    /// <returns>The number of indices written, sorted ascending by index.</returns>
    internal int QueryRadiusToroidal(
        float x,
        float y,
        float radius,
        int selfIndex,
        ReadOnlySpan<float> xPositions,
        ReadOnlySpan<float> yPositions,
        int count,
        Span<int> buffer,
        out bool truncated)
    {
        if (!float.IsFinite(radius) || radius < 0f)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be finite and non-negative.");

        if (count < 0 || count > xPositions.Length || count > yPositions.Length)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must fit both position spans.");

        int activeCount = count;
        float radiusSquared = radius * radius;
        int centerCol = Math.Clamp((int)(x / CellSize), 0, Cols - 1);
        int centerRow = Math.Clamp((int)(y / CellSize), 0, Rows - 1);
        GetDirectionalCellReach(
            x, radius, _worldWidth, Cols, centerCol,
            out int colsBefore, out int colsAfter, out bool scanAllCols);
        GetDirectionalCellReach(
            y, radius, _worldHeight, Rows, centerRow,
            out int rowsBefore, out int rowsAfter, out bool scanAllRows);
        int colCount = scanAllCols ? Cols : colsBefore + colsAfter + 1;
        int rowCount = scanAllRows ? Rows : rowsBefore + rowsAfter + 1;
        int written = 0;
        truncated = false;

        for (int rowOffset = 0; rowOffset < rowCount; rowOffset++)
        {
            int row = scanAllRows ? rowOffset : WrapCell(centerRow - rowsBefore + rowOffset, Rows);
            for (int colOffset = 0; colOffset < colCount; colOffset++)
            {
                int col = scanAllCols ? colOffset : WrapCell(centerCol - colsBefore + colOffset, Cols);
                int agentIndex = _head[col + row * Cols];
                while (agentIndex != -1)
                {
                    if (agentIndex != selfIndex && agentIndex < activeCount)
                    {
                        float dx = MathUtils.MinimumImageDelta(xPositions[agentIndex] - x, _worldWidth);
                        float dy = MathUtils.MinimumImageDelta(yPositions[agentIndex] - y, _worldHeight);
                        if (dx * dx + dy * dy <= radiusSquared)
                        {
                            InsertSortedBounded(buffer, ref written, agentIndex, ref truncated);
                        }
                    }

                    agentIndex = _next[agentIndex];
                }
            }
        }

        return written;
    }

    /// <summary>
    /// Gets the cell index for the given world position.
    /// </summary>
    /// <remarks>
    /// The float quotient here can round ACROSS a cell edge and file an agent in the cell above
    /// the one that geometrically contains it -- 383.624847f / 42.6249847f is 8.999999642 exactly,
    /// but the nearest binary32 is 9. That is deliberately NOT corrected. Snapping it would change
    /// how the legacy engine bins agents, and the legacy 3x3 query derives its centre from the
    /// same raw quotient, so correcting one without the other drops neighbours outright. The
    /// toroidal query tolerates the misplacement instead: a mis-binned agent sits within one ulp
    /// of the edge, and <see cref="MinimumImageUlpSlack"/> already widens the directional walk by
    /// far more than that. See the argument on that constant.
    /// </remarks>
    private int GetCellIndex(float x, float y)
    {
        int col = (int)(x / CellSize);
        int row = (int)(y / CellSize);

        // Clamp to valid range
        col = Math.Clamp(col, 0, Cols - 1);
        row = Math.Clamp(row, 0, Rows - 1);

        return col + row * Cols;
    }

    private void GetDirectionalCellReach(
        float coordinate,
        float radius,
        float extent,
        int cellCount,
        int centerCell,
        out int cellsBefore,
        out int cellsAfter,
        out bool scanAll)
    {
        if (cellCount == 1 || radius >= extent * 0.5f)
        {
            cellsBefore = 0;
            cellsAfter = 0;
            scanAll = true;
            return;
        }

        // Walk outward one cell at a time, accumulating each cell's ACTUAL extent, and stop as
        // soon as the accumulated span covers the radius. Deriving the reach from the wrapped
        // endpoints' cell IDs instead is wrong whenever the world extent is not a whole multiple
        // of CellSize: the terminal cell is short, so a circular interval can cross the whole of
        // it while both wrapped endpoints still land back in the centre cell, and the qualifying
        // neighbours inside that terminal cell are never scanned.
        //
        // The running total is a double on purpose. Accumulating cell extents in float rounds in
        // BOTH directions, and when it rounds up the walk stops one cell early and silently drops
        // neighbours strictly inside the radius. That needs a cell size not exactly representable
        // in binary32 plus a walk of hundreds of cells (radius/cellSize large), so no caller in
        // this repository can reach it today -- every construction site passes
        // cellSize == SenseRadius -- but the public constructor invites cellSize < radius, which
        // is the ordinary grid-tuning move. Doubles remove the drift outright; the walk is O(cells)
        // per query, not per neighbour, so the cost is nil. The comparisons are non-strict so a
        // remaining per-edge ulp can only widen the scan, never shorten it.
        //
        // The walk must nevertheless reach FURTHER than the exact radius, because the acceptance
        // test it feeds is not exact. Callers admit a neighbour when the float
        // MathUtils.MinimumImageDelta is within the radius, and across the seam that helper
        // subtracts two coordinates of world magnitude and then adds the extent back -- so its
        // result can understate the true separation by a couple of ulps OF THE EXTENT, not of the
        // radius. A walk measured against the exact radius therefore stops one cell short of a
        // neighbour the float test accepts, and Grid drops a neighbour Naive returns. Widening the
        // reach by that same error bound restores the contract; it can only pull in one extra cell
        // when the radius lands within a few ulps of a cell edge, so the cost is nil.
        double reach = (double)radius + (double)extent * MinimumImageUlpSlack;

        cellsBefore = 0;
        double coveredBefore = (double)coordinate - (double)centerCell * CellSize;
        int cursor = centerCell;
        while (coveredBefore <= reach && cellsBefore + 1 < cellCount)
        {
            cursor = WrapCell(cursor - 1, cellCount);
            coveredBefore += GetCellExtent(cursor, extent);
            cellsBefore++;
        }

        cellsAfter = 0;
        double coveredAfter = GetCellUpperEdge(centerCell, extent) - (double)coordinate;
        cursor = centerCell;
        while (coveredAfter <= reach && cellsBefore + cellsAfter + 1 < cellCount)
        {
            cursor = WrapCell(cursor + 1, cellCount);
            coveredAfter += GetCellExtent(cursor, extent);
            cellsAfter++;
        }

        scanAll = cellsBefore + cellsAfter + 1 >= cellCount;

        if (scanAll)
        {
            cellsBefore = 0;
            cellsAfter = 0;
        }
    }

    /// <summary>
    /// Gets the actual extent of a cell along one axis. The terminal cell is shorter than
    /// <see cref="CellSize"/> whenever the world extent is not a whole multiple of it.
    /// Returned as a double so a directional walk can accumulate without rounding drift.
    /// </summary>
    private double GetCellExtent(int cell, float extent)
        => GetCellUpperEdge(cell, extent) - (double)cell * CellSize;

    /// <summary>
    /// Gets the upper edge of a cell along one axis, clamped to the world extent.
    /// </summary>
    private double GetCellUpperEdge(int cell, float extent)
        => Math.Min((double)(cell + 1) * CellSize, extent);

    private static int WrapCell(int value, int length)
    {
        int wrapped = value % length;
        return wrapped < 0 ? wrapped + length : wrapped;
    }

    private static void InsertSortedBounded(Span<int> buffer, ref int written, int candidate, ref bool truncated)
    {
        if (written < buffer.Length)
        {
            int insertAt = written;
            while (insertAt > 0 && buffer[insertAt - 1] > candidate)
            {
                buffer[insertAt] = buffer[insertAt - 1];
                insertAt--;
            }
            buffer[insertAt] = candidate;
            written++;
            return;
        }

        truncated = true;
        if (buffer.IsEmpty || candidate >= buffer[written - 1])
            return;

        int replaceAt = written - 1;
        while (replaceAt > 0 && buffer[replaceAt - 1] > candidate)
        {
            buffer[replaceAt] = buffer[replaceAt - 1];
            replaceAt--;
        }
        buffer[replaceAt] = candidate;
    }

    /// <summary>
    /// Gets statistics about the grid for profiling/debugging.
    /// </summary>
    public GridStats GetStats(int agentCount)
    {
        int emptyCells = 0;
        int maxAgentsPerCell = 0;
        int totalAgents = 0;

        for (int i = 0; i < TotalCells; i++)
        {
            int cellCount = 0;
            int agentIdx = _head[i];

            while (agentIdx != -1)
            {
                cellCount++;
                totalAgents++;
                agentIdx = _next[agentIdx];
            }

            if (cellCount == 0)
                emptyCells++;
            else if (cellCount > maxAgentsPerCell)
                maxAgentsPerCell = cellCount;
        }

        int occupiedCells = TotalCells - emptyCells;
        float avgAgentsPerCell = occupiedCells > 0 ? (float)totalAgents / occupiedCells : 0f;

        return new GridStats
        {
            TotalCells = TotalCells,
            OccupiedCells = occupiedCells,
            EmptyCells = emptyCells,
            MaxAgentsPerCell = maxAgentsPerCell,
            AvgAgentsPerOccupiedCell = avgAgentsPerCell
        };
    }
}

/// <summary>
/// Statistics about grid occupancy for profiling.
/// </summary>
public record struct GridStats
{
    public int TotalCells { get; init; }
    public int OccupiedCells { get; init; }
    public int EmptyCells { get; init; }
    public int MaxAgentsPerCell { get; init; }
    public float AvgAgentsPerOccupiedCell { get; init; }
}
