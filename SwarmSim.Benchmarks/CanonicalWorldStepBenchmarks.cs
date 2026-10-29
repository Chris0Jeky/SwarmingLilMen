using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Utils;

namespace SwarmSim.Benchmarks;

/// <summary>
/// Complete canonical ticks, including the grid rebuild, steering and integration, with
/// telemetry capture off. Each case starts from the same seed and evolves through warmup and
/// measurement; the world extents and canonical settings stay fixed as population increases.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[MarkdownExporter]
public class CanonicalWorldStepBenchmarks
{
    private CanonicalWorld _world = null!;

    [Params(1_000, 10_000, 50_000)]
    public int AgentCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = AgentCount,
            Seed = 123456,
            WorldWidth = 1920,
            WorldHeight = 1080,
            TargetSpeed = 1,
            MaxForce = 0.2f,
            SenseRadius = 10,
            SeparationRadius = 5,
            FieldOfView = 270,
            MaxNeighbors = 32,
            FixedDeltaTime = 1f / 60f
        };
        _world = new CanonicalWorld(settings, new GridSpatialIndex(
            settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        var rng = new Rng(settings.Seed);
        for (int i = 0; i < AgentCount; i++)
        {
            var position = new Vec2(rng.NextFloat(0, settings.WorldWidth), rng.NextFloat(0, settings.WorldHeight));
            float angle = rng.NextFloat(0, 2f * MathF.PI);
            var direction = new Vec2(MathF.Cos(angle), MathF.Sin(angle));
            if (!_world.TryAddBoid(position, direction, (byte)(i % 4)))
                throw new InvalidOperationException("Could not initialize the canonical benchmark population.");
        }
    }

    [Benchmark(Description = "Canonical Step (telemetry capture off)")]
    public void Step() => _world.Step(_world.Settings.FixedDeltaTime);
}
