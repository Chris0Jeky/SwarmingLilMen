using SwarmSim.Parity;
using SwarmSim.Core.Metrics;
using System.Security.Cryptography;

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

    [Theory]
    [InlineData(1)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(129)]
    public void StateIdentitiesHashTheCompleteLittleEndianAgentAndTickStream(int count)
    {
        var recipe = new ScenarioRecipe { Id = "HASH_CONTRACT", Layout = "uniform", Count = count, Ticks = 3 };
        KinematicSample[] requested = recipe.Generate(7);
        var reference = new ReynoldsReference(recipe, requested);
        KinematicSample[] initial = reference.Agents.ToArray();
        using var trace = new MemoryStream();
        using var writer = new BinaryWriter(trace);
        WriteSnapshot(writer, initial);
        for (int tick = 0; tick < recipe.Ticks; tick++)
        {
            reference.Step();
            WriteSnapshot(writer, reference.Agents);
        }
        ParityRunResult result = ParityRunner.Run(recipe, 7, "reference");
        var identities = ParityRunner.InitialIdentities(recipe, 7, "reference");
        Assert.Equal(SnapshotHash(requested), result.RequestedInitialHash);
        Assert.Equal(SnapshotHash(initial), result.InitialHash);
        Assert.Equal(SnapshotHash(reference.Agents), result.FinalHash);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(trace.ToArray())), result.StateTraceHash);
        Assert.Equal((result.RequestedInitialHash, result.InitialHash), identities);
    }

    private static string SnapshotHash(ReadOnlySpan<KinematicSample> agents)
    {
        using var bytes = new MemoryStream();
        using var writer = new BinaryWriter(bytes);
        WriteSnapshot(writer, agents);
        return Convert.ToHexString(SHA256.HashData(bytes.ToArray()));
    }

    private static void WriteSnapshot(BinaryWriter writer, ReadOnlySpan<KinematicSample> agents)
    {
        // BinaryWriter writes individual float words little-endian. One-shot hashing of this
        // independent encoding checks component order, partial batches and tick boundaries.
        foreach (KinematicSample a in agents)
        {
            writer.Write(a.X); writer.Write(a.Y); writer.Write(a.Vx); writer.Write(a.Vy);
        }
    }
}
