using SwarmSim.Core;

namespace SwarmSim.Tests;

public sealed class SimulationRunnerInputTests
{
    private static SimConfig CreateBasicConfig() => new()
    {
        Seed = 1u,
        InitialCapacity = 8,
        FixedDeltaTime = 0.125f,
        SenseRadius = 10f,
        SeparationWeight = 0f,
        AlignmentWeight = 0f,
        CohesionWeight = 0f,
        WanderStrength = 0f
    };

    private static SimulationRunner CreateRunner(out World world)
    {
        world = new World(CreateBasicConfig(), seed: 1);
        return new SimulationRunner(world);
    }

    [Fact]
    [Trait("Category", "InputValidationRunner")]
    public void Advance_NaN_Throws()
    {
        var runner = CreateRunner(out _);
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.Advance(double.NaN));
    }

    [Fact]
    [Trait("Category", "InputValidationRunner")]
    public void Advance_PositiveInfinity_Throws()
    {
        var runner = CreateRunner(out _);
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.Advance(double.PositiveInfinity));
    }

    [Fact]
    [Trait("Category", "InputValidationRunner")]
    public void Advance_NegativeInfinity_Throws()
    {
        var runner = CreateRunner(out _);
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.Advance(double.NegativeInfinity));
    }

    [Fact]
    [Trait("Category", "InputValidationRunner")]
    public void Advance_Negative_Throws()
    {
        var runner = CreateRunner(out _);
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.Advance(-0.1));
    }

    [Fact]
    [Trait("Category", "InputValidationRunner")]
    public void Advance_AfterRejectedNaN_StillSteps()
    {
        var runner = CreateRunner(out World world);
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.Advance(double.NaN));

        int steps = runner.Advance(0.375);

        Assert.True(steps > 0);
        Assert.True(world.TickCount > 0);
        Assert.True(double.IsFinite(runner.Accumulator));
    }

    [Fact]
    [Trait("Category", "InputValidationRunner")]
    public void Advance_Zero_ReturnsZero()
    {
        var runner = CreateRunner(out _);
        int steps = runner.Advance(0);
        Assert.Equal(0, steps);
    }
}
