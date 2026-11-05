using System.Text.Json.Nodes;
using SwarmSim.Cli;

namespace SwarmSim.Tests;

public sealed class ModelStateManifestTests
{
    [Theory]
    [InlineData("aco", "aco-state-v1")]
    [InlineData("vicsek", "vicsek-state-v1")]
    public void NativeRunsRecordAndReplayTheVersionedModelProfile(string engine, string profile)
    {
        using var files = new SweepCliTests.SweepFiles();
        string original = CreateRun(files, engine, ticks: 3);
        string manifestPath = Path.Combine(original, "manifest.json");
        RunManifest manifest = RunManifest.Read(manifestPath);
        Assert.Equal(3, manifest.SchemaVersion);
        Assert.Equal(1, manifest.KinematicHashVersion);
        Assert.Equal(profile, manifest.ModelStateProfile);
        Assert.NotNull(manifest.FinalModelStateHash);
        Assert.Equal(64, manifest.FinalModelStateHash.Length);

        var replay = SweepCliTests.Run("--manifest", manifestPath, "--out", files.Output);

        Assert.True(replay.Code == 0, replay.Error);
        RunManifest repeated = RunManifest.Read(Path.Combine(replay.Output.Trim(), "manifest.json"));
        Assert.Equal(manifest.FinalModelStateHash, repeated.FinalModelStateHash);
        Assert.Equal(manifest.FinalKinematicHash, repeated.FinalKinematicHash);
        Assert.Equal(manifest.MetricsSha256, repeated.MetricsSha256);
        Assert.Equal(File.ReadAllBytes(Path.Combine(original, "metrics.csv")),
            File.ReadAllBytes(Path.Combine(replay.Output.Trim(), "metrics.csv")));
    }

    [Theory]
    [InlineData("aco")]
    [InlineData("vicsek")]
    public void SchemaTwoNativeReplayKeepsOriginalChecksAndAddsTheNewObservation(string engine)
    {
        using var files = new SweepCliTests.SweepFiles();
        string original = CreateRun(files, engine, ticks: 3);
        JsonObject legacy = ReadManifest(original);
        string expectedModelHash = legacy["finalModelStateHash"]!.GetValue<string>();
        legacy["schemaVersion"] = 2;
        legacy.Remove("modelStateProfile");
        legacy.Remove("finalModelStateHash");
        string path = WriteManifest(files, legacy);

        var result = SweepCliTests.Run("--manifest", path, "--out", files.Output);

        Assert.True(result.Code == 0, result.Error);
        RunManifest replay = RunManifest.Read(Path.Combine(result.Output.Trim(), "manifest.json"));
        Assert.Equal(3, replay.SchemaVersion);
        Assert.Equal(legacy["finalKinematicHash"]!.GetValue<string>(), replay.FinalKinematicHash);
        Assert.Equal(legacy["metricsSha256"]!.GetValue<string>(), replay.MetricsSha256);
        Assert.Equal(expectedModelHash, replay.FinalModelStateHash);
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("canonical")]
    public void EnginesWithoutADeclaredProfileStaySchemaTwoAndRefuseSchemaThree(string engine)
    {
        using var files = new SweepCliTests.SweepFiles();
        JsonObject manifest = ReadManifest(CreateRun(files, engine));
        Assert.Equal(2, manifest["schemaVersion"]!.GetValue<int>());
        Assert.False(manifest.ContainsKey("modelStateProfile"));
        Assert.False(manifest.ContainsKey("finalModelStateHash"));
        manifest["schemaVersion"] = 3;
        manifest["modelStateProfile"] = "aco-state-v1";
        manifest["finalModelStateHash"] = new string('0', 64);

        AssertRejectedBeforeOutput(files, manifest, "modelStateProfile");
    }

    [Theory]
    [InlineData("modelStateProfile", "missing")]
    [InlineData("modelStateProfile", "null")]
    [InlineData("modelStateProfile", "empty")]
    [InlineData("modelStateProfile", "wrong-version")]
    [InlineData("modelStateProfile", "wrong-engine")]
    [InlineData("finalModelStateHash", "missing")]
    [InlineData("finalModelStateHash", "null")]
    [InlineData("finalModelStateHash", "empty")]
    [InlineData("finalModelStateHash", "non-hex")]
    [InlineData("finalModelStateHash", "short")]
    public void MalformedSchemaThreeMetadataIsRejectedBeforeOutput(string field, string mutation)
    {
        using var files = new SweepCliTests.SweepFiles();
        JsonObject manifest = ReadManifest(CreateRun(files, "aco"));
        switch (mutation)
        {
            case "missing": manifest.Remove(field); break;
            case "null": manifest[field] = null; break;
            case "empty": manifest[field] = ""; break;
            case "wrong-version": manifest[field] = "aco-state-v2"; break;
            case "wrong-engine": manifest[field] = "vicsek-state-v1"; break;
            case "non-hex": manifest[field] = new string('G', 64); break;
            case "short": manifest[field] = new string('0', 63); break;
        }
        // An unsupported profile must fail even before attempting a huge requested replay.
        manifest["ticks"] = int.MaxValue;

        AssertRejectedBeforeOutput(files, manifest, field);
    }

    [Theory]
    [InlineData(1, "modelStateProfile")]
    [InlineData(1, "finalModelStateHash")]
    [InlineData(2, "modelStateProfile")]
    [InlineData(2, "finalModelStateHash")]
    public void OldSchemasRejectEvenExplicitNullModelMetadata(int schema, string field)
    {
        using var files = new SweepCliTests.SweepFiles();
        JsonObject manifest = ReadManifest(CreateRun(files, "canonical"));
        manifest["schemaVersion"] = schema;
        if (schema == 1)
        {
            manifest.Remove("metricContractVersion");
            manifest.Remove("metricsDeterministic");
        }
        manifest[field] = null;

        AssertRejectedBeforeOutput(files, manifest, "do not support model-state metadata");
    }

    [Fact]
    public void AValidButWrongModelHashPublishesNoCompletedRun()
    {
        using var files = new SweepCliTests.SweepFiles();
        JsonObject manifest = ReadManifest(CreateRun(files, "aco"));
        manifest["finalModelStateHash"] = new string('0', 64);
        string output = Path.Combine(files.Root, "failed-replay");

        var result = SweepCliTests.Run("--manifest", WriteManifest(files, manifest), "--out", output);

        Assert.Equal(1, result.Code);
        Assert.Contains("model-state hash verification failed", result.Error, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(output));
    }

    [Fact]
    public void AHiddenConfigurationChangeAtTickZeroFailsOnlyTheNewStateCheck()
    {
        using var files = new SweepCliTests.SweepFiles();
        JsonObject manifest = ReadManifest(CreateRun(files, "aco"));
        // No tick runs and the empty field never evaporates; original v1 and CSV still match.
        manifest["resolvedConfig"]!["Evaporation"] = 0.5;
        string output = Path.Combine(files.Root, "failed-replay");

        var result = SweepCliTests.Run("--manifest", WriteManifest(files, manifest), "--out", output);

        Assert.Equal(1, result.Code);
        Assert.Contains("model-state hash verification failed", result.Error, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(output));
    }

    private static void AssertRejectedBeforeOutput(SweepCliTests.SweepFiles files, JsonObject manifest, string errorFragment)
    {
        string output = Path.Combine(files.Root, "rejected-replay");
        var result = SweepCliTests.Run("--manifest", WriteManifest(files, manifest), "--out", output);
        Assert.Equal(1, result.Code);
        Assert.Contains(errorFragment, result.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(output));
    }

    private static string CreateRun(SweepCliTests.SweepFiles files, string engine, int ticks = 0)
    {
        var result = SweepCliTests.Run("--engine", engine, "--ticks", ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--agents", "4", "--out", files.Output);
        Assert.True(result.Code == 0, result.Error);
        return result.Output.Trim();
    }

    private static JsonObject ReadManifest(string run) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(run, "manifest.json")))!.AsObject();

    private static string WriteManifest(SweepCliTests.SweepFiles files, JsonObject manifest)
    {
        string path = Path.Combine(files.Root, "changed-manifest.json");
        File.WriteAllText(path, manifest.ToJsonString());
        return path;
    }
}
