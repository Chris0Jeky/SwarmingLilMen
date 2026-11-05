using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SwarmSim.Cli;
using SwarmSim.Core.Configuration;
using SwarmSim.Core.Firefly;
using SwarmSim.Core.Metrics;

namespace SwarmSim.Tests;

public sealed class FireflyCliTests
{
    [Fact]
    public void DefaultRunRepeatAndManifestReplayPreservePhaseMetricsAndBothHashes()
    {
        using var files = new SweepCliTests.SweepFiles();
        string[] arguments = ["--engine", "firefly", "--seed", "7", "--ticks", "32", "--agents", "16", "--out", files.Output];
        string first = Succeed(arguments);
        string second = Succeed(arguments);
        RunManifest manifest = Read(first);

        Assert.Equal("firefly", manifest.Engine);
        Assert.Equal(3, manifest.SchemaVersion);
        Assert.Equal("firefly-uniform-v1", manifest.SpawnModel);
        Assert.Equal(FireflyWorld.ModelStateProfile, manifest.ModelStateProfile);
        Assert.Equal(1, manifest.KinematicHashVersion);
        Assert.Equal(new[] { "population", "firefly.coherence" }, manifest.RequestedMetrics);
        string[] rows = File.ReadAllLines(Path.Combine(first, "metrics.csv"));
        Assert.Equal("tick,population,firefly.coherence", rows[0]);
        Assert.Equal(34, rows.Length); // Header plus tick zero through tick 32.

        string replay = Succeed("--manifest", Path.Combine(first, "manifest.json"), "--out", files.Output);
        foreach (string repeated in new[] { second, replay })
        {
            Assert.Equal(File.ReadAllBytes(Path.Combine(first, "metrics.csv")),
                File.ReadAllBytes(Path.Combine(repeated, "metrics.csv")));
            RunManifest observed = Read(repeated);
            Assert.Equal(manifest.FinalKinematicHash, observed.FinalKinematicHash);
            Assert.Equal(manifest.FinalModelStateHash, observed.FinalModelStateHash);
            Assert.Equal(manifest.MetricsSha256, observed.MetricsSha256);
        }
    }

    [Fact]
    public void NativeFileAndExplicitOverridesUseTheFireflySchemaAndExpectedLayerOrder()
    {
        using var files = new SweepCliTests.SweepFiles();
        File.WriteAllText(files.Base, """{"coupling":2.25,"FrequencySpread":0.1,"WorldWidth":24,"Seed":91}""");
        string run = Succeed("--engine", "firefly", "--config", files.Base, "--seed", "17",
            "--ticks", "1", "--agents", "4", "--out", files.Output);
        RunManifest manifest = Read(run);
        FireflySettings settings = FireflySettings.FromJson(manifest.ResolvedConfig.GetRawText());
        Assert.Equal(17u, manifest.Seed);
        Assert.Equal(17u, settings.Seed);
        Assert.Equal(2.25f, settings.Coupling);
        Assert.Equal(0.1f, settings.FrequencySpread);
        Assert.Equal(24f, settings.WorldWidth);
        Assert.Equal(60f, settings.WorldHeight);
        Assert.Equal(1.8f, settings.MeanFrequency);
        Assert.Equal(0.02f, settings.FixedDeltaTime);

        var overrides = new Dictionary<string, JsonElement>
        {
            ["COUPLING"] = JsonSerializer.SerializeToElement(3f),
            ["Seed"] = JsonSerializer.SerializeToElement(23u)
        };
        FireflySettings resolved = ConfigurationResolver.ResolveFireflySettings(files.Base, overrides,
            new FireflySettings { Coupling = 1f, MeanFrequency = 2.4f, FixedDeltaTime = 0.04f });
        Assert.Equal(3f, resolved.Coupling);
        Assert.Equal(23u, resolved.Seed);
        Assert.Equal(0.1f, resolved.FrequencySpread);
        Assert.Equal(2.4f, resolved.MeanFrequency);
        Assert.Equal(0.04f, resolved.FixedDeltaTime);
    }

    [Theory]
    [InlineData("Couplng")]
    [InlineData("Noise")]
    [InlineData("TargetSpeed")]
    public void UnknownOrOtherEngineConfigurationFieldsAreRejectedBeforeOutput(string field)
    {
        using var files = new SweepCliTests.SweepFiles();
        File.WriteAllText(files.Base, JsonSerializer.Serialize(new Dictionary<string, float> { [field] = 1f }));

        var result = SweepCliTests.Run("--engine", "firefly", "--config", files.Base,
            "--ticks", "1", "--agents", "4", "--out", files.Output);

        Assert.Equal(1, result.Code);
        Assert.Contains("Unknown configuration field", result.Error, StringComparison.Ordinal);
        Assert.Contains(field, result.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(files.Output));
    }

    [Fact]
    public void ExplicitOverridesRejectUnknownNamesAndInvalidResolvedValues()
    {
        var unknown = new Dictionary<string, JsonElement> { ["Noise"] = JsonSerializer.SerializeToElement(1f) };
        var invalid = new Dictionary<string, JsonElement> { ["Coupling"] = JsonSerializer.SerializeToElement(-1f) };
        var nameError = Assert.Throws<ArgumentException>(() => ConfigurationResolver.ResolveFireflySettings(overrides: unknown));
        Assert.Contains("Unknown configuration override", nameError.Message, StringComparison.Ordinal);
        var valueError = Assert.Throws<ArgumentException>(() => ConfigurationResolver.ResolveFireflySettings(overrides: invalid));
        Assert.Contains("Coupling", valueError.Message, StringComparison.Ordinal);

        using var files = new SweepCliTests.SweepFiles();
        File.WriteAllText(files.Base, """{"Coupling":-1}""");
        var result = SweepCliTests.Run("--engine", "firefly", "--config", files.Base,
            "--ticks", "1", "--agents", "4", "--out", files.Output);
        Assert.Equal(1, result.Code);
        Assert.Contains("Coupling", result.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(files.Output));
    }

    [Theory]
    [InlineData("--scenario", "balanced", 2, "takes --config")]
    [InlineData("--agents", "2000000000", 1, "--agents exceeds")]
    public void UnsupportedScenarioAndImpossiblePopulationFailBeforeOutput(string option, string value, int code, string error)
    {
        using var files = new SweepCliTests.SweepFiles();
        var result = SweepCliTests.Run("--engine", "firefly", option, value, "--ticks", "1", "--out", files.Output);

        Assert.Equal(code, result.Code);
        Assert.Contains(error, result.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(files.Output));
    }

    [Fact]
    public void AdapterPublishesChangedPhaseStateWhilePhysicalKinematicsStayFixed()
    {
        Assert.Equal(new[] { "population", "firefly.coherence" },
            new RunRequest("firefly", 1, 4, new FireflySettings(), null).RequestedMetrics);
        RunRequest request = RunRequest.Resolve(RunOptions.Parse(
            ["--engine", "firefly", "--seed", "9", "--agents", "16", "--ticks", "32"]));
        Assert.Equal(new[] { "population", "firefly.coherence" }, request.RequestedMetrics);
        var simulation = new PreparedSimulation(request);
        var adapter = Assert.IsType<FireflySimulation>(simulation.Simulation);
        float[] initialPhases = adapter.World.Agents.ToArray().Select(agent => agent.Phase).ToArray();
        string motionHash = simulation.KinematicHash();
        var phaseHash = simulation.StateHash();
        Assert.NotNull(phaseHash);
        for (int tick = 0; tick < 32; tick++) simulation.Step();

        Assert.NotEqual(initialPhases, adapter.World.Agents.ToArray().Select(agent => agent.Phase).ToArray());
        Assert.Equal(motionHash, simulation.KinematicHash());
        Assert.NotEqual(phaseHash, simulation.StateHash());
        Assert.Equal(adapter.World.StateHash(), simulation.StateHash().GetValueOrDefault());
        var capture = new KinematicSample[16];
        Assert.Equal(16, simulation.Capture(capture));
        Assert.All(capture, sample => { Assert.Equal(0f, sample.Vx); Assert.Equal(0f, sample.Vy); });
        Assert.Throws<ArgumentException>(() => simulation.Capture(new KinematicSample[15]));
    }

    [Fact]
    public void FrequencyChangesCannotPassReplayUsingOnlyStationaryKinematicsAndPopulation()
    {
        using var files = new SweepCliTests.SweepFiles();
        string[] arguments = ["--engine", "firefly", "--seed", "7", "--ticks", "16", "--agents", "16",
            "--metrics", "population", "--out", files.Output];
        string original = Succeed(arguments);
        File.WriteAllText(files.Base, """{"MeanFrequency":2.4}""");
        string changed = Succeed([.. arguments, "--config", files.Base]);
        RunManifest before = Read(original);
        RunManifest after = Read(changed);
        Assert.Equal(before.FinalKinematicHash, after.FinalKinematicHash);
        Assert.Equal(before.MetricsSha256, after.MetricsSha256);
        Assert.Equal(File.ReadAllBytes(Path.Combine(original, "metrics.csv")),
            File.ReadAllBytes(Path.Combine(changed, "metrics.csv")));
        Assert.NotEqual(before.FinalModelStateHash, after.FinalModelStateHash);

        JsonObject manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(original, "manifest.json")))!.AsObject();
        manifest["resolvedConfig"]!["MeanFrequency"] = 2.4;
        string manifestPath = Path.Combine(files.Root, "changed-manifest.json");
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        string failedOutput = Path.Combine(files.Root, "failed-replay");
        var replay = SweepCliTests.Run("--manifest", manifestPath, "--out", failedOutput);

        Assert.Equal(1, replay.Code);
        Assert.Contains("model-state hash verification failed", replay.Error, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(failedOutput));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void FireflyRefusesSchemasWithoutARequiredPhaseStateFingerprint(int schema)
    {
        using var files = new SweepCliTests.SweepFiles();
        string run = Succeed("--engine", "firefly", "--ticks", "0", "--agents", "4", "--out", files.Output);
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(run, "manifest.json")))!.AsObject();
        manifest["schemaVersion"] = schema;
        manifest.Remove("modelStateProfile");
        manifest.Remove("finalModelStateHash");
        if (schema == 1)
        {
            manifest.Remove("metricContractVersion");
            manifest.Remove("metricsDeterministic");
            manifest["requestedMetrics"] = new JsonArray("population", "meanSpeed", "polarization");
        }
        string path = Path.Combine(files.Root, "old-schema.json");
        File.WriteAllText(path, manifest.ToJsonString());
        string failedOutput = Path.Combine(files.Root, "rejected-replay");

        var replay = SweepCliTests.Run("--manifest", path, "--out", failedOutput);

        Assert.Equal(1, replay.Code);
        Assert.Contains("firefly manifests require schema 3", replay.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(failedOutput));
    }

    [Fact]
    public void DiscoveryDescribesTypedPhaseStateNativeDefaultsAndTheSupportedReplayProfile()
    {
        var result = SweepCliTests.Run("--engine", "firefly", "--describe-engine", "--json");
        Assert.True(result.Code == 0, result.Error);
        using JsonDocument json = JsonDocument.Parse(result.Output);
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        JsonElement engine = json.RootElement.GetProperty("engine");
        Assert.Equal("firefly", engine.GetProperty("name").GetString());
        Assert.Equal(new[] { "position", "phase", "naturalFrequency" }, engine.GetProperty("state").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(new[] { "population", "firefly.coherence" }, engine.GetProperty("defaultMetrics").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(3, engine.GetProperty("minimumManifestSchema").GetInt32());
        Assert.Equal("firefly-uniform-v1", engine.GetProperty("spawnModel").GetString());
        Assert.Equal(FireflyWorld.ModelStateProfile, engine.GetProperty("modelStateProfile").GetString());
        Assert.False(engine.GetProperty("acceptsScenario").GetBoolean());
        Assert.False(engine.GetProperty("interactiveRenderer").GetBoolean());
        Assert.False(engine.GetProperty("checkpointRestore").GetBoolean());
        Assert.Equal("docs/FIREFLY.md", engine.GetProperty("documentation").GetString());
        using JsonDocument defaults = JsonDocument.Parse(new FireflySettings().ToJson());
        Assert.Equal(JsonSerializer.Serialize(defaults.RootElement), JsonSerializer.Serialize(engine.GetProperty("defaultConfiguration")));
        string[] channels = engine.GetProperty("metrics").EnumerateArray().Select(value => value.GetProperty("name").GetString()!).ToArray();
        Assert.Contains("firefly.coherence", channels);
        Assert.Contains("firefly.meanPhase", channels);
        Assert.Contains("firefly.meanNaturalFrequency", channels);
        Assert.DoesNotContain(channels, name => name.StartsWith("aco.", StringComparison.Ordinal) || name.StartsWith("boids.", StringComparison.Ordinal));
    }

    [Fact]
    public void CouplingSweepHasByteIdenticalSummariesAndNativeReplayableCells()
    {
        using var files = new SweepCliTests.SweepFiles();
        File.WriteAllText(files.Base, """{"FrequencySpread":0.2,"FixedDeltaTime":0.02}""");
        string sweep = Path.Combine(files.Root, "spec", "firefly-sweep.json");
        File.WriteAllText(sweep, """
            {"base":"base.json","engine":"firefly","agents":16,"seeds":[3,1],
             "grid":{"Coupling":[0,3]},"ticks":32,
             "metrics":["firefly.coherence","firefly.meanNaturalFrequency"]}
            """);
        string first = Succeed("--sweep", sweep, "--out", files.Output);
        string second = Succeed("--sweep", sweep, "--out", files.Output);
        Assert.Equal(File.ReadAllBytes(Path.Combine(first, "summary.csv")),
            File.ReadAllBytes(Path.Combine(second, "summary.csv")));
        string[] rows = File.ReadAllLines(Path.Combine(first, "summary.csv"));
        Assert.Equal(5, rows.Length);
        string[] header = rows[0].Split(',');
        int coherence = Array.IndexOf(header, "firefly.coherence.terminal");
        int samples = Array.IndexOf(header, "firefly.coherence.samples");
        int frequencySamples = Array.IndexOf(header, "firefly.meanNaturalFrequency.samples");
        Assert.True(coherence >= 0 && samples >= 0 && frequencySamples >= 0);
        foreach (string row in rows.Skip(1))
        {
            string[] cells = row.Split(',');
            Assert.InRange(double.Parse(cells[coherence], CultureInfo.InvariantCulture), 0d, 1d);
            Assert.Equal("33", cells[samples]);
            Assert.Equal("33", cells[frequencySamples]);
            RunManifest manifest = Read(Path.Combine(first, cells[0]));
            Assert.Equal(3, manifest.SchemaVersion);
            Assert.Equal(FireflyWorld.ModelStateProfile, manifest.ModelStateProfile);
            Assert.Equal(new[] { "firefly.coherence", "firefly.meanNaturalFrequency" }, manifest.RequestedMetrics);
            Assert.Equal(manifest.FinalModelStateHash, Read(Path.Combine(second, cells[0])).FinalModelStateHash);
        }

        string cellDirectory = Path.Combine(first, rows[1].Split(',')[0]);
        string replay = Succeed("--manifest", Path.Combine(cellDirectory, "manifest.json"), "--out", files.Output);
        Assert.Equal(Read(cellDirectory).FinalModelStateHash, Read(replay).FinalModelStateHash);
        Assert.Equal(File.ReadAllBytes(Path.Combine(cellDirectory, "metrics.csv")),
            File.ReadAllBytes(Path.Combine(replay, "metrics.csv")));
    }

    [Fact]
    public void VisualObservationChangesNeitherMetricsNorModelState()
    {
        using var files = new SweepCliTests.SweepFiles();
        string[] arguments = ["--engine", "firefly", "--seed", "7", "--ticks", "16", "--agents", "8",
            "--metrics", "population,firefly.coherence,firefly.meanPhase", "--out", files.Output];
        string plain = Succeed(arguments);
        string visual = Succeed([.. arguments, "--visual"]);

        Assert.Equal(File.ReadAllBytes(Path.Combine(plain, "metrics.csv")),
            File.ReadAllBytes(Path.Combine(visual, "metrics.csv")));
        Assert.Equal(Read(plain).FinalKinematicHash, Read(visual).FinalKinematicHash);
        Assert.Equal(Read(plain).FinalModelStateHash, Read(visual).FinalModelStateHash);
        Assert.True(File.Exists(Path.Combine(visual, "filmstrip.png")));
        Assert.True(File.Exists(Path.Combine(visual, "report.html")));
        string report = File.ReadAllText(Path.Combine(visual, "report.html"));
        Assert.Contains("Colour shows oscillator phase", report, StringComparison.Ordinal);
        Assert.Contains("stationary positions", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Colour is heading angle", report, StringComparison.Ordinal);
        Assert.Contains("Model state profile", report, StringComparison.Ordinal);
        Assert.Contains(FireflyWorld.ModelStateProfile, report, StringComparison.Ordinal);
        Assert.Contains(Read(visual).FinalModelStateHash!, report, StringComparison.Ordinal);
    }

    private static string Succeed(params string[] arguments)
    {
        var result = SweepCliTests.Run(arguments);
        Assert.True(result.Code == 0, result.Error);
        return result.Output.Trim();
    }

    private static RunManifest Read(string directory) => RunManifest.Read(Path.Combine(directory, "manifest.json"));
}
