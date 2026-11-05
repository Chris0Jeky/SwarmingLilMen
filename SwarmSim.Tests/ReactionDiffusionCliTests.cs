using System.Text.Json;
using System.Text.Json.Nodes;
using SwarmSim.Cli;
using SwarmSim.Core.Configuration;
using SwarmSim.Core.Diagnostics;
using SwarmSim.Core.Metrics;
using SwarmSim.Core.ReactionDiffusion;

namespace SwarmSim.Tests;

public sealed class ReactionDiffusionCliTests
{
    private const string Engine = "reaction-diffusion";
    private const string Small = """{"Columns":8,"Rows":8,"PatchFraction":0.5,"PatchNoise":0}""";

    [Fact]
    public void FieldEngineDefaultsToZeroAgentsAndAcceptsExplicitZeroInEitherArgumentOrder()
    {
        foreach (string[] args in new[]
        {
            new[] { "--engine", Engine },
            new[] { "--engine", Engine, "--agents", "0" },
            new[] { "--agents", "0", "--engine", Engine }
        })
        {
            RunOptions options = RunOptions.Parse(args);
            Assert.Equal(0, options.AgentCount);
            RunRequest request = RunRequest.Resolve(options);
            Assert.Equal(0, request.AgentCount);
            Assert.False(request.Definition.SupportsKinematics);
            Assert.Equal(new[] { "reactionDiffusion.uMean", "reactionDiffusion.vMean" }, request.RequestedMetrics);
            Assert.IsType<ReactionDiffusionSettings>(request.Settings);
        }
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("canonical")]
    [InlineData("vicsek")]
    [InlineData("aco")]
    [InlineData("firefly")]
    public void AgentEnginesKeepTheirDefaultsAndRejectZero(string engine)
    {
        RunOptions options = RunOptions.Parse(["--engine", engine]);
        Assert.Equal(400, options.AgentCount);
        Assert.True(EngineRegistry.Get(engine).SupportsKinematics);
        Assert.Equal(engine == "firefly" ? new[] { "population", "firefly.coherence" }
            : new[] { "population", "meanSpeed", "polarization" }, options.RequestedMetrics);
        Assert.Throws<UsageException>(() => RunOptions.Parse(["--engine", engine, "--agents", "0"]));
        Assert.Contains(MetricCatalog.Describe(engine), metric => metric.Name == "population");
        Assert.Contains(MetricCatalog.Describe(engine), metric => metric.Name == "nearestNeighborMean");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("400")]
    [InlineData("-1")]
    public void NonzeroFieldAgentCountsFailBeforeOutput(string count)
    {
        using var files = new SweepCliTests.SweepFiles();
        var result = SweepCliTests.Run("--engine", Engine, "--agents", count, "--out", files.Output);
        Assert.Equal(2, result.Code);
        Assert.Contains("--agents", result.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(files.Output));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void DirectRequestsCannotConstructAFieldSimulationWithAgents(int count)
    {
        var request = new RunRequest(Engine, 1, count, new ReactionDiffusionSettings(), null);
        Assert.Throws<ArgumentException>(() => new PreparedSimulation(request));
    }

    [Theory]
    [InlineData("population")]
    [InlineData("meanSpeed")]
    [InlineData("polarization")]
    [InlineData("centroidX")]
    [InlineData("nearestNeighborMean")]
    public void FieldEngineRejectsSharedAgentMetricsBeforeOutput(string metric)
    {
        using var files = new SweepCliTests.SweepFiles();
        var result = SweepCliTests.Run("--engine", Engine, "--metrics", metric, "--out", files.Output);
        Assert.Equal(1, result.Code);
        Assert.Contains(metric, result.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(files.Output));
    }

    [Fact]
    public void DiscoveryPublishesNativeFieldAndCountSemanticsWithoutAgentMetrics()
    {
        var result = SweepCliTests.Run("--engine", Engine, "--describe-engine", "--json");
        Assert.True(result.Code == 0, result.Error);
        using JsonDocument json = JsonDocument.Parse(result.Output);
        JsonElement engine = json.RootElement.GetProperty("engine");
        Assert.False(engine.GetProperty("supportsKinematics").GetBoolean());
        Assert.Equal(0, engine.GetProperty("defaultAgentCount").GetInt32());
        Assert.Contains("zero", engine.GetProperty("agentCountContract").GetString()!, StringComparison.Ordinal);
        Assert.Equal(3, engine.GetProperty("minimumManifestSchema").GetInt32());
        Assert.Equal("gray-scott-centered-patch-v1", engine.GetProperty("spawnModel").GetString());
        Assert.Equal(ReactionDiffusionWorld.ModelStateProfile, engine.GetProperty("modelStateProfile").GetString());
        Assert.False(engine.GetProperty("acceptsScenario").GetBoolean());
        Assert.False(engine.GetProperty("interactiveRenderer").GetBoolean());
        Assert.False(engine.GetProperty("checkpointRestore").GetBoolean());
        Assert.Equal(64, engine.GetProperty("defaultConfiguration").GetProperty("Columns").GetInt32());
        Assert.Equal(new[] { "reactionDiffusion.uMean", "reactionDiffusion.vMean" },
            engine.GetProperty("defaultMetrics").EnumerateArray().Select(value => value.GetString()));
        string[] channels = engine.GetProperty("metrics").EnumerateArray()
            .Select(value => value.GetProperty("name").GetString()!).ToArray();
        Assert.Equal(8, channels.Length);
        Assert.Contains("runtime.wallMilliseconds", channels);
        Assert.Contains("reactionDiffusion.uMin", channels);
        Assert.Contains("reactionDiffusion.vMax", channels);
        Assert.All(channels, name => Assert.True(name.StartsWith("reactionDiffusion.", StringComparison.Ordinal)
            || name.StartsWith("runtime.", StringComparison.Ordinal), name));
        Assert.False(MetricCatalog.ValidateSelection(Engine, ["runtime.allocatedBytes", "reactionDiffusion.vMean"]));

        var text = SweepCliTests.Run("--engine", Engine, "--describe-engine");
        Assert.Contains("Field-only observations", text.Output, StringComparison.Ordinal);
        Assert.Contains("--agents is 0", text.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void AdapterExposesFieldCopiesAndEmptyKinematicsWithoutChangingModelState()
    {
        var request = new RunRequest(Engine, 1, 0, ReactionDiffusionSettings.FromJson(Small), null);
        var simulation = new PreparedSimulation(request);
        var adapter = Assert.IsType<ReactionDiffusionSimulation>(simulation.Simulation);
        Assert.Equal(0, simulation.MetricCapacity);
        Assert.Equal(8f, simulation.WorldWidth);
        Assert.Equal(8f, simulation.WorldHeight);
        Assert.True(simulation.Domain.IsPeriodic);
        Assert.Equal(8, adapter.FieldColumns);
        Assert.Equal(8, adapter.FieldRows);
        Assert.Equal("V concentration", adapter.FieldLabel);
        Assert.Equal(0f, adapter.ColorMinimum);
        Assert.Equal(1f, adapter.ColorMaximum);

        var sentinel = new KinematicSample(1f, 2f, 3f, 4f);
        KinematicSample[] agents = [sentinel];
        Assert.Equal(0, simulation.Capture(agents));
        Assert.Equal(sentinel, agents[0]);
        Assert.Equal(SimulationKinematicHash.Compute(ReadOnlySpan<KinematicSample>.Empty), simulation.KinematicHash());
        var before = simulation.StateHash();
        float[] field = Enumerable.Repeat(-7f, adapter.World.CellCount + 1).ToArray();
        adapter.CaptureField(field);
        Assert.Equal(adapter.World.Cells.ToArray().Select(cell => cell.V), field.Take(adapter.World.CellCount));
        Assert.Equal(-7f, field[^1]);
        Assert.Equal(before, simulation.StateHash());
        float[] shortBuffer = Enumerable.Repeat(-7f, adapter.World.CellCount - 1).ToArray();
        Assert.Throws<ArgumentException>(() => adapter.CaptureField(shortBuffer));
        Assert.All(shortBuffer, value => Assert.Equal(-7f, value));

        float[] snapshot = field.ToArray();
        simulation.Step();
        Assert.Equal(snapshot, field);
        Assert.NotEqual(before, simulation.StateHash());
        Assert.Equal(SimulationKinematicHash.Compute(ReadOnlySpan<KinematicSample>.Empty), simulation.KinematicHash());
    }

    [Fact]
    public void NativeConfigurationLayersUseStrictGridSettingsAndRejectBoidsScenarios()
    {
        using var files = new SweepCliTests.SweepFiles();
        File.WriteAllText(files.Base, """{"columns":12,"Feed":0.04,"Seed":91}""");
        var overrides = new Dictionary<string, JsonElement>
        {
            ["FEED"] = JsonSerializer.SerializeToElement(0.03f),
            ["Seed"] = JsonSerializer.SerializeToElement(17u)
        };
        ReactionDiffusionSettings settings = ConfigurationResolver.ResolveReactionDiffusionSettings(files.Base, overrides,
            new ReactionDiffusionSettings { Rows = 6, Feed = 0.01f });
        Assert.Equal(12, settings.Columns);
        Assert.Equal(6, settings.Rows);
        Assert.Equal(0.03f, settings.Feed);
        Assert.Equal(17u, settings.Seed);
        Assert.Throws<ArgumentException>(() => ConfigurationResolver.ResolveReactionDiffusionSettings(overrides:
            new Dictionary<string, JsonElement> { ["TargetSpeed"] = JsonSerializer.SerializeToElement(1f) }));
        var scenario = SweepCliTests.Run("--engine", Engine, "--scenario", "balanced", "--out", files.Output);
        Assert.Equal(2, scenario.Code);
        Assert.False(Directory.Exists(files.Output));
    }

    [Theory]
    [InlineData("{\"WorldWidth\":8}")]
    [InlineData("{\"Colums\":8}")]
    [InlineData("{\"Columns\":0}")]
    [InlineData("{\"DiffusionU\":-0.1}")]
    public void InvalidNativeConfigurationFailsBeforeOutput(string json)
    {
        using var files = new SweepCliTests.SweepFiles();
        File.WriteAllText(files.Base, json);
        var result = SweepCliTests.Run("--engine", Engine, "--config", files.Base, "--out", files.Output);
        Assert.Equal(1, result.Code);
        Assert.False(Directory.Exists(files.Output));
    }

    [Fact]
    public void RunRepeatReplayAndVisualCapturePreserveNativeMetricsAndBothHashes()
    {
        using var files = new SweepCliTests.SweepFiles();
        File.WriteAllText(files.Base, Small);
        string[] args = ["--engine", Engine, "--config", files.Base, "--seed", "7", "--ticks", "8", "--out", files.Output];
        string plain = Succeed(args);
        string repeated = Succeed(args);
        string replay = Succeed("--manifest", Path.Combine(plain, "manifest.json"), "--out", files.Output);
        string visual = Succeed([.. args, "--visual"]);
        RunManifest expected = Read(plain);
        Assert.Equal(3, expected.SchemaVersion);
        Assert.Equal(0, expected.AgentCount);
        Assert.Equal(ReactionDiffusionWorld.ModelStateProfile, expected.ModelStateProfile);
        Assert.Equal("gray-scott-centered-patch-v1", expected.SpawnModel);
        Assert.Equal(1, expected.KinematicHashVersion);
        Assert.Equal("tick,reactionDiffusion.uMean,reactionDiffusion.vMean",
            File.ReadLines(Path.Combine(plain, "metrics.csv")).First());
        foreach (string directory in new[] { repeated, replay, visual })
        {
            Assert.Equal(File.ReadAllBytes(Path.Combine(plain, "metrics.csv")), File.ReadAllBytes(Path.Combine(directory, "metrics.csv")));
            RunManifest actual = Read(directory);
            Assert.Equal(expected.FinalKinematicHash, actual.FinalKinematicHash);
            Assert.Equal(expected.FinalModelStateHash, actual.FinalModelStateHash);
            Assert.Equal(expected.MetricsSha256, actual.MetricsSha256);
        }
        Assert.True(File.Exists(Path.Combine(visual, "filmstrip.png")));
        Assert.True(File.Exists(Path.Combine(visual, "thumbnail.png")));
        string report = File.ReadAllText(Path.Combine(visual, "report.html"));
        Assert.Contains("Colour shows V concentration on a fixed scale", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Colour is heading angle", report, StringComparison.Ordinal);
        Assert.Contains(expected.FinalModelStateHash!, report, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeCostsCanBeRecordedWithNativeFieldMetricsButCannotBeExactlyReplayed()
    {
        using var files = new SweepCliTests.SweepFiles();
        File.WriteAllText(files.Base, Small);
        string run = Succeed("--engine", Engine, "--config", files.Base, "--ticks", "2",
            "--metrics", "runtime.allocatedBytes,reactionDiffusion.vMean", "--out", files.Output);
        RunManifest manifest = Read(run);
        Assert.False(manifest.MetricsDeterministic);
        Assert.Equal(0, manifest.AgentCount);
        Assert.Equal("tick,runtime.allocatedBytes,reactionDiffusion.vMean",
            File.ReadLines(Path.Combine(run, "metrics.csv")).First());
        string output = Path.Combine(files.Root, "runtime-replay");
        var replay = SweepCliTests.Run("--manifest", Path.Combine(run, "manifest.json"), "--out", output);
        Assert.Equal(1, replay.Code);
        Assert.Contains("nondeterministic runtime metrics", replay.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void ChangedFieldLayoutCannotReplayUsingOnlyIdenticalMeansAndEmptyKinematics()
    {
        using var files = new SweepCliTests.SweepFiles();
        File.WriteAllText(files.Base, Small);
        string[] args = ["--engine", Engine, "--config", files.Base, "--ticks", "0", "--out", files.Output];
        string original = Succeed(args);
        File.WriteAllText(files.Base, """{"Columns":4,"Rows":16,"PatchFraction":0.5,"PatchNoise":0}""");
        string changed = Succeed(args);
        Assert.Equal(Read(original).FinalKinematicHash, Read(changed).FinalKinematicHash);
        Assert.Equal(File.ReadAllBytes(Path.Combine(original, "metrics.csv")), File.ReadAllBytes(Path.Combine(changed, "metrics.csv")));
        Assert.NotEqual(Read(original).FinalModelStateHash, Read(changed).FinalModelStateHash);

        JsonObject manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(original, "manifest.json")))!.AsObject();
        manifest["resolvedConfig"]!["Columns"] = 4;
        manifest["resolvedConfig"]!["Rows"] = 16;
        string path = Path.Combine(files.Root, "changed-manifest.json");
        File.WriteAllText(path, manifest.ToJsonString());
        string output = Path.Combine(files.Root, "failed-replay");
        var result = SweepCliTests.Run("--manifest", path, "--out", output);
        Assert.Equal(1, result.Code);
        Assert.Contains("model-state hash verification failed", result.Error, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(output));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(3, -1)]
    public void ReplayRejectsOldSchemasOrNonzeroFieldAgentCountsBeforeOutput(int schema, int agents)
    {
        using var files = new SweepCliTests.SweepFiles();
        File.WriteAllText(files.Base, Small);
        string run = Succeed("--engine", Engine, "--config", files.Base, "--ticks", "0", "--out", files.Output);
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(run, "manifest.json")))!.AsObject();
        manifest["schemaVersion"] = schema;
        manifest["agentCount"] = agents;
        string path = Path.Combine(files.Root, "invalid-manifest.json");
        File.WriteAllText(path, manifest.ToJsonString());
        string output = Path.Combine(files.Root, "failed-replay");
        var result = SweepCliTests.Run("--manifest", path, "--out", output);
        Assert.Equal(1, result.Code);
        Assert.Contains(schema < 3 ? "require schema 3" : "agentCount", result.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(output));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SweepUsesZeroAgentDefaultOrExplicitZeroAndRejectsPositiveCounts(bool explicitZero)
    {
        using var files = new SweepCliTests.SweepFiles();
        File.WriteAllText(files.Base, Small);
        var spec = new JsonObject
        {
            ["base"] = "base.json", ["engine"] = Engine, ["ticks"] = 2,
            ["seeds"] = new JsonArray(7), ["grid"] = new JsonObject { ["Feed"] = new JsonArray(0.03, 0.04) },
            ["metrics"] = new JsonArray("reactionDiffusion.uMean", "reactionDiffusion.vMean")
        };
        if (explicitZero) spec["agents"] = 0;
        string path = files.WriteDefinition(spec);
        SweepPlan plan = SweepPlan.Load(path);
        Assert.Equal(2, plan.Cells.Count);
        Assert.All(plan.Cells, cell => Assert.Equal(0, cell.Request.AgentCount));
        string first = Succeed("--sweep", path, "--out", files.Output);
        string second = Succeed("--sweep", path, "--out", files.Output);
        Assert.Equal(File.ReadAllBytes(Path.Combine(first, "summary.csv")), File.ReadAllBytes(Path.Combine(second, "summary.csv")));
        Assert.All(Directory.GetDirectories(first), cell => Assert.Equal(0, Read(cell).AgentCount));
        spec["agents"] = 1;
        Assert.Throws<ArgumentException>(() => SweepPlan.Load(files.WriteDefinition(spec)));
    }

    [Fact]
    public void GalleryAcceptsExplicitZeroAndProducesAFieldReportButRejectsPositiveCounts()
    {
        using var files = new SweepCliTests.SweepFiles();
        string path = Path.Combine(files.Root, "gallery.json");
        string recipe = """
            {"entries":[{"name":"field","engine":"reaction-diffusion",
              "settings":{"Columns":8,"Rows":8,"PatchFraction":0.5,"PatchNoise":0},
              "agents":0,"ticks":2,"seed":7,"metrics":["reactionDiffusion.vMean"],
              "expect":{"reactionDiffusion.vMean":{"min":0,"max":1}}}]}
            """;
        File.WriteAllText(path, recipe);
        GalleryPlan plan = GalleryPlan.Load(path);
        Assert.Equal(0, Assert.Single(plan.Entries).Request.AgentCount);
        var result = SweepCliTests.Run("--gallery", path, "--out", files.Output);
        Assert.True(result.Code == 0, result.Error + result.Output);
        string directory = result.Output.Split('\n')[0].Trim();
        string run = Path.Combine(directory, "01-field");
        Assert.Equal(0, Read(run).AgentCount);
        Assert.Contains("Colour shows V concentration", File.ReadAllText(Path.Combine(run, "report.html")), StringComparison.Ordinal);
        File.WriteAllText(path, recipe.Replace("\"agents\":0", "\"agents\":1", StringComparison.Ordinal));
        Assert.Throws<ArgumentException>(() => GalleryPlan.Load(path));
    }

    private static string Succeed(params string[] arguments)
    {
        var result = SweepCliTests.Run(arguments);
        Assert.True(result.Code == 0, result.Error);
        return result.Output.Trim();
    }

    private static RunManifest Read(string directory) => RunManifest.Read(Path.Combine(directory, "manifest.json"));
}
