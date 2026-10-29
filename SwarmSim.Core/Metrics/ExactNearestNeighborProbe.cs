namespace SwarmSim.Core.Metrics;

/// <summary>
/// Exact all-pairs nearest-neighbor analysis, independent of steering neighborhoods. O(N²) work
/// plus sorting; opt in only for affordable population sizes/offline samples. Four double buffers
/// are allocated once. Capacity exhaustion is an error, never truncation or approximation.
/// </summary>
public sealed class ExactNearestNeighborProbe : IMetricProbe
{
    private static readonly IReadOnlyList<string> Names = Array.AsReadOnly(new[]
        { "nearestNeighborCount", "nearestNeighborMean", "nearestNeighborMin", "nearestNeighborP50", "nearestNeighborP95", "nearestNeighborMax" });
    private readonly double[] _x;
    private readonly double[] _y;
    private readonly double[] _distances;
    private readonly double[] _sorted;
    private int _sampleCount;

    public ExactNearestNeighborProbe(int capacity)
    {
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _x = new double[capacity];
        _y = new double[capacity];
        _distances = new double[capacity];
        _sorted = new double[capacity];
    }

    public IReadOnlyList<string> Channels => Names;
    public bool IsDeterministic => true;
    public int Capacity => _distances.Length;

    /// <summary>Agent-ordered distances from the last sample; empty for fewer than two agents.</summary>
    public ReadOnlySpan<double> NearestDistances => _distances.AsSpan(0, _sampleCount);

    /// <summary>Sorted distances from the last sample. Both views are overwritten on the next sample.</summary>
    public ReadOnlySpan<double> SortedNearestDistances => _sorted.AsSpan(0, _sampleCount);

    public void Sample(in MetricFrame frame, Span<double> output)
    {
        MetricMath.RequireOutput(output, Names.Count);
        if (frame.Count > Capacity)
            throw new ArgumentException($"Analysis population {frame.Count} exceeds exact-neighbor capacity {Capacity}.", nameof(frame));
        _sampleCount = frame.Count < 2 ? 0 : frame.Count;
        output[0] = _sampleCount;
        if (_sampleCount == 0)
        {
            output.Slice(1, 5).Fill(double.NaN);
            return;
        }

        for (int i = 0; i < frame.Count; i++)
        {
            KinematicSample agent = frame.Agents[i];
            _x[i] = frame.Domain.IsPeriodic ? MetricMath.CenteredRemainder(agent.X, frame.Domain.Width) : agent.X;
            _y[i] = frame.Domain.IsPeriodic ? MetricMath.CenteredRemainder(agent.Y, frame.Domain.Height) : agent.Y;
            _distances[i] = double.PositiveInfinity;
        }
        for (int i = 0; i < frame.Count; i++)
        {
            for (int j = i + 1; j < frame.Count; j++)
            {
                double dx = Math.Abs(_x[j] - _x[i]);
                double dy = Math.Abs(_y[j] - _y[i]);
                if (frame.Domain.IsPeriodic)
                {
                    dx = Math.Min(dx, frame.Domain.Width - dx);
                    dy = Math.Min(dy, frame.Domain.Height - dy);
                }
                double squared = dx * dx + dy * dy;
                if (squared < _distances[i]) _distances[i] = squared;
                if (squared < _distances[j]) _distances[j] = squared;
            }
        }

        double total = 0d;
        for (int i = 0; i < frame.Count; i++)
        {
            _distances[i] = Math.Sqrt(_distances[i]);
            total += _distances[i];
        }
        Array.Copy(_distances, _sorted, frame.Count);
        Array.Sort(_sorted, 0, frame.Count);
        output[1] = total / frame.Count;
        output[2] = _sorted[0];
        output[3] = Percentile(0.5d);
        output[4] = Percentile(0.95d);
        output[5] = _sorted[frame.Count - 1];
    }

    private double Percentile(double quantile)
    {
        double rank = quantile * (_sampleCount - 1);
        int lower = (int)rank;
        int upper = Math.Min(lower + 1, _sampleCount - 1);
        return _sorted[lower] + (rank - lower) * (_sorted[upper] - _sorted[lower]);
    }
}
