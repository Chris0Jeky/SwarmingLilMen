using System.Diagnostics;
using System.Text.Json;
using SwarmSim.Core;

namespace SwarmSim.Tests;

/// <summary>
/// Reported performance measurements with machine-relative scaling checks.
/// Absolute throughput targets are observations, not test gates.
/// </summary>
public class PerformanceTests
{
    private const int WarmupTicks = 10;
    private const int WarmupGridRebuilds = 10;

    [Fact]
    [Trait("Category", "Performance")]
    public void Tick_With50kAgents_StaysWithinScalingEnvelope()
    {
        const int referenceAgents = 1_000;
        const int scenarioAgents = 50_000;
        const int measuredTicks = 100;
        // Dense fixed-area neighbor searches degrade per agent at larger populations. This 40x
        // allowance remains a meaningful regression ceiling while leaving headroom over one
        // 2026-07-25 local sample of about 19.4x per-agent degradation.
        const double maxPerAgentDegradation = 40.0;
        const double reportedTargetMilliseconds = 16.67;

        // Match the heavier steering-weight preset used by WorldTickBenchmarks; the lower-scale
        // tests intentionally exercise SimConfig defaults.
        static SimConfig CreateConfig(int agentCount) => new()
        {
            InitialCapacity = agentCount,
            SeparationWeight = 2.0f,
            AlignmentWeight = 1.5f,
            CohesionWeight = 1.5f
        };

        double referenceMilliseconds = MeasureTicks(referenceAgents, measuredTicks, CreateConfig);
        double scenarioMilliseconds = MeasureTicks(scenarioAgents, measuredTicks, CreateConfig);
        double maximumScenarioMilliseconds = ReportResult(
            "legacy_world_tick",
            referenceAgents,
            referenceMilliseconds,
            scenarioAgents,
            scenarioMilliseconds,
            maxPerAgentDegradation,
            reportedTargetMilliseconds);

        Assert.True(
            scenarioMilliseconds <= maximumScenarioMilliseconds,
            $"50k tick scaling regressed: {scenarioMilliseconds:F3}ms exceeds the same-run " +
            $"machine-relative ceiling of {maximumScenarioMilliseconds:F3}ms.");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void Tick_With1kAgents_StaysWithinScalingEnvelope()
    {
        const int referenceAgents = 500;
        const int scenarioAgents = 1_000;
        const int measuredTicks = 1_000;
        // The 4x allowance leaves headroom over one 2026-07-25 local sample of about 1.7x
        // per-agent degradation while still detecting a large scaling regression.
        const double maxPerAgentDegradation = 4.0;
        const double reportedTargetMilliseconds = 1.0;

        static SimConfig CreateConfig(int agentCount) => new() { InitialCapacity = agentCount };

        double referenceMilliseconds = MeasureTicks(referenceAgents, measuredTicks, CreateConfig);
        double scenarioMilliseconds = MeasureTicks(scenarioAgents, measuredTicks, CreateConfig);
        double maximumScenarioMilliseconds = ReportResult(
            "legacy_world_tick",
            referenceAgents,
            referenceMilliseconds,
            scenarioAgents,
            scenarioMilliseconds,
            maxPerAgentDegradation,
            reportedTargetMilliseconds);

        Assert.True(
            scenarioMilliseconds <= maximumScenarioMilliseconds,
            $"1k tick scaling regressed: {scenarioMilliseconds:F3}ms exceeds the same-run " +
            $"machine-relative ceiling of {maximumScenarioMilliseconds:F3}ms.");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void Tick_With10kAgents_StaysWithinScalingEnvelope()
    {
        const int referenceAgents = 1_000;
        const int scenarioAgents = 10_000;
        const int measuredTicks = 100;
        // The 12x allowance leaves headroom over one 2026-07-25 local sample of about 5.4x
        // per-agent degradation while still detecting a large scaling regression.
        const double maxPerAgentDegradation = 12.0;
        const double reportedTargetMilliseconds = 16.67;

        static SimConfig CreateConfig(int agentCount) => new() { InitialCapacity = agentCount };

        double referenceMilliseconds = MeasureTicks(referenceAgents, measuredTicks, CreateConfig);
        double scenarioMilliseconds = MeasureTicks(scenarioAgents, measuredTicks, CreateConfig);
        double maximumScenarioMilliseconds = ReportResult(
            "legacy_world_tick",
            referenceAgents,
            referenceMilliseconds,
            scenarioAgents,
            scenarioMilliseconds,
            maxPerAgentDegradation,
            reportedTargetMilliseconds);

        Assert.True(
            scenarioMilliseconds <= maximumScenarioMilliseconds,
            $"10k tick scaling regressed: {scenarioMilliseconds:F3}ms exceeds the same-run " +
            $"machine-relative ceiling of {maximumScenarioMilliseconds:F3}ms.");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void GridRebuild_With50kAgents_StaysWithinScalingEnvelope()
    {
        const int referenceAgents = 5_000;
        const int scenarioAgents = 50_000;
        const int measuredRebuilds = 100;
        // The 2.5x allowance leaves headroom over one 2026-07-25 local sample of about 1.1x
        // per-agent degradation while still detecting a large scaling regression.
        const double maxPerAgentDegradation = 2.5;
        const double reportedTargetMilliseconds = 2.0;

        double referenceMilliseconds = MeasureGridRebuilds(referenceAgents, measuredRebuilds);
        double scenarioMilliseconds = MeasureGridRebuilds(scenarioAgents, measuredRebuilds);
        double maximumScenarioMilliseconds = ReportResult(
            "uniform_grid_rebuild",
            referenceAgents,
            referenceMilliseconds,
            scenarioAgents,
            scenarioMilliseconds,
            maxPerAgentDegradation,
            reportedTargetMilliseconds);

        Assert.True(
            scenarioMilliseconds <= maximumScenarioMilliseconds,
            $"50k grid-rebuild scaling regressed: {scenarioMilliseconds:F3}ms exceeds the same-run " +
            $"machine-relative ceiling of {maximumScenarioMilliseconds:F3}ms.");
    }

    private static double MeasureTicks(
        int agentCount,
        int measuredTicks,
        Func<int, SimConfig> createConfig)
    {
        var world = new World(createConfig(agentCount), seed: 42);
        for (int i = 0; i < agentCount; i++)
        {
            world.AddRandomAgent(group: (byte)(i % 4));
        }

        for (int i = 0; i < WarmupTicks; i++)
        {
            world.Tick();
        }

        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < measuredTicks; i++)
        {
            world.Tick();
        }

        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds / measuredTicks;
    }

    private static double MeasureGridRebuilds(int agentCount, int measuredRebuilds)
    {
        var world = new World(new SimConfig { InitialCapacity = agentCount }, seed: 42);
        for (int i = 0; i < agentCount; i++)
        {
            world.AddRandomAgent();
        }

        for (int i = 0; i < WarmupGridRebuilds; i++)
        {
            world.Grid.Rebuild(world.X, world.Y, world.Count);
        }

        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < measuredRebuilds; i++)
        {
            world.Grid.Rebuild(world.X, world.Y, world.Count);
        }

        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds / measuredRebuilds;
    }

    private static double ReportResult(
        string measurement,
        int referenceAgents,
        double referenceMilliseconds,
        int scenarioAgents,
        double scenarioMilliseconds,
        double maxPerAgentDegradation,
        double reportedTargetMilliseconds)
    {
        double agentScale = (double)scenarioAgents / referenceAgents;
        double maximumTimeScale = agentScale * maxPerAgentDegradation;
        double maximumScenarioMilliseconds = referenceMilliseconds * maximumTimeScale;

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Schema = "swarm-performance/v1",
            Measurement = measurement,
            ReferenceAgents = referenceAgents,
            ReferenceMillisecondsPerOperation = referenceMilliseconds,
            ScenarioAgents = scenarioAgents,
            ScenarioMillisecondsPerOperation = scenarioMilliseconds,
            ScenarioOperationsPerSecond = 1000.0 / scenarioMilliseconds,
            AgentScale = agentScale,
            ObservedTimeScale = scenarioMilliseconds / referenceMilliseconds,
            ObservedPerAgentDegradation =
                (scenarioMilliseconds / referenceMilliseconds) / agentScale,
            MaximumTimeScale = maximumTimeScale,
            MaximumPerAgentDegradation = maxPerAgentDegradation,
            ReportedTargetMillisecondsPerOperation = reportedTargetMilliseconds,
            ReportedTargetMet = scenarioMilliseconds <= reportedTargetMilliseconds
        }));

        return maximumScenarioMilliseconds;
    }
}
