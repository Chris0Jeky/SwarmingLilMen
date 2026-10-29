using SwarmSim.Parity;

namespace SwarmSim.Tests;

public sealed class ParityCommandTests
{
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
