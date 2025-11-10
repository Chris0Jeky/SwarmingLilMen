using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using SwarmSim.Core;

namespace SwarmSim.Benchmarks;

/// <summary>
/// Benchmarks for full World.Tick() performance with varying agent counts.
/// Tests the complete simulation pipeline: grid rebuild, systems, integration.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[MarkdownExporter]
public class WorldTickBenchmarks
{
    private World _world1k = null!;
    private World _world10k = null!;
    private World _world50k = null!;
    private World _world100k = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Create worlds with different agent counts
        _world1k = CreateWorld(1_000);
        _world10k = CreateWorld(10_000);
        _world50k = CreateWorld(50_000);
        _world100k = CreateWorld(100_000);
    }

    private static World CreateWorld(int agentCount)
    {
        var config = new SimConfig
        {
            InitialCapacity = agentCount,
            SeparationWeight = 2.0f,
            AlignmentWeight = 1.5f,
            CohesionWeight = 1.5f,
            AttackDamage = 0f, // No combat for benchmarking
            BaseDrain = 0.1f // Low energy drain
        };

        var world = new World(config, seed: 42);

        // Spawn agents randomly
        for (int i = 0; i < agentCount; i++)
        {
            world.AddRandomAgent(group: (byte)(i % 4));
        }

        return world;
    }

    [Benchmark(Description = "Tick with 1,000 agents")]
    public void Tick_1k_Agents()
    {
        _world1k.Tick();
    }

    [Benchmark(Description = "Tick with 10,000 agents")]
    public void Tick_10k_Agents()
    {
        _world10k.Tick();
    }

    [Benchmark(Description = "Tick with 50,000 agents")]
    public void Tick_50k_Agents()
    {
        _world50k.Tick();
    }

    [Benchmark(Description = "Tick with 100,000 agents")]
    public void Tick_100k_Agents()
    {
        _world100k.Tick();
    }
}
