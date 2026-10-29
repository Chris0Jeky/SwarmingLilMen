using System.Globalization;
using System.Text.Json.Nodes;
using SwarmSim.Cli;
using static SwarmSim.Tests.SweepCliTests;

namespace SwarmSim.Tests;

public sealed class SweepBehaviorTests
{
    [Fact]
    public void PropertyOrderAndCasingDoNotChangeCellOrderOrSummaryBytes()
    {
        using var files = new SweepFiles();
        JsonObject definition = files.Definition();
        definition["seeds"] = new JsonArray(7, 3);
        definition["grid"] = new JsonObject
        {
            ["separationweight"] = new JsonArray(2, 0),
            ["SenseRadius"] = new JsonArray(10, 12)
        };
        string path = files.WriteDefinition(definition);
        SweepPlan first = SweepPlan.Load(path);
        Assert.Equal(new[] { "SenseRadius", "SeparationWeight" }, first.Parameters);
        Assert.Equal(new[] { "10/2", "10/0", "12/2", "12/0", "10/2", "10/0", "12/2", "12/0" },
            first.Cells.Select(cell => string.Join('/', cell.ParameterValues)));
        Assert.Equal(new uint[] { 7, 7, 7, 7, 3, 3, 3, 3 }, first.Cells.Select(cell => cell.Request.Seed));
        string original = SweepRunner.Run(first, files.Output);

        definition["grid"] = new JsonObject
        {
            ["senseradius"] = new JsonArray(10, 12),
            ["SeparationWeight"] = new JsonArray(2, 0)
        };
        SweepPlan reordered = SweepPlan.Load(files.WriteDefinition(definition));
        string repeated = SweepRunner.Run(reordered, files.Output);
        Assert.Equal(File.ReadAllBytes(Path.Combine(original, "summary.csv")),
            File.ReadAllBytes(Path.Combine(repeated, "summary.csv")));

        // The summary writer remains stable if a caller supplies completed cells out of order.
        SweepCellSummary[] results = first.Cells.Select(cell => SweepSummary.Read(cell, first.Metrics,
            Path.Combine(original, cell.Id, "metrics.csv"))).Reverse().ToArray();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        SweepSummary.Write(output, first, results);
        Assert.Equal(File.ReadAllText(Path.Combine(original, "summary.csv")), output.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void UndefinedValuesHaveBlankAggregatesAndDefinedSampleCounts(int ticks)
    {
        using var files = new SweepFiles();
        JsonObject definition = files.Definition();
        definition.Remove("engine"); // Canonical is the sweep default as well as the single-run default.
        definition["ticks"] = ticks;
        definition["agents"] = 1;
        definition["seeds"] = new JsonArray(7);
        definition["grid"] = new JsonObject();
        definition["metrics"] = new JsonArray("boids.truncatedQueryFraction", "nearestNeighborMean", "population");

        var result = Run("--sweep", files.WriteDefinition(definition), "--out", files.Output);

        Assert.True(result.Code == 0, result.Error);
        string[] row = File.ReadAllLines(Path.Combine(result.Output.Trim(), "summary.csv"))[1].Split(',');
        Assert.Equal("canonical", row[1]);
        Assert.Equal(ticks == 0 ? "" : "0", row[5]); // Boids tick zero has no completed observation.
        Assert.Equal(ticks == 0 ? "" : "0", row[6]);
        Assert.Equal(ticks.ToString(CultureInfo.InvariantCulture), row[7]);
        Assert.Equal("", row[8]); // A singleton never has a nearest-neighbor distance.
        Assert.Equal("", row[9]);
        Assert.Equal("0", row[10]);
        Assert.Equal("1", row[11]);
        Assert.Equal("1", row[12]);
        Assert.Equal((ticks + 1).ToString(CultureInfo.InvariantCulture), row[13]);
    }

    [Fact]
    public void FailureAfterACompletedCellRemovesOnlyTheCurrentAttempt()
    {
        using var files = new SweepFiles();
        SweepPlan plan = SweepPlan.Load(files.WriteSweep());
        string prior = Path.Combine(files.Output, "sweep-prior");
        Directory.CreateDirectory(prior);
        string sentinel = Path.Combine(prior, "keep.txt");
        File.WriteAllText(sentinel, "prior experiment");
        int calls = 0;

        IOException failure = Assert.Throws<IOException>(() => SweepRunner.Run(plan, files.Output,
            (request, directory) =>
            {
                if (++calls == 2) throw new IOException("Injected cell failure");
                return ExperimentRunner.Run(request, directory);
            }));

        Assert.Equal("Injected cell failure", failure.Message);
        Assert.Equal(2, calls);
        Assert.Equal(new[] { prior }, Directory.GetFileSystemEntries(files.Output));
        Assert.Equal("prior experiment", File.ReadAllText(sentinel));
    }

    [Theory]
    [InlineData("tick,meanSpeed,polarization\n0,3,0\n", "all requested")]
    [InlineData("tick,meanSpeed,polarization\n1,3,0\n", "invalid metric row")]
    [InlineData("tick,meanSpeed,polarization\n0,Infinity,0\n", "invalid value")]
    [InlineData("tick,population\n0,12\n", "schema")]
    public void InvalidCellCsvCannotBecomeASuccessfulSummary(string csv, string message)
    {
        using var files = new SweepFiles();
        SweepPlan plan = SweepPlan.Load(files.WriteSweep());

        InvalidDataException failure = Assert.Throws<InvalidDataException>(() => SweepRunner.Run(plan, files.Output,
            (request, directory) =>
            {
                string run = ExperimentRunner.Run(request, directory);
                File.WriteAllText(Path.Combine(run, "metrics.csv"), csv);
                return run;
            }));

        Assert.Contains(message, failure.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(files.Output));
    }

    [Fact]
    public void ANewInvalidPlanDoesNotTouchAnExistingSweep()
    {
        using var files = new SweepFiles();
        Directory.CreateDirectory(files.Output);
        string prior = Path.Combine(files.Output, "prior.txt");
        File.WriteAllText(prior, "keep");
        string sweep = files.WriteSweep(new JsonObject
        {
            ["grid"] = new JsonObject { ["SenseRadius"] = new JsonArray(12, 0) }
        });

        var result = Run("--sweep", sweep, "--out", files.Output);

        Assert.Equal(1, result.Code);
        Assert.Equal(new[] { prior }, Directory.GetFileSystemEntries(files.Output));
        Assert.Equal("keep", File.ReadAllText(prior));
    }
}
