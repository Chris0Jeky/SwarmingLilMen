using SwarmSim.Core.Metrics;

namespace SwarmSim.Core.Firefly;

/// <summary>
/// Circular phase statistics from typed oscillator state. The kinematic frame deliberately carries no
/// phase. Coherence is |mean(exp(i*phase))|; a mean phase below the binary32 resolution threshold is NaN.
/// A null world constructs channel metadata only. Sampling does not allocate or advance simulation state.
/// </summary>
public sealed class FireflyMetricProbe(FireflyWorld? world) : IMetricProbe
{
    private static readonly IReadOnlyList<string> Names = Array.AsReadOnly(new[]
        { "firefly.coherence", "firefly.meanPhase", "firefly.meanNaturalFrequency" });

    public IReadOnlyList<string> Channels => Names;
    public bool IsDeterministic => true;

    public void Sample(in MetricFrame frame, Span<double> output)
    {
        MetricMath.RequireOutput(output, Names.Count);
        if (world is null)
            throw new InvalidOperationException("This probe holds only channel metadata.");
        double sumSin = 0d, sumCos = 0d, sumFrequency = 0d;
        foreach (Oscillator agent in world.Agents)
        {
            (double sin, double cos) = Math.SinCos(agent.Phase);
            sumSin += sin;
            sumCos += cos;
            sumFrequency += agent.NaturalFrequency;
        }
        double meanSin = sumSin / world.Count;
        double meanCos = sumCos / world.Count;
        double coherence = Math.Clamp(Math.Sqrt(meanSin * meanSin + meanCos * meanCos), 0d, 1d);
        double phase = coherence <= 1e-6d ? double.NaN : Math.Atan2(meanSin, meanCos);
        if (phase < 0d) phase += Math.Tau;
        output[0] = coherence;
        output[1] = phase;
        output[2] = sumFrequency / world.Count;
    }
}
