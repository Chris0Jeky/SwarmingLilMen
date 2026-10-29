using SwarmSim.Parity;

namespace SwarmSim.Tests;

public sealed class ParityRunnerTests
{
    [Theory]
    [InlineData("enhanced")]
    [InlineData("classic")]
    [InlineData("legacy")]
    [InlineData("reference")]
    public void FixedRecipeProducesRepeatableCompleteMetricArtifacts(string model)
    {
        var recipe = new ScenarioRecipe { Ticks = 8 };
        ParityRunResult first = ParityRunner.Run(recipe, 7, model);
        ParityRunResult second = ParityRunner.Run(recipe, 7, model);
        Assert.Equal(first.InitialHash, second.InitialHash);
        Assert.Equal(first.FinalHash, second.FinalHash);
        Assert.Equal(first.CsvHash, second.CsvHash);
        Assert.NotEqual(first.InitialHash, first.FinalHash);
        Assert.All(first.WindowMeans, x => Assert.True(x.HasValue && double.IsFinite(x.Value)));
        Assert.All(first.WindowFiniteCounts, x => Assert.Equal(2, x));
    }

    [Theory]
    [InlineData("enhanced")]
    [InlineData("classic")]
    public void SpatialIndexChoicePreservesStateAndMetricBytes(string model)
    {
        var recipe = new ScenarioRecipe { Id = "S4_UNIFORM_RANDOM_512", Layout = "uniform", Count = 32, Ticks = 16 };
        ParityRunResult grid = ParityRunner.Run(recipe, 7, model);
        ParityRunResult naive = ParityRunner.Run(recipe, 7, model, naiveIndex: true);
        Assert.Equal(grid.InitialHash, naive.InitialHash);
        Assert.Equal(grid.FinalHash, naive.FinalHash);
        Assert.Equal(grid.CsvHash, naive.CsvHash);
    }

    [Fact]
    public void NamedAlignmentDisablingChangesClassicTrajectory()
    {
        var recipe = new ScenarioRecipe { Id = "S4_UNIFORM_RANDOM_512", Layout = "uniform", Count = 32, Ticks = 16,
            Width = 80, Height = 80 };
        ParityRunResult normal = ParityRunner.Run(recipe, 7, "classic");
        ParityRunResult disabled = ParityRunner.Run(recipe, 7, "classic", alignmentDisabled: true);
        Assert.Equal(normal.InitialHash, disabled.InitialHash);
        Assert.NotEqual(normal.FinalHash, disabled.FinalHash);
        Assert.NotEqual(normal.CsvHash, disabled.CsvHash);
    }
}
