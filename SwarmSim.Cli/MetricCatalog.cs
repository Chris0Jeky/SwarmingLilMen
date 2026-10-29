using SwarmSim.Core.Canonical;
using SwarmSim.Core.Metrics;

namespace SwarmSim.Cli;

/// <summary>CLI registration policy; the reusable registry itself remains engine-neutral.</summary>
internal static class MetricCatalog
{
    internal const int ContractVersion = 1;
    internal static readonly IReadOnlyList<string> DefaultMetrics = Array.AsReadOnly(new[]
        { "population", "meanSpeed", "polarization" });

    /// <summary>Checks names, uniqueness and engine support without constructing a simulation.</summary>
    internal static bool ValidateSelection(string engine, string[] requestedMetrics) =>
        CreatePlan(engine, requestedMetrics, 0, null, new RuntimeCostMetricProbe()).IsDeterministic;

    internal static MetricPlan CreatePlan(string engine, string[] requestedMetrics, int capacity,
        CanonicalWorld? canonical, RuntimeCostMetricProbe runtimeCosts)
    {
        ArgumentNullException.ThrowIfNull(requestedMetrics);
        if (requestedMetrics.Length == 0)
            throw new ArgumentException("At least one metric name must be requested.", nameof(requestedMetrics));
        var registry = new MetricRegistry();
        foreach (IMetricProbe probe in CreateProbes(engine, requestedMetrics, capacity, canonical, runtimeCosts))
            registry.Register(probe);
        return registry.CreatePlan(requestedMetrics);
    }

    internal static void WriteAvailable(string engine, TextWriter output)
    {
        output.WriteLine($"Available metrics for {engine} (names are case-sensitive):");
        foreach (IMetricProbe probe in CreateProbes(engine, [], 0, null, new RuntimeCostMetricProbe()))
        {
            string cost = probe switch
            {
                ExactNearestNeighborProbe => "O(N^2) exact analysis plus sorting; opt in for affordable populations",
                RuntimeCostMetricProbe => "nondeterministic; completed simulation Step only; exact replay unavailable",
                _ => "O(N); deterministic"
            };
            foreach (string channel in probe.Channels)
                output.WriteLine($"  {channel} — {cost}");
        }
        output.WriteLine("Default: " + string.Join(',', DefaultMetrics));
    }

    private static IMetricProbe[] CreateProbes(string engine, string[] requestedMetrics, int capacity,
        CanonicalWorld? canonical, RuntimeCostMetricProbe runtimeCosts)
    {
        if (engine is not ("legacy" or "canonical"))
            throw new ArgumentException("Metric engine must be legacy or canonical.", nameof(engine));
        var nearest = new ExactNearestNeighborProbe(0);
        // Metadata registration is cheap. Allocate the four capacity-sized exact-analysis
        // buffers only when at least one of that probe's channels is requested.
        if (requestedMetrics.Any(name => nearest.Channels.Contains(name, StringComparer.Ordinal)))
            nearest = new ExactNearestNeighborProbe(capacity);
        IMetricProbe[] common = [new MotionMetricProbe(), nearest, runtimeCosts];
        if (engine == "legacy") return common;
        IMetricProbe boids = canonical is null
            ? new BoidsRuleMetricProbe(new RuleInstrumentation(1))
            : new CompletedStepBoidsMetricProbe(canonical);
        return [.. common, boids];
    }
}

/// <summary>Initial state has no completed rule observation; later mismatches still fail.</summary>
internal sealed class CompletedStepBoidsMetricProbe : IMetricProbe
{
    private readonly CanonicalWorld _world;
    private readonly BoidsRuleMetricProbe _inner;

    internal CompletedStepBoidsMetricProbe(CanonicalWorld world)
    {
        _world = world;
        _inner = new BoidsRuleMetricProbe(world.Instrumentation);
    }

    public IReadOnlyList<string> Channels => _inner.Channels;
    public bool IsDeterministic => _inner.IsDeterministic;

    public void Sample(in MetricFrame frame, Span<double> output)
    {
        if (_world.TickCount == 0)
        {
            if (output.Length < Channels.Count)
                throw new ArgumentException("Output cannot hold the boids metric channels.", nameof(output));
            output.Slice(0, Channels.Count).Fill(double.NaN);
            return;
        }
        _inner.Sample(frame, output);
    }
}
