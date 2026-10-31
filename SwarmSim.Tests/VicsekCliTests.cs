using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SwarmSim.Tests;

public sealed class VicsekCliTests
{
    // Locked before the regression seeds were first run. Calibration used seeds 1-5 only and
    // measured 0.988-0.989 low and 0.124-0.152 high; see docs/VICSEK.md. Do not move these or
    // change the recipe seeds to make a failure pass.
    private const int WindowStart = 1000;
    private const double LowNoiseFloor = 0.95d;
    private const double HighNoiseCeiling = 0.25d;
    private const double EnsembleMargin = 0.65d;

    [Fact]
    public void RepeatAndManifestReplayAreByteIdentical()
    {
        using var files = new SweepCliTests.SweepFiles();
        string[] arguments = ["--engine", "vicsek", "--seed", "7", "--ticks", "32", "--agents", "64", "--out", files.Output];
        var first = SweepCliTests.Run(arguments);
        var second = SweepCliTests.Run(arguments);
        Assert.True(first.Code == 0, first.Error);
        Assert.True(second.Code == 0, second.Error);

        string run = first.Output.Trim();
        byte[] metrics = File.ReadAllBytes(Path.Combine(run, "metrics.csv"));
        Assert.Equal(metrics, File.ReadAllBytes(Path.Combine(second.Output.Trim(), "metrics.csv")));

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(run, "manifest.json")));
        Assert.Equal("vicsek", manifest.RootElement.GetProperty("engine").GetString());
        Assert.Equal("vicsek-uniform-v1", manifest.RootElement.GetProperty("spawnModel").GetString());
        Assert.Equal(0.5f, manifest.RootElement.GetProperty("resolvedConfig").GetProperty("Noise").GetSingle());

        var replay = SweepCliTests.Run("--manifest", Path.Combine(run, "manifest.json"), "--out", files.Output);
        Assert.True(replay.Code == 0, replay.Error);
        Assert.Equal(metrics, File.ReadAllBytes(Path.Combine(replay.Output.Trim(), "metrics.csv")));
    }

    [Fact]
    public void ReplayRefusesAnEditedConfiguration()
    {
        using var files = new SweepCliTests.SweepFiles();
        var run = SweepCliTests.Run("--engine", "vicsek", "--ticks", "16", "--agents", "32", "--out", files.Output);
        Assert.True(run.Code == 0, run.Error);
        string path = Path.Combine(run.Output.Trim(), "manifest.json");
        JsonNode manifest = JsonNode.Parse(File.ReadAllText(path))!;
        manifest["resolvedConfig"]!["Noise"] = 1.5;
        File.WriteAllText(path, manifest.ToJsonString());

        var replay = SweepCliTests.Run("--manifest", path, "--out", files.Output);
        Assert.Equal(1, replay.Code);
        Assert.Contains("hash verification failed", replay.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ScenarioLayerIsRefusedBecauseNamedScenariosAreBoidsRecipes()
    {
        var result = SweepCliTests.Run("--engine", "vicsek", "--scenario", "balanced");
        Assert.Equal(2, result.Code);
        Assert.Contains("--config", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ImpossiblePopulationFailsAsARunErrorBeforeAllocating()
    {
        var result = SweepCliTests.Run("--engine", "vicsek", "--agents", "2000000000", "--ticks", "1");
        Assert.Equal(1, result.Code);
        Assert.Contains("--agents exceeds", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AnotherEnginesConfigurationIsRefused()
    {
        using var files = new SweepCliTests.SweepFiles();
        var result = SweepCliTests.Run("--engine", "vicsek", "--config", files.Base, "--out", files.Output);
        Assert.Equal(1, result.Code);
        Assert.False(Directory.Exists(files.Output));
    }

    [Fact]
    public void BoidsRuleMetricsAreNotAdvertised()
    {
        var result = SweepCliTests.Run("--engine", "vicsek", "--list-metrics");
        Assert.Equal(0, result.Code);
        Assert.Contains("polarization", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("separation", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoiseSweepRepeatsByteIdentically()
    {
        using var files = new SweepCliTests.SweepFiles();
        string sweep = Path.Combine(files.Root, "spec", "noise.json");
        File.WriteAllText(Path.Combine(files.Root, "spec", "vicsek.json"), "{\"Speed\":0.1}");
        File.WriteAllText(sweep, """
            {"base":"vicsek.json","engine":"vicsek","agents":32,"seeds":[3,1],
             "grid":{"Noise":[0.5,5]},"ticks":16,"metrics":["polarization"]}
            """);

        var first = SweepCliTests.Run("--sweep", sweep, "--out", files.Output);
        var second = SweepCliTests.Run("--sweep", sweep, "--out", files.Output);
        Assert.True(first.Code == 0, first.Error);
        Assert.Equal(File.ReadAllBytes(Path.Combine(first.Output.Trim(), "summary.csv")),
            File.ReadAllBytes(Path.Combine(second.Output.Trim(), "summary.csv")));
        Assert.Equal(5, File.ReadAllLines(Path.Combine(first.Output.Trim(), "summary.csv")).Length);
    }

    [Fact]
    public void LockedRecipeOrdersAtLowNoiseAndDisordersAtHighNoise()
    {
        using var files = new SweepCliTests.SweepFiles();
        var sweep = SweepCliTests.Run("--sweep", Path.Combine(RepositoryRoot(), "scenarios", "vicsek", "noise-response.json"), "--out", files.Output);
        Assert.True(sweep.Code == 0, sweep.Error);

        var low = new List<double>();
        var high = new List<double>();
        string root = sweep.Output.Trim();
        string[] summary = File.ReadAllLines(Path.Combine(root, "summary.csv"));
        int noiseColumn = Array.IndexOf(summary[0].Split(','), "Noise");
        foreach (string row in summary.Skip(1))
        {
            string[] cells = row.Split(',');
            double noise = double.Parse(cells[noiseColumn], CultureInfo.InvariantCulture);
            double order = WindowMean(Path.Combine(root, cells[0], "metrics.csv"));
            (noise < 1d ? low : high).Add(order);
        }

        Assert.Equal(5, low.Count);
        Assert.Equal(5, high.Count);
        Assert.All(low, order => Assert.True(order >= LowNoiseFloor, $"low-noise seed order {order}"));
        Assert.All(high, order => Assert.True(order <= HighNoiseCeiling, $"high-noise seed order {order}"));
        Assert.True(low.Average() - high.Average() >= EnsembleMargin);
    }

    private static double WindowMean(string metricsCsv)
    {
        string[] rows = File.ReadAllLines(metricsCsv);
        string[] header = rows[0].Split(',');
        int column = Array.IndexOf(header, "polarization");
        // Row 1 is tick 0, so ticks after the burn-in begin at row WindowStart + 2.
        return rows.Skip(WindowStart + 2)
            .Select(row => double.Parse(row.Split(',')[column], CultureInfo.InvariantCulture))
            .Average();
    }

    private static string RepositoryRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "SwarmingLilMen.sln")))
            directory = Path.GetDirectoryName(directory);
        return directory ?? throw new InvalidOperationException("Repository root not found.");
    }
}
