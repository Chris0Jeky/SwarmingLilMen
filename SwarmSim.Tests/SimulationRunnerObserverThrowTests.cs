using SwarmSim.Core;

namespace SwarmSim.Tests;

/// <summary>
/// A throwing snapshot observer must not leave the completed tick's time in the accumulator,
/// otherwise the next Advance(0) replays a tick that already ran (#131).
/// </summary>
public class SimulationRunnerObserverThrowTests
{
    private static SimConfig Config() => new()
    {
        Seed = 1u,
        InitialCapacity = 8,
        FixedDeltaTime = 0.125f, // exact in binary
        SenseRadius = 10f,
        SeparationWeight = 0f,
        AlignmentWeight = 0f,
        CohesionWeight = 0f,
        WanderStrength = 0f
    };

    [Fact]
    public void Advance_ThrowingObserver_ConsumesExecutedTickTime()
    {
        var world = new World(Config(), seed: 1);
        int calls = 0;
        var runner = new SimulationRunner(world, _ =>
        {
            if (++calls == 1) throw new InvalidOperationException("observer");
        });

        Assert.Throws<InvalidOperationException>(() => runner.Advance(0.125));

        Assert.Equal((ulong)1, world.TickCount);
        Assert.Equal(0.0, runner.Accumulator, 9);

        Assert.Equal(0, runner.Advance(0));
        Assert.Equal((ulong)1, world.TickCount);
        Assert.Equal(1, calls);

        // The throwing call consumed snapshot version 1; the next capture is version 2.
        Assert.Equal(2, runner.CaptureSnapshot().CaptureVersion);
    }

    [Fact]
    public void Advance_ThrowingObserver_KeepsRemainingTime()
    {
        var world = new World(Config(), seed: 1);
        int calls = 0;
        var runner = new SimulationRunner(world, _ =>
        {
            if (++calls == 1) throw new InvalidOperationException("observer");
        });

        Assert.Throws<InvalidOperationException>(() => runner.Advance(0.25)); // two ticks' worth

        Assert.Equal((ulong)1, world.TickCount);
        Assert.Equal(0.125, runner.Accumulator, 9);
        Assert.Equal(1, runner.Advance(0));
        Assert.Equal((ulong)2, world.TickCount);
    }

    [Fact]
    public void Step_ThrowingObserver_ConsumesExecutedTickTime()
    {
        var world = new World(Config(), seed: 1);
        int calls = 0;
        var runner = new SimulationRunner(world, _ =>
        {
            if (++calls == 1) throw new InvalidOperationException("observer");
        });
        runner.Advance(0.0625);

        Assert.Throws<InvalidOperationException>(() => runner.Step());

        Assert.Equal((ulong)1, world.TickCount);
        Assert.Equal(0.0, runner.Accumulator, 9); // clamped at zero, as for a successful Step
        Assert.Equal(0, runner.Advance(0));
        Assert.Equal((ulong)1, world.TickCount);
    }
}
