using System.Text.Json;
using SwarmSim.Parity;

namespace SwarmSim.Tests;

public sealed class ParityCommandTests
{
    [Fact]
    public void FilteredRunRecordsOperationalTimingsWithoutChangingScientificArtifacts()
    {
        string root = Path.Combine(Path.GetTempPath(), "parity-timing-" + Guid.NewGuid().ToString("N"));
        string outputDirectory = Path.Combine(root, "command"), expectedDirectory = Path.Combine(root, "direct");
        try
        {
            var recipe = ScenarioRecipe.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "scenarios", "S1_TWO_BOID_HEAD_ON.json")));
            ParityRunResult expected = ParityRunner.Run(recipe, 7, "classic", expectedDirectory);
            using var output = new StringWriter();
            using var error = new StringWriter();
            int code = ParityCommand.Run(new[] { "run", "--out", outputDirectory,
                "--scenario", "S1_TWO_BOID_HEAD_ON", "--seed", "7", "--model", "classic" }, output, error);

            Assert.Equal(0, code);
            Assert.Equal(string.Empty, error.ToString());
            string timingPath = Path.Combine(outputDirectory, "timing.json");
            Assert.True(File.Exists(timingPath), "A completed command must retain its separate operational timing receipt.");
            using var timingDocument = JsonDocument.Parse(File.ReadAllText(timingPath));
            JsonElement timing = timingDocument.RootElement;
            Assert.Equal(1, timing.GetProperty("Version").GetInt32());
            Assert.Equal(1, timing.GetProperty("PrimaryRuns").GetInt32());
            Assert.Equal(0, timing.GetProperty("RepeatRuns").GetInt32());
            Assert.Equal(0, timing.GetProperty("NaiveIndexRuns").GetInt32());
            double primarySeconds = timing.GetProperty("PrimarySeconds").GetDouble();
            double matrixSeconds = timing.GetProperty("MatrixSeconds").GetDouble();
            Assert.True(double.IsFinite(primarySeconds) && primarySeconds > 0);
            Assert.True(double.IsFinite(matrixSeconds) && matrixSeconds >= primarySeconds);
            Assert.Equal(0, timing.GetProperty("RepeatSeconds").GetDouble());
            Assert.Equal(0, timing.GetProperty("NaiveIndexSeconds").GetDouble());
            Assert.False(string.IsNullOrWhiteSpace(timing.GetProperty("MatrixScope").GetString()));
            Assert.Contains("verification work", output.ToString());

            using var summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDirectory, "summary.json")));
            Assert.Equal(JsonSerializer.Serialize(new[] { expected }), JsonSerializer.Serialize(summary.RootElement));
            string actualDirectory = Path.Combine(outputDirectory, "S1_TWO_BOID_HEAD_ON", "7", "classic");
            foreach (string file in new[] { "metrics.csv", "requested-initial.csv", "initial.csv", "configuration.json" })
                Assert.Equal(File.ReadAllBytes(Path.Combine(expectedDirectory, file)),
                    File.ReadAllBytes(Path.Combine(actualDirectory, file)));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("--seed", "not-a-seed")]
    [InlineData("--seed", "-1")]
    [InlineData("--model", "missing-model")]
    [InlineData("--scenario", "missing-scenario")]
    [InlineData("--unknown", "anything")]
    public void InvalidCommandDoesNotCreateOutput(string flag, string value)
    {
        string path = Path.Combine(Path.GetTempPath(), "parity-invalid-" + Guid.NewGuid().ToString("N"));
        using var error = new StringWriter();
        int code = ParityCommand.Run(new[] { "run", "--out", path, flag, value }, TextWriter.Null, error);
        Assert.Equal(2, code);
        Assert.False(Directory.Exists(path));
        Assert.False(string.IsNullOrEmpty(error.ToString()));
    }

    [Fact]
    public void MissingBaselineIsNotAutomaticallyRecorded()
    {
        string root = Path.Combine(Path.GetTempPath(), "parity-missing-" + Guid.NewGuid().ToString("N"));
        int code = ParityCommand.Run(new[] { "verify", "--out", root, "--baseline", root + ".json" }, TextWriter.Null, TextWriter.Null);
        Assert.Equal(2, code);
        Assert.False(Directory.Exists(root));
        Assert.False(File.Exists(root + ".json"));
    }

    [Fact]
    public void RecordingCannotReplaceAnExistingBaseline()
    {
        string root = Path.Combine(Path.GetTempPath(), "parity-existing-" + Guid.NewGuid().ToString("N"));
        string baseline = root + ".json";
        File.WriteAllText(baseline, "keep this evidence");
        try
        {
            int code = ParityCommand.Run(new[] { "record", "--out", root, "--baseline", baseline }, TextWriter.Null, TextWriter.Null);
            Assert.Equal(2, code);
            Assert.False(Directory.Exists(root));
            Assert.Equal("keep this evidence", File.ReadAllText(baseline));
        }
        finally { File.Delete(baseline); }
    }

    [Fact]
    public void PartialMatrixCannotBePassedOffAsVerification()
    {
        string root = Path.Combine(Path.GetTempPath(), "parity-partial-" + Guid.NewGuid().ToString("N"));
        int code = ParityCommand.Run(new[] { "verify", "--out", root, "--baseline", "missing.json", "--seed", "7" }, TextWriter.Null, TextWriter.Null);
        Assert.Equal(2, code);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void DistanceOnlyRejectionDoesNotCountAsTheAlignmentPolarizationWitness()
    {
        using var output = new StringWriter();
        int code = ParityCommand.WriteVerificationResult(new[] { "S4_UNIFORM_RANDOM_512 nearestNeighborMean: difference exceeds band." },
            alignmentDisabled: true, output, TextWriter.Null);
        Assert.Equal(3, code);
        Assert.Contains("NOT established", output.ToString());
    }

    [Fact]
    public void PolarizationRejectionIsReportedAsTheExpectedMutationFailure()
    {
        int code = ParityCommand.WriteVerificationResult(new[] { "S4_UNIFORM_RANDOM_512 polarization: difference exceeds band." },
            alignmentDisabled: true, TextWriter.Null, TextWriter.Null);
        Assert.Equal(1, code);
    }
}
