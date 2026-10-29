using System.Globalization;
using System.Text.Json;
using SwarmSim.Core;
using CliProgram = SwarmSim.Cli.Program;

namespace SwarmSim.Tests;

public sealed class LegacyCapacityCliTests
{
    [Fact]
    public void LegacyPopulationBeyondInitialCapacityHasCompleteMetricsAndReplay()
    {
        using var files = new TestFiles();
        string config = files.Config(initial: 2, maximum: 5);

        var result = Run("--engine", "legacy", "--config", config, "--agents", "5", "--ticks", "16",
            "--metrics", "population,nearestNeighborMean,centroidX", "--out", Path.Combine(files.Root, "growing"));

        Assert.True(result.Code == 0, result.Error);
        string original = result.Output.Trim();
        string[] rows = File.ReadAllLines(Path.Combine(original, "metrics.csv"));
        Assert.Equal(18, rows.Length);
        Assert.All(rows.Skip(1), row => Assert.Equal("5", row.Split(',')[1]));
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(original, "manifest.json")));
        Assert.Equal(2, manifest.RootElement.GetProperty("resolvedConfig").GetProperty("InitialCapacity").GetInt32());
        Assert.Equal(5, manifest.RootElement.GetProperty("resolvedConfig").GetProperty("MaxCapacity").GetInt32());

        var replay = Run("--manifest", Path.Combine(original, "manifest.json"), "--out", Path.Combine(files.Root, "replay"));
        Assert.True(replay.Code == 0, replay.Error);
        Assert.Equal(File.ReadAllBytes(Path.Combine(original, "metrics.csv")),
            File.ReadAllBytes(Path.Combine(replay.Output.Trim(), "metrics.csv")));

        var preallocated = Run("--engine", "legacy", "--config", files.Config(initial: 5, maximum: 5),
            "--agents", "5", "--ticks", "16", "--metrics", "population,nearestNeighborMean,centroidX",
            "--out", Path.Combine(files.Root, "preallocated"));
        Assert.True(preallocated.Code == 0, preallocated.Error);
        Assert.Equal(File.ReadAllBytes(Path.Combine(original, "metrics.csv")),
            File.ReadAllBytes(Path.Combine(preallocated.Output.Trim(), "metrics.csv")));
        using JsonDocument fullManifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(preallocated.Output.Trim(), "manifest.json")));
        Assert.Equal(manifest.RootElement.GetProperty("finalKinematicHash").GetString(),
            fullManifest.RootElement.GetProperty("finalKinematicHash").GetString());
    }

    [Fact]
    public void LegacyPopulationBeyondMaximumFailsBeforeOutput()
    {
        using var files = new TestFiles();
        string output = Path.Combine(files.Root, "rejected");

        var result = Run("--engine", "legacy", "--config", files.Config(2, 5),
            "--agents", "6", "--ticks", "0", "--out", output);

        Assert.Equal(1, result.Code);
        Assert.Contains("MaxCapacity", result.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void LegacySweepPreflightAcceptsInitialCapacityAxesBelowPopulation()
    {
        using var files = new TestFiles();
        string configuration = files.Config(2, 5);
        string sweep = Path.Combine(files.Root, "sweep.json");
        File.WriteAllText(sweep, JsonSerializer.Serialize(new
        {
            @base = Path.GetFileName(configuration), engine = "legacy", seeds = new[] { 7 },
            grid = new { InitialCapacity = new[] { 1, 2, 5 } }, agents = 5, ticks = 2,
            metrics = new[] { "population", "nearestNeighborMean" }
        }));

        var result = Run("--sweep", sweep, "--out", Path.Combine(files.Root, "sweeps"));

        Assert.True(result.Code == 0, result.Error);
        string output = result.Output.Trim();
        Assert.Equal(3, Directory.GetDirectories(output).Length);
        byte[] expected = File.ReadAllBytes(Path.Combine(output, "cell-000000", "metrics.csv"));
        Assert.Equal(expected, File.ReadAllBytes(Path.Combine(output, "cell-000001", "metrics.csv")));
        Assert.Equal(expected, File.ReadAllBytes(Path.Combine(output, "cell-000002", "metrics.csv")));
    }

    private static (int Code, string Output, string Error) Run(params string[] arguments)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        int code = CliProgram.Execute(arguments, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private sealed class TestFiles : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "swarm-capacity-test-" + Guid.NewGuid().ToString("N"));

        internal TestFiles() => Directory.CreateDirectory(Root);

        internal string Config(int initial, int maximum)
        {
            string path = Path.Combine(Root, $"config-{initial}-{maximum}.json");
            new SimConfig
            {
                Seed = 7u, InitialCapacity = initial, MaxCapacity = maximum,
                WorldWidth = 64f, WorldHeight = 64f, GridCellSize = 8f,
                SenseRadius = 18f, SeparationRadius = 6f, FieldOfView = 360f,
                FixedDeltaTime = 1f / 32f, MaxSpeed = 3f, MaxForce = 0.4f,
                WanderStrength = 0.25f
            }.SaveToJson(path);
            return path;
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
