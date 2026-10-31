using System.Diagnostics;
using SwarmSim.Core.Metrics;

namespace SwarmSim.Cli;

/// <summary>Reusable state capture, metric plan and outputs for one prepared simulation.</summary>
internal sealed class ExperimentMetrics
{
    private readonly PreparedSimulation _simulation;
    private readonly KinematicSample[] _agents;
    private readonly double[] _values;
    private int _count;
    private readonly RuntimeCostMetricProbe _runtimeCosts = new();

    internal ExperimentMetrics(RunRequest request, PreparedSimulation simulation)
    {
        _simulation = simulation;
        Plan = MetricCatalog.CreatePlan(request.Engine, request.RequestedMetrics, simulation.MetricCapacity,
            simulation.Simulation, _runtimeCosts);
        _agents = new KinematicSample[simulation.MetricCapacity];
        _values = new double[Plan.ChannelCount];
        Sample(); // Validate initial finite state and the selected probes before output creation.
    }

    internal MetricPlan Plan { get; }
    internal ReadOnlySpan<double> Values => _values;
    /// <summary>The agents captured by the latest Sample(); borrowed, valid until the next sample.</summary>
    internal ReadOnlySpan<KinematicSample> Agents => _agents.AsSpan(0, _count);

    internal void Sample()
    {
        _count = _simulation.Capture(_agents);
        var frame = new MetricFrame(_agents.AsSpan(0, _count), _simulation.Domain);
        Plan.Sample(frame, _values);
    }

    internal void StepAndSample()
    {
        if (Plan.IsDeterministic)
            _simulation.Step();
        else
        {
            // Capture and output are deliberately outside this interval. Both allocation
            // counters are read on this synchronous simulation thread.
            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            long beforeTime = Stopwatch.GetTimestamp();
            _simulation.Step();
            long afterTime = Stopwatch.GetTimestamp();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
            _runtimeCosts.Record((afterTime - beforeTime) * 1000d / Stopwatch.Frequency, allocated);
        }
        Sample();
    }
}
