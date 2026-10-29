using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Configuration;
using CliProgram = SwarmSim.Cli.Program;

namespace SwarmSim.Tests;

public sealed class CanonicalCompositionConfigurationTests
{
    [Fact]
    public void DefaultModeIsOmittedAndMissingModeAlwaysMeansEnhanced()
    {
        string json = new CanonicalWorldSettings().ToJson();
        Assert.DoesNotContain("Mode", json, StringComparison.Ordinal);
        Assert.Equal(CanonicalMode.Enhanced, CanonicalWorldSettings.FromJson(json).Mode);
        Assert.Equal(CanonicalMode.Enhanced, ConfigurationResolver.ResolveCanonicalSettings().Mode);
    }

    [Fact]
    public void PublicClassicFactoryAndSparseFileReachTheRealThreeRuleComposition()
    {
        CanonicalWorldSettings factory = CanonicalWorldSettings.ClassicReynolds();
        Assert.Equal(CanonicalMode.ClassicReynolds, factory.Mode);
        Assert.Equal(360f, factory.FieldOfView);
        Assert.Contains("ClassicReynolds", factory.ToJson(), StringComparison.Ordinal);
        using var files = new Files();
        string path = Path.Combine(files.Root, "classic.json");
        File.WriteAllText(path, """{"Mode":"ClassicReynolds","FieldOfView":360}""");
        CanonicalWorldSettings settings = ConfigurationResolver.ResolveCanonicalSettings(filePath: path);
        var world = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        Assert.Equal(new[] { "separation", "alignment", "cohesion" }, world.Policies.Select(policy => policy.Id));
    }

    [Theory]
    [InlineData("{\"Mode\":\"unknown\",\"FieldOfView\":360}")]
    [InlineData("{\"Mode\":\"ClassicReynolds\"}")]
    [InlineData("{\"Mode\":999,\"FieldOfView\":360}")]
    public void InvalidModeOrClassicFieldOfViewFailsBeforeOutput(string json)
    {
        using var files = new Files();
        string config = Path.Combine(files.Root, "invalid.json");
        File.WriteAllText(config, json);
        string output = Path.Combine(files.Root, "runs");
        Assert.Equal(1, Run("--config", config, "--agents", "4", "--ticks", "2", "--out", output));
        Assert.False(Directory.Exists(output));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public void ManifestReplayPreservesCompleteCsvAndModeWithoutRequiringModeInOldManifests(bool classic, int schemaVersion)
    {
        using var files = new Files();
        string config = Path.Combine(files.Root, "config.json");
        File.WriteAllText(config, classic ? """{"Mode":"ClassicReynolds","FieldOfView":360}""" : "{}");
        string output = Path.Combine(files.Root, "original");
        Assert.Equal(0, Run("--config", config, "--agents", "12", "--ticks", "12", "--seed", "7", "--out", output));
        string original = Assert.Single(Directory.GetDirectories(output));
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(original, "manifest.json")))!.AsObject();
        Assert.Equal(classic, manifest["resolvedConfig"]!.AsObject().ContainsKey("Mode"));
        if (schemaVersion == 1)
        {
            manifest["schemaVersion"] = 1;
            manifest.Remove("metricContractVersion");
            manifest.Remove("metricsDeterministic");
        }
        string source = Path.Combine(files.Root, "source.json");
        File.WriteAllText(source, manifest.ToJsonString());
        string replayOutput = Path.Combine(files.Root, "replay");
        Assert.Equal(0, Run("--manifest", source, "--out", replayOutput));
        string replay = Assert.Single(Directory.GetDirectories(replayOutput));
        Assert.Equal(File.ReadAllBytes(Path.Combine(original, "metrics.csv")), File.ReadAllBytes(Path.Combine(replay, "metrics.csv")));
        using JsonDocument actual = JsonDocument.Parse(File.ReadAllText(Path.Combine(replay, "manifest.json")));
        Assert.Equal(manifest["finalKinematicHash"]!.GetValue<string>(), actual.RootElement.GetProperty("finalKinematicHash").GetString());

        // The optional compatibility exception is limited to Mode. Other complete fields remain required.
        manifest["resolvedConfig"]!.AsObject().Remove("WanderRate");
        File.WriteAllText(source, manifest.ToJsonString());
        string rejectedOutput = Path.Combine(files.Root, "rejected");
        Assert.Equal(1, Run("--manifest", source, "--out", rejectedOutput));
        Assert.False(Directory.Exists(rejectedOutput));
    }

    private static int Run(params string[] args)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        return CliProgram.Execute(args, output, error);
    }

    private sealed class Files : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "swarm-composition-" + Guid.NewGuid().ToString("N"));
        public Files() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, true);
    }
}
