namespace SwarmSim.Core.Canonical;

/// <summary>
/// Caller-owned, reusable buffers for whole-world researcher diagnostics from the last tick.
/// This is telemetry, not an individual boid's policy observation.
/// </summary>
/// <remarks>
/// Allocate once, then explicitly call <see cref="CanonicalWorld.CaptureTelemetryInto"/> when
/// diagnostics are needed. Capturing and stepping allocate no managed memory after setup for
/// the built-in rules and spatial indexes. Advancing the world does not change this object;
/// another capture into the same destination replaces its contents. Use separate destinations
/// to retain multiple ticks. A world and its telemetry destination are not thread-safe.
/// </remarks>
public sealed class CanonicalTelemetry
{
    private readonly float[] _nearestDistances;
    private readonly float[] _nearestAngles;
    private readonly int[] _whiskerCounts;
    private readonly bool[] _queryTruncated;

    public CanonicalTelemetry(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        Capacity = capacity;
        _nearestDistances = new float[capacity];
        _nearestAngles = new float[capacity];
        _whiskerCounts = new int[capacity];
        _queryTruncated = new bool[capacity];
    }

    public int Capacity { get; }
    public ulong TickCount { get; private set; }
    public int AgentCount { get; private set; }
    public float AverageNeighborCount { get; private set; }
    public float AverageNeighborDistance { get; private set; }
    public float MinNeighborDistance { get; private set; }
    public float MaxNeighborDistance { get; private set; }
    public float AverageNeighborWeight { get; private set; }
    public float AverageSeparationMagnitude { get; private set; }
    public float AverageAlignmentMagnitude { get; private set; }
    public float AverageCohesionMagnitude { get; private set; }
    public (int Min, int Max, float Avg) NeighborCountStats { get; private set; }
    public bool SeparationPriorityTriggered { get; private set; }
    public int TruncatedCount { get; private set; }

    public ReadOnlySpan<float> NearestDistances => _nearestDistances.AsSpan(0, AgentCount);
    public ReadOnlySpan<float> NearestAngles => _nearestAngles.AsSpan(0, AgentCount);
    public ReadOnlySpan<int> WhiskerCounts => _whiskerCounts.AsSpan(0, AgentCount);

    /// <summary>
    /// Whether each boid's bounded neighbor query omitted candidates during the captured tick,
    /// before integration. This can differ from a query against the world's current positions.
    /// </summary>
    public ReadOnlySpan<bool> QueryTruncated => _queryTruncated.AsSpan(0, AgentCount);

    internal void Capture(
        ulong tickCount,
        RuleInstrumentation instrumentation,
        float averageDistance,
        float minDistance,
        float maxDistance,
        bool separationPriorityTriggered,
        ReadOnlySpan<float> nearestDistances,
        ReadOnlySpan<float> nearestAngles,
        ReadOnlySpan<int> whiskerCounts)
    {
        TickCount = tickCount;
        AgentCount = nearestDistances.Length;
        NeighborCountStats = instrumentation.NeighborCountStats;
        AverageNeighborCount = NeighborCountStats.Avg;
        AverageNeighborDistance = averageDistance;
        MinNeighborDistance = minDistance;
        MaxNeighborDistance = maxDistance;
        AverageNeighborWeight = instrumentation.AverageNeighborWeight;
        AverageSeparationMagnitude = instrumentation.AverageSeparationMagnitude;
        AverageAlignmentMagnitude = instrumentation.AverageAlignmentMagnitude;
        AverageCohesionMagnitude = instrumentation.AverageCohesionMagnitude;
        SeparationPriorityTriggered = separationPriorityTriggered;
        TruncatedCount = instrumentation.TruncatedCount;

        nearestDistances.CopyTo(_nearestDistances);
        nearestAngles.CopyTo(_nearestAngles);
        whiskerCounts.CopyTo(_whiskerCounts);
        instrumentation.QueryTruncated.CopyTo(_queryTruncated);
    }

}
