using System.Collections.Generic;
using System.Linq;
using SwarmSim.Core;

namespace SwarmSim.Tests;

public class SimulationRunnerTests
{
    private static SimConfig CreateBasicConfig() => new()
    {
        InitialCapacity = 8,
        FixedDeltaTime = 0.125f, // Use power-of-2 fraction for exact binary representation
        SenseRadius = 10f,
        SeparationWeight = 0f,
        AlignmentWeight = 0f,
        CohesionWeight = 0f,
        WanderStrength = 0f // Keep deterministic
    };

    [Fact]
    public void Advance_ProcessesStep_WhenAccumulatorExceedsDt()
    {
        var world = new World(CreateBasicConfig(), seed: 1);
        var runner = new SimulationRunner(world);

        // First half step → no tick (0.0625 < 0.125)
        int steps = runner.Advance(0.0625);
        Assert.Equal(0, steps);
        Assert.Equal((ulong)0, world.TickCount);

        // Second half step → one tick processed (0.0625 + 0.0625 = 0.125)
        steps = runner.Advance(0.0625);
        Assert.Equal(1, steps);
        Assert.Equal((ulong)1, world.TickCount);
    }

    [Fact]
    public void Advance_Respects_MaxStepsPerAdvance()
    {
        var config = new SimConfig
        {
            InitialCapacity = 8,
            FixedDeltaTime = 0.0625f, // Use power-of-2 fraction (1/16)
            SenseRadius = 10f,
            SeparationWeight = 0f,
            AlignmentWeight = 0f,
            CohesionWeight = 0f,
            WanderStrength = 0f
        };

        var world = new World(config, seed: 1);
        var runner = new SimulationRunner(world, maxStepsPerAdvance: 2);

        // Needs 4 steps worth of time (0.25 / 0.0625 = 4), but cap should limit to 2
        int steps = runner.Advance(0.25);

        Assert.Equal(2, steps);
        Assert.Equal((ulong)2, world.TickCount);
        Assert.True(runner.Accumulator > 0); // remaining work saved for later
    }

    [Fact]
    public void Step_ReturnsSnapshot_WithLatestTick()
    {
        var world = new World(CreateBasicConfig(), seed: 1);
        world.AddAgent(10, 10);

        var runner = new SimulationRunner(world);
        var snapshot = runner.Step();

        Assert.Equal(world.TickCount, snapshot.TickCount);
        Assert.Equal(world.Count, snapshot.AgentCount);
        Assert.True(snapshot.CaptureVersion > 0);
    }

    [Fact]
    public void CaptureSnapshot_ProvidesMonotonicVersions()
    {
        var world = new World(CreateBasicConfig(), seed: 1);
        var runner = new SimulationRunner(world);

        var s1 = runner.CaptureSnapshot();
        var s2 = runner.CaptureSnapshot();

        Assert.True(s2.CaptureVersion > s1.CaptureVersion);
    }

    [Fact]
    public void NotifyWorldMutated_BumpsMutationVersion_AndResetsAccumulator()
    {
        var world = new World(CreateBasicConfig(), seed: 1);
        var runner = new SimulationRunner(world);

        runner.Advance(0.0625); // accumulate some time
        Assert.True(runner.Accumulator > 0);

        runner.NotifyWorldMutated();
        Assert.Equal(0, runner.Accumulator);

        var snapshot = runner.CaptureSnapshot();
        Assert.Equal(runner.MutationVersion, snapshot.MutationVersion);
    }

    [Fact]
    public void SnapshotCallback_ReceivesImmutableCopies()
    {
        var world = new World(CreateBasicConfig(), seed: 1);
        int agent = world.AddAgent(0, 0);
        world.Vx[agent] = 1f;

        var snapshots = new List<SimSnapshot>();
        var runner = new SimulationRunner(world, snapshots.Add);

        // Advance enough time for two ticks
        runner.Advance(0.25);

        Assert.Equal(2, snapshots.Count);
        Assert.Equal((ulong)2, snapshots.Last().TickCount);

        // Modify world after capture → snapshots remain unchanged
        world.X[agent] = 999f;
        Assert.All(snapshots, snap => Assert.NotEqual(999f, snap.PositionsX[0]));
    }
}
