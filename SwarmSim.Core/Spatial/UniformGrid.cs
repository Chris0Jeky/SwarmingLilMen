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
        int colReach = GetCellReach(radius, Cols);
        int rowReach = GetCellReach(radius, Rows);
        bool scanAllCols = colReach * 2 + 1 >= Cols;
        bool scanAllRows = rowReach * 2 + 1 >= Rows;
        int colCount = scanAllCols ? Cols : colReach * 2 + 1;
        int rowCount = scanAllRows ? Rows : rowReach * 2 + 1;
        int written = 0;
        truncated = false;

        for (int rowOffset = 0; rowOffset < rowCount; rowOffset++)
        {
            int row = scanAllRows ? rowOffset : WrapCell(centerRow - rowReach + rowOffset, Rows);
            for (int colOffset = 0; colOffset < colCount; colOffset++)
            {
                int col = scanAllCols ? colOffset : WrapCell(centerCol - colReach + colOffset, Cols);
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
    private int GetCellIndex(float x, float y)
    {
        int col = (int)(x / CellSize);
        int row = (int)(y / CellSize);

        // Clamp to valid range
        col = Math.Clamp(col, 0, Cols - 1);
        row = Math.Clamp(row, 0, Rows - 1);

        return col + row * Cols;
    }

    private int GetCellReach(float radius, int cellCount)
    {
        if (radius >= cellCount * CellSize)
            return cellCount;

        // Include one guard cell because floating-point division can classify a point lying just
        // below a cell edge into the next cell while it remains within the inclusive radius.
        return Math.Min(cellCount, (int)MathF.Ceiling(radius / CellSize) + 1);
    }

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
