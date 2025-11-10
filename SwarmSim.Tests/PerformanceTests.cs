using System.Diagnostics;
using SwarmSim.Core;

namespace SwarmSim.Tests;

/// <summary>
/// Performance verification tests for Phase 1 goals.
/// These are not unit tests but performance gates to verify we hit our targets.
/// </summary>
public class PerformanceTests
{
    [Fact]
    public void Tick_With50kAgents_RunsAt60FPSOrBetter()
    {
        // Phase 1 goal: 50k agents @ 60 FPS (16.67ms per tick)
        const int agentCount = 50_000;
        const int warmupTicks = 10;
        const int testTicks = 100;
        const double targetTickTime = 16.67; // ms (60 FPS)

        var config = new SimConfig
        {
            InitialCapacity = agentCount,
            SeparationWeight = 2.0f,
            AlignmentWeight = 1.5f,
            CohesionWeight = 1.5f
        };

        var world = new World(config, seed: 42);

        // Spawn agents
        for (int i = 0; i < agentCount; i++)
        {
            world.AddRandomAgent(group: (byte)(i % 4));
        }

        // Warmup
        for (int i = 0; i < warmupTicks; i++)
        {
            world.Tick();
        }

        // Measure
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < testTicks; i++)
        {
            world.Tick();
        }
        sw.Stop();

        double avgTickTime = sw.Elapsed.TotalMilliseconds / testTicks;
        double fps = 1000.0 / avgTickTime;

        // Output for human readability
        Console.WriteLine($"50k agents: {avgTickTime:F2}ms per tick ({fps:F1} FPS)");
        Console.WriteLine($"Target: {targetTickTime:F2}ms per tick (60 FPS)");

        // Assert we meet performance goal (with 20% margin)
        Assert.True(avgTickTime < targetTickTime * 1.2,
            $"Performance below target: {avgTickTime:F2}ms > {targetTickTime * 1.2:F2}ms");
    }

    [Fact]
    public void Tick_With1kAgents_IsVeryFast()
    {
        // Baseline: 1k agents should be extremely fast
        const int agentCount = 1_000;
        const int testTicks = 1000;

        var config = new SimConfig { InitialCapacity = agentCount };
        var world = new World(config, seed: 42);

        for (int i = 0; i < agentCount; i++)
        {
            world.AddRandomAgent(group: (byte)(i % 4));
        }

        // Warmup
        for (int i = 0; i < 10; i++)
            world.Tick();

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < testTicks; i++)
        {
            world.Tick();
        }
        sw.Stop();

        double avgTickTime = sw.Elapsed.TotalMilliseconds / testTicks;
        double fps = 1000.0 / avgTickTime;

        Console.WriteLine($"1k agents: {avgTickTime:F3}ms per tick ({fps:F0} FPS)");

        // Should be under 1ms per tick
        Assert.True(avgTickTime < 1.0, $"1k agents too slow: {avgTickTime:F3}ms");
    }

    [Fact]
    public void Tick_With10kAgents_MeetsInteractiveGoal()
    {
        // 10k agents should easily hit 60 FPS
        const int agentCount = 10_000;
        const int testTicks = 100;
        const double targetTickTime = 16.67; // ms (60 FPS)

        var config = new SimConfig { InitialCapacity = agentCount };
        var world = new World(config, seed: 42);

        for (int i = 0; i < agentCount; i++)
        {
            world.AddRandomAgent(group: (byte)(i % 4));
        }

        // Warmup
        for (int i = 0; i < 10; i++)
            world.Tick();

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < testTicks; i++)
        {
            world.Tick();
        }
        sw.Stop();

        double avgTickTime = sw.Elapsed.TotalMilliseconds / testTicks;
        double fps = 1000.0 / avgTickTime;

        Console.WriteLine($"10k agents: {avgTickTime:F2}ms per tick ({fps:F1} FPS)");

        // Should comfortably beat 60 FPS
        Assert.True(avgTickTime < targetTickTime,
            $"10k agents below 60 FPS: {avgTickTime:F2}ms");
    }

    [Fact]
    public void GridRebuild_With50kAgents_IsEfficient()
    {
        // Grid rebuild should be O(n) and fast
        const int agentCount = 50_000;
        const int testRebuilds = 100;

        var config = new SimConfig { InitialCapacity = agentCount };
        var world = new World(config, seed: 42);

        for (int i = 0; i < agentCount; i++)
        {
            world.AddRandomAgent();
        }

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < testRebuilds; i++)
        {
            world.Grid.Rebuild(world.X, world.Y, world.Count);
        }
        sw.Stop();

        double avgRebuildTime = sw.Elapsed.TotalMilliseconds / testRebuilds;

        Console.WriteLine($"Grid rebuild (50k agents): {avgRebuildTime:F3}ms");

        // Should be under 2ms
        Assert.True(avgRebuildTime < 2.0,
            $"Grid rebuild too slow: {avgRebuildTime:F3}ms");
    }
}
