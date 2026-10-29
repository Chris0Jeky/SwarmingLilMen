using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using SwarmSim.Benchmarks;

BenchmarkSwitcher.FromTypes([typeof(CompositionCostBenchmarks)]).Run(args);

// Forward the production benchmark's exact setup and complete Step. This wrapper has no
// SimpleJob attribute, so CLI job settings do not accidentally create a second full matrix.
[MemoryDiagnoser]
public class CompositionCostBenchmarks
{
    private readonly CanonicalWorldStepBenchmarks _scenario = new();

    [Params(1_000, 10_000, 50_000)]
    public int AgentCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _scenario.AgentCount = AgentCount;
        _scenario.Setup();
    }

    [Benchmark(Description = "Existing canonical Step, telemetry capture off")]
    public void Step() => _scenario.Step();
}
