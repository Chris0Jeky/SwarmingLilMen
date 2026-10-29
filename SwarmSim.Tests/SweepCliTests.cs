using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CliProgram = SwarmSim.Cli.Program;

namespace SwarmSim.Tests;

public sealed class SweepCliTests
{
    [Theory]
    [InlineData("canonical")]
    [InlineData("legacy")]
    public void ThreeSeedsByFourValuesRepeatSummaryAndReplayACell(string engine)
    {
        using var files = new SweepFiles(engine);
        string sweep = files.WriteSweep();
        var first = Run("--sweep", sweep, "--out", files.Output);
        Assert.True(first.Code == 0, first.Error);
        string original = first.Output.Trim();
        Assert.True(Directory.Exists(original));
        string[] cells = Directory.GetDirectories(original).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(12, cells.Length);
        Assert.Equal(Enumerable.Range(0, 12).Select(index => $"cell-{index:D6}"), cells.Select(Path.GetFileName));
        Assert.Equal("summary.csv", Path.GetFileName(Assert.Single(Directory.GetFiles(original))));

        byte[] summary = File.ReadAllBytes(Path.Combine(original, "summary.csv"));
        Assert.False(summary.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
        Assert.DoesNotContain((byte)'\r', summary);
        string[] rows = File.ReadAllLines(Path.Combine(original, "summary.csv"));
        Assert.Equal(13, rows.Length);
        Assert.Equal("cellId,engine,seed,ticks,agents,SeparationWeight,meanSpeed.terminal,meanSpeed.mean,meanSpeed.samples,polarization.terminal,polarization.mean,polarization.samples", rows[0]);
        uint[] seeds = [13, 7, 11];
        for (int index = 0; index < cells.Length; index++)
        {
            string[] row = rows[index + 1].Split(',');
            Assert.Equal($"cell-{index:D6}", row[0]);
            Assert.Equal(engine, row[1]);
            Assert.Equal(seeds[index / 4].ToString(CultureInfo.InvariantCulture), row[2]);
            Assert.Equal("8", row[3]);
            Assert.Equal("12", row[4]);
            Assert.Equal((index % 4).ToString(CultureInfo.InvariantCulture), row[5]);
            Assert.Equal("9", row[8]);
            Assert.Equal("9", row[11]);
            Assert.Equal(new[] { "log.txt", "manifest.json", "metrics.csv" },
                Directory.GetFiles(cells[index]).Select(Path.GetFileName).Order(StringComparer.Ordinal));
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(cells[index], "manifest.json")));
            Assert.Equal(2, manifest.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.True(manifest.RootElement.GetProperty("metricsDeterministic").GetBoolean());
            Assert.Equal(seeds[index / 4], manifest.RootElement.GetProperty("seed").GetUInt32());
            Assert.Equal(index % 4, manifest.RootElement.GetProperty("resolvedConfig").GetProperty("SeparationWeight").GetSingle());
        }

        string[] metricRows = File.ReadAllLines(Path.Combine(cells[0], "metrics.csv")).Skip(1).ToArray();
        double[] speed = metricRows.Select(row => double.Parse(row.Split(',')[1], CultureInfo.InvariantCulture)).ToArray();
        string[] summaryRow = rows[1].Split(',');
        Assert.Equal(speed[^1], double.Parse(summaryRow[6], CultureInfo.InvariantCulture));
        Assert.Equal(speed.Sum() / speed.Length, double.Parse(summaryRow[7], CultureInfo.InvariantCulture));

        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");
            var repeated = Run("--sweep", sweep, "--out", files.Output);
            Assert.True(repeated.Code == 0, repeated.Error);
            Assert.NotEqual(original, repeated.Output.Trim());
            Assert.Equal(summary, File.ReadAllBytes(Path.Combine(repeated.Output.Trim(), "summary.csv")));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }

        string cellManifest = Path.Combine(cells[5], "manifest.json");
        byte[] beforeManifest = File.ReadAllBytes(cellManifest);
        var replay = Run("--manifest", cellManifest, "--out", Path.Combine(files.Root, "replays"));
        Assert.True(replay.Code == 0, replay.Error);
        Assert.Equal(File.ReadAllBytes(Path.Combine(cells[5], "metrics.csv")),
            File.ReadAllBytes(Path.Combine(replay.Output.Trim(), "metrics.csv")));
        Assert.Equal(beforeManifest, File.ReadAllBytes(cellManifest));
        Assert.Equal(2, Directory.GetDirectories(files.Output).Length);
    }

    [Theory]
    [InlineData("{\"typo\":1}")]
    [InlineData("{\"seeds\":[]}")]
    [InlineData("{\"seeds\":[-1]}")]
    [InlineData("{\"seeds\":[2147483648]}")]
    [InlineData("{\"seeds\":[1.5]}")]
    [InlineData("{\"seeds\":[7,7]}")]
    [InlineData("{\"grid\":{\"SeparationWeigth\":[1]}}")]
    [InlineData("{\"grid\":{\"Seed\":[1]}}")]
    [InlineData("{\"grid\":{\"SenseRadius\":[]}}")]
    [InlineData("{\"grid\":{\"SenseRadius\":[12,0]}}")]
    [InlineData("{\"grid\":{\"SenseRadius\":[\"12\"]}}")]
    [InlineData("{\"grid\":{\"SenseRadius\":[true]}}")]
    [InlineData("{\"grid\":null}")]
    [InlineData("{\"grid\":{\"SenseRadius\":12}}")]
    [InlineData("{\"grid\":{\"SenseRadius\":[1e400]}}")]
    [InlineData("{\"grid\":{\"SenseRadius\":[1e100]}}")]
    [InlineData("{\"grid\":{\"InitialCapacity\":[2147483648]}}")]
    [InlineData("{\"grid\":{\"InitialCapacity\":[1]}}")]
    [InlineData("{\"ticks\":-1}")]
    [InlineData("{\"ticks\":2147483648}")]
    [InlineData("{\"agents\":0}")]
    [InlineData("{\"metrics\":[]}")]
    [InlineData("{\"metrics\":[\"unknown\"]}")]
    [InlineData("{\"metrics\":[\"meanSpeed\",\"meanSpeed\"]}")]
    [InlineData("{\"metrics\":[\"runtime.wallMilliseconds\"]}")]
    [InlineData("{\"metrics\":[\"runtime.allocatedBytes\"]}")]
    [InlineData("{\"engine\":\"unsupported\"}")]
    [InlineData("{\"base\":\"missing.json\"}")]
    public void InvalidSweepFailsBeforeCreatingOutput(string changes)
    {
        using var files = new SweepFiles();
        string sweep = files.WriteSweep(JsonNode.Parse(changes)!.AsObject());

        var result = Run("--sweep", sweep, "--out", files.Output);

        Assert.Equal(1, result.Code);
        Assert.NotEmpty(result.Error);
        Assert.Empty(result.Output);
        Assert.False(Directory.Exists(files.Output));
    }

    [Theory]
    [InlineData("base")]
    [InlineData("seeds")]
    [InlineData("grid")]
    [InlineData("ticks")]
    [InlineData("metrics")]
    public void RequiredSweepFieldsCannotBeOmitted(string missing)
    {
        using var files = new SweepFiles();
        JsonObject definition = files.Definition();
        definition.Remove(missing);
        string sweep = files.WriteDefinition(definition);

        var result = Run("--sweep", sweep, "--out", files.Output);

        Assert.Equal(1, result.Code);
        Assert.False(Directory.Exists(files.Output));
    }

    [Theory]
    [InlineData("{\"SenseRadius\":12,\"senseradius\":10}", true)]
    [InlineData("{\"TargetSpeed\":3,\"Typo\":10}", true)]
    [InlineData("{\"SenseRadius\":0}", true)]
    [InlineData("{\"base\":\"base.json\",\"seeds\":[7],\"grid\":{},\"ticks\":1,\"Ticks\":2,\"metrics\":[\"population\"]}", false)]
    [InlineData("{\"base\":\"base.json\",\"seeds\":[7],\"grid\":{\"SenseRadius\":[12],\"senseradius\":[10]},\"ticks\":1,\"metrics\":[\"population\"]}", false)]
    public void StrictJsonRejectsDuplicatesAndInvalidBaseFiles(string json, bool isBase)
    {
        using var files = new SweepFiles();
        string sweep = files.WriteSweep();
        File.WriteAllText(isBase ? files.Base : sweep, json);

        var result = Run("--sweep", sweep, "--out", files.Output);

        Assert.Equal(1, result.Code);
        Assert.False(Directory.Exists(files.Output));
    }

    [Fact]
    public void ExcessiveCartesianProductIsRejectedBeforeExpansionOrOutput()
    {
        using var files = new SweepFiles("legacy");
        var axes = new JsonObject();
        using JsonDocument defaults = JsonDocument.Parse(new SwarmSim.Core.SimConfig().ToJson());
        foreach (JsonProperty property in defaults.RootElement.EnumerateObject()
                     .Where(property => property.Name != "Seed" && property.Value.ValueKind == JsonValueKind.Number).Take(30))
            axes[property.Name] = new JsonArray(Enumerable.Range(1, 10).Select(value => JsonValue.Create(value)!).ToArray<JsonNode?>());
        string sweep = files.WriteSweep(new JsonObject { ["grid"] = axes });

        var result = Run("--sweep", sweep, "--out", files.Output);

        Assert.Equal(1, result.Code);
        Assert.Contains("10000", result.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(files.Output));
    }

    [Theory]
    [InlineData("--seed", "7")]
    [InlineData("--ticks", "1")]
    [InlineData("--engine", "legacy")]
    [InlineData("--agents", "8")]
    [InlineData("--metrics", "population")]
    [InlineData("--config", "base.json")]
    [InlineData("--scenario", "balanced")]
    [InlineData("--manifest", "manifest.json")]
    public void SweepCannotBeMixedWithSingleRunOverrides(string option, string value)
    {
        using var files = new SweepFiles();
        string sweep = files.WriteSweep();

        var result = Run("--sweep", sweep, option, value, "--out", files.Output);

        Assert.Equal(2, result.Code);
        Assert.False(Directory.Exists(files.Output));
    }

    internal static (int Code, string Output, string Error) Run(params string[] arguments)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        int code = CliProgram.Execute(arguments, output, error);
        return (code, output.ToString(), error.ToString());
    }

    internal sealed class SweepFiles : IDisposable
    {
        private readonly string _engine;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "swarm-sweep-test-" + Guid.NewGuid().ToString("N"));
        internal string Base => Path.Combine(Root, "spec", "base.json");
        internal string Output => Path.Combine(Root, "runs");

        internal SweepFiles(string engine = "canonical")
        {
            _engine = engine;
            Directory.CreateDirectory(Path.GetDirectoryName(Base)!);
            var config = new JsonObject
            {
                ["InitialCapacity"] = 16, ["WorldWidth"] = 96, ["WorldHeight"] = 64,
                ["FixedDeltaTime"] = 0.05, ["SenseRadius"] = 12, ["SeparationRadius"] = 5,
                ["MaxForce"] = 0.2, ["WanderStrength"] = 0,
                [engine == "canonical" ? "TargetSpeed" : "MaxSpeed"] = 3
            };
            if (engine == "legacy") config["GridCellSize"] = 12;
            File.WriteAllText(Base, config.ToJsonString());
        }

        internal JsonObject Definition() => new()
        {
            ["base"] = "base.json", ["engine"] = _engine, ["agents"] = 12, ["ticks"] = 8,
            ["seeds"] = new JsonArray(13, 7, 11),
            ["grid"] = new JsonObject { ["SeparationWeight"] = new JsonArray(0, 1, 2, 3) },
            ["metrics"] = new JsonArray("meanSpeed", "polarization")
        };

        internal string WriteSweep(JsonObject? changes = null)
        {
            JsonObject definition = Definition();
            if (changes is not null)
                foreach ((string key, JsonNode? value) in changes) definition[key] = value?.DeepClone();
            return WriteDefinition(definition);
        }

        internal string WriteDefinition(JsonObject definition)
        {
            string path = Path.Combine(Root, "spec", "sweep.json");
            File.WriteAllText(path, definition.ToJsonString());
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
