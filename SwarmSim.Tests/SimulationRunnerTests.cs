using System.Collections.Generic;
using System.Linq;
using SwarmSim.Core;

namespace SwarmSim.Tests;

public class SimulationRunnerTests
{
    private static SimConfig CreateBasicConfig() => new()
    {
        InitialCapacity = 8,
        FixedDeltaTime = 0.1f,
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

        // First half step → no tick
        int steps = runner.Advance(0.05);
        Assert.Equal(0, steps);
        Assert.Equal((ulong)0, world.TickCount);

        // Second half step → one tick processed
        steps = runner.Advance(0.05);
        Assert.Equal(1, steps);
        Assert.Equal((ulong)1, world.TickCount);
    }

    [Fact]
    public void Advance_Respects_MaxStepsPerAdvance()
    {
        var config = new SimConfig
        {
            InitialCapacity = 8,
            FixedDeltaTime = 0.01f, // Smaller dt to need more steps
            SenseRadius = 10f,
            SeparationWeight = 0f,
            AlignmentWeight = 0f,
            CohesionWeight = 0f,
            WanderStrength = 0f
        };

        var world = new World(config, seed: 1);
        var runner = new SimulationRunner(world, maxStepsPerAdvance: 2);

        // Needs 5 steps worth of time, but cap should limit to 2
        int steps = runner.Advance(0.05);

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
