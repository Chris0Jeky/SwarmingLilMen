using SwarmSim.Core.Canonical;

namespace SwarmSim.Core.Metrics;

/// <summary>
/// Optional canonical-boids diagnostics, registered outside the metric kernel. Sample after the
/// corresponding completed step, before instrumentation is overwritten. These are steering
/// observations, not exact spatial statistics; query truncation is exposed explicitly.
/// </summary>
public sealed class BoidsRuleMetricProbe : IMetricProbe
{
    private static readonly IReadOnlyList<string> Names = Array.AsReadOnly(new[]
    {
        "boids.separationMagnitudeMean", "boids.alignmentMagnitudeMean", "boids.cohesionMagnitudeMean",
        "boids.steeringMagnitudeMean", "boids.truncatedQueryFraction"
    });
    private readonly RuleInstrumentation _instrumentation;

    public BoidsRuleMetricProbe(RuleInstrumentation instrumentation) =>
        _instrumentation = instrumentation ?? throw new ArgumentNullException(nameof(instrumentation));

    public IReadOnlyList<string> Channels => Names;
    public bool IsDeterministic => true;

    public void Sample(in MetricFrame frame, Span<double> output)
    {
        MetricMath.RequireOutput(output, Names.Count);
        ReadOnlySpan<float> steeringSquared = _instrumentation.SteeringMagnitudesSquared;
        ReadOnlySpan<bool> truncated = _instrumentation.QueryTruncated;
        if (steeringSquared.Length != frame.Count)
            throw new ArgumentException("Boids instrumentation and analysis population must refer to the same completed step.", nameof(frame));
        output.Slice(0, Names.Count).Clear();
        if (frame.Count == 0) return;
        for (int i = 0; i < frame.Count; i++)
        {
            if (!_instrumentation.TryGetMetrics(i, out RuleInstrumentation.Metrics metrics) ||
                !IsMagnitude(metrics.SeparationMagnitude) || !IsMagnitude(metrics.AlignmentMagnitude) ||
                !IsMagnitude(metrics.CohesionMagnitude) || !IsMagnitude(steeringSquared[i]))
                throw new InvalidOperationException("Boids instrumentation contains invalid magnitude data.");
            output[0] += metrics.SeparationMagnitude;
            output[1] += metrics.AlignmentMagnitude;
            output[2] += metrics.CohesionMagnitude;
            output[3] += Math.Sqrt(steeringSquared[i]);
            if (truncated[i]) output[4] += 1d;
        }
        for (int i = 0; i < Names.Count; i++) output[i] /= frame.Count;
    }

    private static bool IsMagnitude(float value) => float.IsFinite(value) && value >= 0f;
}
