using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using SwarmSim.Core;
using SwarmSim.Core.Spatial;
using SwarmSim.Core.Utils;

namespace SwarmSim.Benchmarks;

/// <summary>
/// Benchmarks for UniformGrid operations: Rebuild and Query performance.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[MarkdownExporter]
public class GridBenchmarks
{
    private UniformGrid _grid = null!;
    private float[] _x = null!;
    private float[] _y = null!;

    [Params(1_000, 10_000, 50_000, 100_000)]
    public int AgentCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var config = SimConfig.PeacefulFlocks();
        _grid = new UniformGrid(
            cellSize: config.SenseRadius,
            worldWidth: config.WorldWidth,
            worldHeight: config.WorldHeight,
            capacity: AgentCount);

        _x = new float[AgentCount];
        _y = new float[AgentCount];

        var rng = new Rng(42);
        for (int i = 0; i < AgentCount; i++)
        {
            _x[i] = rng.NextFloat(0f, config.WorldWidth);
            _y[i] = rng.NextFloat(0f, config.WorldHeight);
        }

        // Warm up
        _grid.Rebuild(_x, _y, AgentCount);
    }

    [Benchmark(Description = "Rebuild grid")]
    public void RebuildGrid()
    {
        _grid.Rebuild(_x, _y, AgentCount);
    }

    [Benchmark(Description = "Query 3x3 (callback)")]
    public void Query3x3_Callback()
    {
        int count = 0;
        _grid.Query3x3(500f, 500f, idx => count++);
    }

    [Benchmark(Description = "Query 3x3 (buffer)")]
    public int Query3x3_Buffer()
    {
        Span<int> buffer = stackalloc int[128];
        return _grid.Query3x3(500f, 500f, buffer, maxResults: 128);
    }
}
