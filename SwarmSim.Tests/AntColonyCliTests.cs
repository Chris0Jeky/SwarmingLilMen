using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SwarmSim.Cli;
using SwarmSim.Core.Aco;
using SwarmSim.Core.Metrics;

namespace SwarmSim.Tests;

public sealed class AntColonyCliTests
{
    private const string Metrics = "population,meanSpeed,aco.foodReturned,aco.carrying,aco.fieldTotal";

    // Locked before the regression seeds were first run. Calibration used seeds 1-5 only and
    // measured per-seed ratios 4.61-4.93, ensemble 4.73 and controls 490-512; see docs/ACO.md. Do
    // not move these or change the recipe seeds to make a failure pass. The control floor keeps a
    // dead control from manufacturing a large ratio.
    private const double SeedRatioFloor = 3.5d;
    private const double EnsembleRatioFloor = 4d;
    private const double ControlFloor = 300d;

    [Fact]
    public void RepeatAndManifestReplayAreByteIdentical()
    {
        using var files = new SweepCliTests.SweepFiles();
        string[] arguments = ["--engine", "aco", "--seed", "7", "--ticks", "300", "--agents", "64", "--metrics", Metrics, "--out", files.Output];
        var first = SweepCliTests.Run(arguments);
        var second = SweepCliTests.Run(arguments);
        Assert.True(first.Code == 0, first.Error);
        Assert.True(second.Code == 0, second.Error);

        string run = first.Output.Trim();
        byte[] metrics = File.ReadAllBytes(Path.Combine(run, "metrics.csv"));
        Assert.Equal(metrics, File.ReadAllBytes(Path.Combine(second.Output.Trim(), "metrics.csv")));

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(run, "manifest.json")));
        JsonElement root = manifest.RootElement;
        Assert.Equal("aco", root.GetProperty("engine").GetString());
        Assert.Equal("aco-nest-v1", root.GetProperty("spawnModel").GetString());
        Assert.Equal(1, root.GetProperty("resolvedConfig").GetProperty("FieldCadence").GetInt32());

        var replay = SweepCliTests.Run("--manifest", Path.Combine(run, "manifest.json"), "--out", files.Output);
        Assert.True(replay.Code == 0, replay.Error);
        Assert.Equal(metrics, File.ReadAllBytes(Path.Combine(replay.Output.Trim(), "metrics.csv")));
    }

    [Fact]
    public void FieldBitsMatchAcrossRepeatsThroughTheEngineAdapter()
    {
        // Hash version 1 covers kinematics only, so the field gets its own exact comparison.
        var settings = new AntColonySettings { Seed = 7u };
        var first = new PreparedSimulation(new RunRequest("aco", 400, 64, settings, null));
        var second = new PreparedSimulation(new RunRequest("aco", 400, 64, settings, null));

        for (int tick = 0; tick < 400; tick++)
        {
            first.Step();
            second.Step();
        }

        float[] cells = ((AcoSimulation)first.Simulation).World.Field.Cells.ToArray();
        Assert.Contains(cells, cell => cell > 0f);
        Assert.Equal(cells.Select(BitConverter.SingleToUInt32Bits),
            ((AcoSimulation)second.Simulation).World.Field.Cells.ToArray().Select(BitConverter.SingleToUInt32Bits));
        Assert.Equal(first.KinematicHash(), second.KinematicHash());
    }

    [Fact]
    public void ReplayRefusesAnEditedDeposit()
    {
        using var files = new SweepCliTests.SweepFiles();
        var run = SweepCliTests.Run("--engine", "aco", "--seed", "7", "--ticks", "300", "--agents", "64", "--out", files.Output);
        Assert.True(run.Code == 0, run.Error);
        string path = Path.Combine(run.Output.Trim(), "manifest.json");
        JsonNode manifest = JsonNode.Parse(File.ReadAllText(path))!;
        manifest["resolvedConfig"]!["Deposit"] = 0;
        File.WriteAllText(path, manifest.ToJsonString());

        var replay = SweepCliTests.Run("--manifest", path, "--out", files.Output);

        Assert.Equal(1, replay.Code);
        Assert.Contains("hash verification failed", replay.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ASchemaOneManifestIsRefusedBecauseTheEngineNeverWroteOne()
    {
        using var files = new SweepCliTests.SweepFiles();
        var run = SweepCliTests.Run("--engine", "aco", "--ticks", "8", "--agents", "8", "--out", files.Output);
        Assert.True(run.Code == 0, run.Error);
        string path = Path.Combine(run.Output.Trim(), "manifest.json");
        JsonNode manifest = JsonNode.Parse(File.ReadAllText(path))!;
        manifest["schemaVersion"] = 1;
        File.WriteAllText(path, manifest.ToJsonString());

        var replay = SweepCliTests.Run("--manifest", path, "--out", files.Output);

        Assert.Equal(1, replay.Code);
        Assert.Contains("aco manifests require schema 2", replay.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ScenarioLayerIsRefusedBecauseNamedScenariosAreBoidsRecipes()
    {
        var result = SweepCliTests.Run("--engine", "aco", "--scenario", "balanced");

        Assert.Equal(2, result.Code);
        Assert.Contains("the aco engine takes --config", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ChannelsAreAdvertisedOnlyForThisEngine()
    {
        var aco = SweepCliTests.Run("--engine", "aco", "--list-metrics");
        Assert.Equal(0, aco.Code);
        Assert.Contains("aco.foodReturned", aco.Output, StringComparison.Ordinal);
        Assert.Contains("polarization", aco.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("boids.", aco.Output, StringComparison.Ordinal);

        var vicsek = SweepCliTests.Run("--engine", "vicsek", "--ticks", "1", "--agents", "4", "--metrics", "aco.carrying");
        Assert.NotEqual(0, vicsek.Code);
    }

    [Fact]
    public void AVisualRunChangesNoMetricByte()
    {
        using var files = new SweepCliTests.SweepFiles();
        string[] arguments = ["--engine", "aco", "--seed", "7", "--ticks", "120", "--agents", "32", "--metrics", Metrics, "--out", files.Output];
        var plain = SweepCliTests.Run(arguments);
        var visual = SweepCliTests.Run([.. arguments, "--visual"]);
        Assert.True(visual.Code == 0, visual.Error);

        Assert.Equal(File.ReadAllBytes(Path.Combine(plain.Output.Trim(), "metrics.csv")),
            File.ReadAllBytes(Path.Combine(visual.Output.Trim(), "metrics.csv")));
        Assert.True(File.Exists(Path.Combine(visual.Output.Trim(), "filmstrip.png")));
        Assert.True(File.Exists(Path.Combine(visual.Output.Trim(), "report.html")));
    }

    [Fact]
    public void ADepositSweepRepeatsByteIdenticallyAndReportsExactCounts()
    {
        using var files = new SweepCliTests.SweepFiles();
        string sweep = Path.Combine(files.Root, "spec", "aco.json");
        File.WriteAllText(Path.Combine(files.Root, "spec", "colony.json"), "{\"Evaporation\":0.02}");
        File.WriteAllText(sweep, """
            {"base":"colony.json","engine":"aco","agents":32,"seeds":[3,1],
             "grid":{"Deposit":[0,1]},"ticks":200,"metrics":["aco.foodReturned","aco.fieldTotal"]}
            """);

        var first = SweepCliTests.Run("--sweep", sweep, "--out", files.Output);
        var second = SweepCliTests.Run("--sweep", sweep, "--out", files.Output);
        Assert.True(first.Code == 0, first.Error);

        byte[] summary = File.ReadAllBytes(Path.Combine(first.Output.Trim(), "summary.csv"));
        Assert.Equal(summary, File.ReadAllBytes(Path.Combine(second.Output.Trim(), "summary.csv")));
        Assert.Equal(5, File.ReadAllLines(Path.Combine(first.Output.Trim(), "summary.csv")).Length);
    }

    [Fact]
    public void AcoSamplesTheProbeThroughTheNormalMetricPlan()
    {
        var request = new RunRequest("aco", 1, 16, new AntColonySettings { Seed = 2u }, null)
            { RequestedMetrics = ["aco.foodReturned", "aco.carrying", "aco.fieldTotal", "population"] };
        var simulation = new PreparedSimulation(request);
        var metrics = new ExperimentMetrics(request, simulation);

        for (int tick = 0; tick < 300; tick++) metrics.StepAndSample();

        AntColonyWorld world = ((AcoSimulation)simulation.Simulation).World;
        Assert.Equal(world.FoodReturned, metrics.Values[0]);
        Assert.Equal(world.CarryingCount, metrics.Values[1]);
        Assert.Equal(world.Field.Total(), metrics.Values[2]);
        Assert.Equal(16d, metrics.Values[3]);
        Assert.True(metrics.Plan.IsDeterministic);
    }

    [Fact]
    public void AdapterAgreesWithTheEngineItWraps()
    {
        var settings = new AntColonySettings { WorldWidth = 120f, WorldHeight = 80f, NestY = 40f, FoodY = 40f };
        var simulation = new AcoSimulation(new RunRequest("aco", 1, 8, settings, null));
        var samples = new KinematicSample[8];

        Assert.Equal(8, simulation.Capture(samples));
        Assert.Equal(8, simulation.MetricCapacity);
        Assert.Equal(120f, simulation.WorldWidth);
        Assert.Equal(80f, simulation.WorldHeight);
        Assert.True(simulation.Domain.IsPeriodic);
        Assert.Throws<ArgumentException>(() => simulation.Capture(new KinematicSample[7]));
    }

    [Fact]
    public void LockedRecipeReturnsMoreFoodWithPheromoneThanItsControl()
    {
        using var files = new SweepCliTests.SweepFiles();
        var sweep = SweepCliTests.Run("--sweep", Path.Combine(RepositoryRoot(), "scenarios", "aco", "food-response.json"), "--out", files.Output);
        Assert.True(sweep.Code == 0, sweep.Error);

        var on = new Dictionary<string, double>();
        var control = new Dictionary<string, double>();
        string[] summary = File.ReadAllLines(Path.Combine(sweep.Output.Trim(), "summary.csv"));
        string[] header = summary[0].Split(',');
        int seed = Array.IndexOf(header, "seed");
        int deposit = Array.IndexOf(header, "Deposit");
        int food = Array.IndexOf(header, "aco.foodReturned.terminal");
        foreach (string row in summary.Skip(1))
        {
            string[] cells = row.Split(',');
            double returned = double.Parse(cells[food], CultureInfo.InvariantCulture);
            (double.Parse(cells[deposit], CultureInfo.InvariantCulture) > 0d ? on : control).Add(cells[seed], returned);
        }

        Assert.Equal(5, on.Count);
        Assert.Equal(on.Keys.Order(), control.Keys.Order());
        Assert.All(control, pair => Assert.True(pair.Value >= ControlFloor, $"seed {pair.Key} control {pair.Value}"));
        Assert.All(on, pair => Assert.True(pair.Value / control[pair.Key] >= SeedRatioFloor,
            $"seed {pair.Key}: {pair.Value} against {control[pair.Key]}"));
        double ensemble = on.Values.Sum() / control.Values.Sum();
        Assert.True(ensemble >= EnsembleRatioFloor, $"ensemble ratio {ensemble}");
    }

    private static string RepositoryRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "SwarmingLilMen.sln")))
            directory = Path.GetDirectoryName(directory);
        return directory ?? throw new InvalidOperationException("Repository root not found.");
    }
}
