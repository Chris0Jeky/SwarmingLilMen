namespace SwarmSim.Core.Metrics;

/// <summary>
/// Explicit operational measurements supplied by a caller around a tick. These channels are
/// nondeterministic and must stay outside byte-repeatable physical-metric/replay defaults.
/// </summary>
public sealed class RuntimeCostMetricProbe : IMetricProbe
{
    private static readonly IReadOnlyList<string> Names = Array.AsReadOnly(new[]
        { "runtime.wallMilliseconds", "runtime.allocatedBytes" });
    private double _wallMilliseconds = double.NaN;
    private double _allocatedBytes = double.NaN;

    public IReadOnlyList<string> Channels => Names;
    public bool IsDeterministic => false;

    /// <summary>Records one caller-defined tick interval; reads return that interval until replaced.</summary>
    public void Record(double wallMilliseconds, long allocatedBytes)
    {
        if (!double.IsFinite(wallMilliseconds) || wallMilliseconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(wallMilliseconds));
        if (allocatedBytes < 0L) throw new ArgumentOutOfRangeException(nameof(allocatedBytes));
        _wallMilliseconds = wallMilliseconds;
        _allocatedBytes = allocatedBytes;
    }

    public void Sample(in MetricFrame frame, Span<double> output)
    {
        MetricMath.RequireOutput(output, Names.Count);
        output[0] = _wallMilliseconds;
        output[1] = _allocatedBytes;
    }
}
