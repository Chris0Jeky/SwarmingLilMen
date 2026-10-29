using System.Globalization;
using System.Text.Json.Nodes;
using CliProgram = SwarmSim.Cli.Program;

namespace SwarmSim.Tests;

public sealed class HeadlessMetricSelectionTests
{
    [Theory]
    [InlineData("canonical")]
    [InlineData("legacy")]
    public void SelectedDeterministicMetrics_ReplayEveryColumnAndKeepRequestedOrder(string engine)
    {
        using var files = new TestFiles();
        const string selected = "nearestNeighborP95,centroidY,population,nearestNeighborMean,polarization,centroidX";
        string output = Path.Combine(files.Root, "original");
        var result = Run("--engine", engine, "--seed", "17", "--ticks", "3", "--agents", "8",
            "--metrics", selected, "--out", output);
        Assert.True(result.Code == 0, result.Error);
        string original = Assert.Single(Directory.GetDirectories(output));
        byte[] expected = File.ReadAllBytes(Path.Combine(original, "metrics.csv"));
        string[] lines = File.ReadAllLines(Path.Combine(original, "metrics.csv"));
        Assert.Equal("tick," + selected, lines[0]);
        Assert.Equal(5, lines.Length);
        Assert.All(lines.Skip(1), line => Assert.Equal(7, line.Split(',').Length));
        JsonObject manifest = ReadManifest(original);
        Assert.Equal(2, manifest["schemaVersion"]!.GetValue<int>());
        Assert.Equal(1, manifest["metricContractVersion"]!.GetValue<int>());
        Assert.True(manifest["metricsDeterministic"]!.GetValue<bool>());
        Assert.Equal(selected.Split(','), manifest["requestedMetrics"]!.AsArray().Select(value => value!.GetValue<string>()));

        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            string replayOutput = Path.Combine(files.Root, "replay");
            result = Run("--manifest", Path.Combine(original, "manifest.json"), "--out", replayOutput);
            Assert.True(result.Code == 0, result.Error);
            string replay = Assert.Single(Directory.GetDirectories(replayOutput));
            Assert.Equal(expected, File.ReadAllBytes(Path.Combine(replay, "metrics.csv")));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void CanonicalRuleChannels_AreMissingBeforeTheFirstCompletedStep()
    {
        using var files = new TestFiles();
        string output = Path.Combine(files.Root, "runs");
        var result = Run("--ticks", "2", "--agents", "4", "--metrics",
            "boids.separationMagnitudeMean,boids.steeringMagnitudeMean,boids.truncatedQueryFraction", "--out", output);
        Assert.True(result.Code == 0, result.Error);
        string original = Assert.Single(Directory.GetDirectories(output));
        string[] lines = File.ReadAllLines(Path.Combine(original, "metrics.csv"));
        Assert.Equal("0,,,", lines[1]);
        Assert.All(lines.Skip(2), line => Assert.All(line.Split(',').Skip(1), value =>
            Assert.True(double.IsFinite(double.Parse(value, CultureInfo.InvariantCulture)))));
        string replay = Path.Combine(files.Root, "replay");
        result = Run("--manifest", Path.Combine(original, "manifest.json"), "--out", replay);
        Assert.True(result.Code == 0, result.Error);
        Assert.Equal(File.ReadAllBytes(Path.Combine(original, "metrics.csv")),
            File.ReadAllBytes(Path.Combine(Assert.Single(Directory.GetDirectories(replay)), "metrics.csv")));
    }

    [Fact]
    public void RuntimeChannels_RecordOnlyCompletedStepCosts_AndCannotBeExactlyReplayed()
    {
        using var files = new TestFiles();
        string output = Path.Combine(files.Root, "runs");
        var result = Run("--ticks", "2", "--agents", "4", "--metrics",
            "population,runtime.wallMilliseconds,runtime.allocatedBytes", "--out", output);
        Assert.True(result.Code == 0, result.Error);
        string original = Assert.Single(Directory.GetDirectories(output));
        string[] lines = File.ReadAllLines(Path.Combine(original, "metrics.csv"));
        Assert.Equal("0,4,,", lines[1]);
        Assert.All(lines.Skip(2), line => Assert.All(line.Split(',').Skip(2), value =>
            Assert.InRange(double.Parse(value, CultureInfo.InvariantCulture), 0d, double.MaxValue)));
        Assert.False(ReadManifest(original)["metricsDeterministic"]!.GetValue<bool>());
        Assert.Contains("Step only", File.ReadAllText(Path.Combine(original, "log.txt")), StringComparison.Ordinal);

        string replayOutput = Path.Combine(files.Root, "replay");
        result = Run("--manifest", Path.Combine(original, "manifest.json"), "--out", replayOutput);
        Assert.Equal(1, result.Code);
        Assert.Contains("nondeterministic", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(replayOutput));
    }

    [Theory]
    [InlineData("canonical", "unknown", 1)]
    [InlineData("canonical", "Population", 1)]
    [InlineData("canonical", "population,population", 1)]
    [InlineData("canonical", "population,,meanSpeed", 2)]
    [InlineData("legacy", "boids.steeringMagnitudeMean", 1)]
    public void InvalidOrInapplicableMetricNames_FailBeforeOutput(string engine, string names, int expectedCode)
    {
        using var files = new TestFiles();
        string output = Path.Combine(files.Root, "runs");
        var result = Run("--engine", engine, "--ticks", "0", "--agents", "2", "--metrics", names, "--out", output);

        Assert.Equal(expectedCode, result.Code);
        Assert.NotEmpty(result.Error);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void MetricDiscovery_UsesTheSelectedEngine_AndCreatesNoRun()
    {
        var canonical = Run("--list-metrics");
        var legacy = Run("--engine", "legacy", "--list-metrics");

        Assert.Equal(0, canonical.Code);
        Assert.Equal(0, legacy.Code);
        foreach (string name in new[] { "population", "centroidX", "nearestNeighborP95", "runtime.allocatedBytes" })
        {
            Assert.Contains(name, canonical.Output, StringComparison.Ordinal);
            Assert.Contains(name, legacy.Output, StringComparison.Ordinal);
        }
        Assert.Contains("boids.steeringMagnitudeMean", canonical.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("boids.", legacy.Output, StringComparison.Ordinal);
        Assert.Contains("O(N^2)", canonical.Output, StringComparison.Ordinal);
        Assert.Contains("nondeterministic", canonical.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayRejectsMetricOverridesEvenWhenTheyMatchTheManifest()
    {
        using var files = new TestFiles();
        string output = Path.Combine(files.Root, "runs");

        var result = Run("--manifest", "source.json", "--metrics", "population", "--out", output);

        Assert.Equal(2, result.Code);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void ChangingSelectedColumnOrder_FailsTheCompleteCsvReplayHash()
    {
        using var files = new TestFiles();
        string output = Path.Combine(files.Root, "initial");
        Assert.Equal(0, Run("--ticks", "1", "--agents", "4", "--metrics", "centroidX,centroidY", "--out", output).Code);
        string original = Assert.Single(Directory.GetDirectories(output));
        JsonObject manifest = ReadManifest(original);
        manifest["requestedMetrics"] = new JsonArray("centroidY", "centroidX");
        string path = Path.Combine(files.Root, "reordered.json");
        File.WriteAllText(path, manifest.ToJsonString());
        string replayOutput = Path.Combine(files.Root, "replay");

        var result = Run("--manifest", path, "--out", replayOutput);

        Assert.Equal(1, result.Code);
        Assert.Contains("hash verification failed", result.Error, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(replayOutput));
        Assert.Equal(2, File.ReadAllLines(Path.Combine(original, "metrics.csv")).Skip(1).Count());
    }

    [Theory]
    [InlineData("metricContractVersion")]
    [InlineData("metricsDeterministic")]
    [InlineData("requestedMetrics")]
    public void InconsistentMetricManifest_IsRejectedBeforeOutput(string field)
    {
        using var files = new TestFiles();
        string output = Path.Combine(files.Root, "initial");
        Assert.Equal(0, Run("--ticks", "0", "--agents", "1", "--out", output).Code);
        JsonObject manifest = ReadManifest(Assert.Single(Directory.GetDirectories(output)));
        if (field == "metricContractVersion") manifest[field] = 99;
        else if (field == "metricsDeterministic") manifest[field] = false;
        else manifest[field] = new JsonArray("unknown");
        string path = Path.Combine(files.Root, "invalid.json");
        File.WriteAllText(path, manifest.ToJsonString());
        string replayOutput = Path.Combine(files.Root, "replay");

        var result = Run("--manifest", path, "--out", replayOutput);

        Assert.Equal(1, result.Code);
        Assert.False(Directory.Exists(replayOutput));
    }

    [Theory]
    [InlineData("canonical")]
    [InlineData("legacy")]
    public void SchemaOneManifest_KeepsItsOriginalDefaultColumnsAndReplays(string engine)
    {
        using var files = new TestFiles();
        string output = Path.Combine(files.Root, "initial");
        Assert.Equal(0, Run("--engine", engine, "--ticks", "3", "--agents", "4", "--out", output).Code);
        string original = Assert.Single(Directory.GetDirectories(output));
        JsonObject manifest = ReadManifest(original);
        manifest["schemaVersion"] = 1;
        manifest.Remove("metricContractVersion");
        manifest.Remove("metricsDeterministic");
        string path = Path.Combine(files.Root, "schema1.json");
        File.WriteAllText(path, manifest.ToJsonString());
        string replayOutput = Path.Combine(files.Root, "replay");

        var result = Run("--manifest", path, "--out", replayOutput);

        Assert.True(result.Code == 0, result.Error);
        string replay = Assert.Single(Directory.GetDirectories(replayOutput));
        Assert.Equal(File.ReadAllBytes(Path.Combine(original, "metrics.csv")), File.ReadAllBytes(Path.Combine(replay, "metrics.csv")));
        Assert.Equal("tick,population,meanSpeed,polarization", File.ReadLines(Path.Combine(replay, "metrics.csv")).First());
    }

    [Theory]
    [InlineData("metricContractVersion")]
    [InlineData("metricsDeterministic")]
    public void SchemaOneManifest_RejectsExplicitNullSchemaTwoMetadata(string field)
    {
        using var files = new TestFiles();
        string output = Path.Combine(files.Root, "initial");
        Assert.Equal(0, Run("--ticks", "0", "--agents", "1", "--out", output).Code);
        JsonObject manifest = ReadManifest(Assert.Single(Directory.GetDirectories(output)));
        manifest["schemaVersion"] = 1;
        manifest.Remove("metricContractVersion");
        manifest.Remove("metricsDeterministic");
        manifest[field] = null;
        string path = Path.Combine(files.Root, "schema1-extra-field.json");
        File.WriteAllText(path, manifest.ToJsonString());
        string replayOutput = Path.Combine(files.Root, "replay");

        var result = Run("--manifest", path, "--out", replayOutput);

        Assert.Equal(1, result.Code);
        Assert.Contains("no schema 2 metric metadata", result.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(replayOutput));
    }

    private static JsonObject ReadManifest(string directory) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")))!.AsObject();

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int code = CliProgram.Execute(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private sealed class TestFiles : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), $"swarm-metric-cli-{Guid.NewGuid():N}");
        internal TestFiles() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
