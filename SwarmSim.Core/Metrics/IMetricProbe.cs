namespace SwarmSim.Core.Metrics;

/// <summary>
/// An explicitly registered metric source. Channel names/order are fixed for the probe's lifetime.
/// NaN represents a mathematically undefined or unavailable value; infinity is never a valid result.
/// Implementations must not retain the borrowed frame or allocate while sampling valid inputs.
/// Probe instances and plans are reusable but are not thread-safe.
/// </summary>
public interface IMetricProbe
{
    IReadOnlyList<string> Channels { get; }
    bool IsDeterministic { get; }
    void Sample(in MetricFrame frame, Span<double> output);
}
