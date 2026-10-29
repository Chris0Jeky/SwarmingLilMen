using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using System.Text.Json.Nodes;
using CliProgram = SwarmSim.Cli.Program;

namespace SwarmSim.Tests;

public sealed class HeadlessCliTests
{
    [Theory]
    [InlineData("canonical")]
    [InlineData("legacy")]
    public void RepeatAndManifestReplay_ProduceIdenticalMetricsAcrossCultures(string engine)
    {
        using var files = new TestFiles();
        string output = Path.Combine(files.Root, "runs");
        string[] args = ["--engine", engine, "--scenario", "balanced", "--seed", "7", "--ticks", "8", "--agents", "12", "--out", output];

        Assert.Equal(0, Run(args).Code);
        string original = Assert.Single(Directory.GetDirectories(output));
        byte[] expected = File.ReadAllBytes(Path.Combine(original, "metrics.csv"));
        string originalManifest = File.ReadAllText(Path.Combine(original, "manifest.json"));
        Assert.Equal(0, Run(args).Code);

        CultureInfo oldCulture = CultureInfo.CurrentCulture;
        CultureInfo oldUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");
            Assert.Equal(0, Run(args).Code);
            Assert.Equal(0, Run("--manifest", Path.Combine(original, "manifest.json"), "--out", output).Code);
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
            CultureInfo.CurrentUICulture = oldUiCulture;
        }

        string[] runs = Directory.GetDirectories(output);
        Assert.Equal(4, runs.Length);
        foreach (string run in runs)
        {
            Assert.Equal(expected, File.ReadAllBytes(Path.Combine(run, "metrics.csv")));
            Assert.Equal(new[] { "log.txt", "manifest.json", "metrics.csv" }, Directory.GetFiles(run).Select(Path.GetFileName).Order().ToArray());
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(run, "manifest.json")));
            Assert.Equal(1, manifest.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(engine, manifest.RootElement.GetProperty("engine").GetString());
            Assert.Equal(7u, manifest.RootElement.GetProperty("seed").GetUInt32());
            Assert.Equal(7u, manifest.RootElement.GetProperty("resolvedConfig").GetProperty("Seed").GetUInt32());
            Assert.Equal(64, manifest.RootElement.GetProperty("finalKinematicHash").GetString()!.Length);
            Assert.Equal(64, manifest.RootElement.GetProperty("metricsSha256").GetString()!.Length);
            Assert.True(manifest.RootElement.TryGetProperty("gitCommit", out _));
            Assert.True(manifest.RootElement.TryGetProperty("dirtyWorktree", out _));
            Assert.True(manifest.RootElement.TryGetProperty("runtime", out _));
        }
        Assert.Equal(originalManifest, File.ReadAllText(Path.Combine(original, "manifest.json")));

        string[] lines = File.ReadAllLines(Path.Combine(original, "metrics.csv"));
        Assert.Equal("tick,population,meanSpeed,polarization", lines[0]);
        Assert.Equal(10, lines.Length); // header + initial state + eight steps
        Assert.Equal("0", lines[1].Split(',')[0]);
        Assert.Equal("8", lines[^1].Split(',')[0]);
        Assert.All(lines.Skip(1), line =>
        {
            string[] values = line.Split(',');
            Assert.Equal(4, values.Length);
            Assert.Equal("12", values[1]);
            Assert.True(double.IsFinite(double.Parse(values[2], CultureInfo.InvariantCulture)));
            Assert.InRange(double.Parse(values[3], CultureInfo.InvariantCulture), 0d, 1d);
        });
    }

    [Fact]
    public void ChangingSeed_ChangesMetricsAndFinalKinematicHash()
    {
        using var files = new TestFiles();
        string first = Path.Combine(files.Root, "a");
        string second = Path.Combine(files.Root, "b");
        Assert.Equal(0, Run("--seed", "7", "--ticks", "3", "--agents", "8", "--out", first).Code);
        Assert.Equal(0, Run("--seed", "8", "--ticks", "3", "--agents", "8", "--out", second).Code);
        string a = Assert.Single(Directory.GetDirectories(first));
        string b = Assert.Single(Directory.GetDirectories(second));

        Assert.NotEqual(File.ReadAllText(Path.Combine(a, "metrics.csv")), File.ReadAllText(Path.Combine(b, "metrics.csv")));
        JsonObject manifestA = JsonNode.Parse(File.ReadAllText(Path.Combine(a, "manifest.json")))!.AsObject();
        JsonObject manifestB = JsonNode.Parse(File.ReadAllText(Path.Combine(b, "manifest.json")))!.AsObject();
        Assert.Equal("canonical", manifestA["engine"]!.GetValue<string>());
        Assert.NotEqual(manifestA["finalKinematicHash"]!.GetValue<string>(), manifestB["finalKinematicHash"]!.GetValue<string>());
    }

    public static IEnumerable<object[]> InvalidArguments()
    {
        yield return new object[] { new[] { "--unknown" } };
        yield return new object[] { new[] { "--ticks", "-1" } };
        yield return new object[] { new[] { "--ticks", "1.5" } };
        yield return new object[] { new[] { "--ticks", "2147483648" } };
        yield return new object[] { new[] { "--agents", "0" } };
        yield return new object[] { new[] { "--seed", "2147483648" } };
        yield return new object[] { new[] { "--engine", "gpu" } };
        yield return new object[] { new[] { "--seed", "1", "--seed", "2" } };
        yield return new object[] { new[] { "--config" } };
        yield return new object[] { new[] { "--manifest", "source.json", "--engine", "canonical" } };
        yield return new object[] { new[] { "--manifest", "source.json", "--seed", "1" } };
        yield return new object[] { new[] { "--manifest", "source.json", "--ticks", "2" } };
        yield return new object[] { new[] { "--manifest", "source.json", "--agents", "2" } };
        yield return new object[] { new[] { "--manifest", "source.json", "--scenario", "balanced" } };
        yield return new object[] { new[] { "--manifest", "source.json", "--config", "config.json" } };
    }

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public void BadUsage_ReturnsTwoAndCreatesNoOutput(string[] arguments)
    {
        using var files = new TestFiles();
        string output = Path.Combine(files.Root, "runs");
        var result = Run(new[] { "--out", output }.Concat(arguments).ToArray());

        Assert.Equal(2, result.Code);
        Assert.NotEmpty(result.Error);
        Assert.False(Directory.Exists(output));
    }

    [Theory]
    [InlineData("{\"MaxSpeed\":12}")]
    [InlineData("{\"TargetSpeed\":0}")]
    [InlineData("{\"InitialCapacity\":1}")]
    [InlineData("{\"TargetSpeeed\":12}")]
    public void InvalidCanonicalConfiguration_FailsBeforeOutput(string json)
    {
        using var files = new TestFiles();
        string output = Path.Combine(files.Root, "runs");
        string config = Path.Combine(files.Root, "config.json");
        File.WriteAllText(config, json);

        var result = Run("--config", config, "--ticks", "1", "--agents", "2", "--out", output);

        Assert.Equal(1, result.Code);
        Assert.NotEmpty(result.Error);
        Assert.False(Directory.Exists(output));
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("seed")]
    [InlineData("missingConfigField")]
    [InlineData("runId")]
    [InlineData("outputDirectory")]
    public void InvalidManifest_IsRejectedWithoutWritingOrChangingSource(string mutation)
    {
        using var files = new TestFiles();
        string initialOutput = Path.Combine(files.Root, "initial");
        Assert.Equal(0, Run("--seed", "7", "--ticks", "1", "--agents", "4", "--out", initialOutput).Code);
        string run = Assert.Single(Directory.GetDirectories(initialOutput));
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(run, "manifest.json")))!.AsObject();
        switch (mutation)
        {
            case "schemaVersion": manifest[mutation] = 2; break;
            case "seed": manifest[mutation] = 9; break;
            case "missingConfigField": manifest["resolvedConfig"]!.AsObject().Remove("WanderRate"); break;
            case "runId": manifest[mutation] = "../escape"; break;
            case "outputDirectory": manifest[mutation] = Path.Combine(files.Root, "escape"); break;
        }
        string source = Path.Combine(files.Root, "tampered.json");
        File.WriteAllText(source, manifest.ToJsonString());
        string before = File.ReadAllText(source);
        string output = Path.Combine(files.Root, "replay");

        var result = Run("--manifest", source, "--out", output);

        Assert.Equal(1, result.Code);
        Assert.False(Directory.Exists(output));
        Assert.False(Directory.Exists(Path.Combine(files.Root, "escape")));
        Assert.Equal(before, File.ReadAllText(source));
    }

    [Fact]
    public void ReplayHashMismatch_DoesNotPublishAnIncompleteRun()
    {
        using var files = new TestFiles();
        string initialOutput = Path.Combine(files.Root, "initial");
        Assert.Equal(0, Run("--ticks", "1", "--agents", "4", "--out", initialOutput).Code);
        string original = Assert.Single(Directory.GetDirectories(initialOutput));
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(original, "manifest.json")))!.AsObject();
        manifest["finalKinematicHash"] = new string('0', 64);
        string source = Path.Combine(files.Root, "mismatch.json");
        File.WriteAllText(source, manifest.ToJsonString());
        string output = Path.Combine(files.Root, "replay");

        Assert.Equal(1, Run("--manifest", source, "--out", output).Code);
        Assert.Empty(Directory.GetFileSystemEntries(output));
    }

    [Fact]
    public void ZeroTicks_RecordsOnlyInitialState_AndCliHasNoRendererDependency()
    {
        using var files = new TestFiles();
        string output = Path.Combine(files.Root, "runs");
        Assert.Equal(0, Run("--ticks", "0", "--agents", "1", "--out", output).Code);
        string run = Assert.Single(Directory.GetDirectories(output));

        string[] lines = File.ReadAllLines(Path.Combine(run, "metrics.csv"));
        Assert.Equal(2, lines.Length);
        string[] initial = lines[1].Split(',');
        Assert.Equal("1", initial[1]);
        Assert.Equal(1d, double.Parse(initial[2], CultureInfo.InvariantCulture), precision: 6);
        Assert.Equal(1d, double.Parse(initial[3], CultureInfo.InvariantCulture), precision: 6);
        JsonObject before = JsonNode.Parse(File.ReadAllText(Path.Combine(run, "manifest.json")))!.AsObject();
        string steppedOutput = Path.Combine(files.Root, "stepped");
        Assert.Equal(0, Run("--ticks", "1", "--agents", "1", "--out", steppedOutput).Code);
        string stepped = Assert.Single(Directory.GetDirectories(steppedOutput));
        JsonObject after = JsonNode.Parse(File.ReadAllText(Path.Combine(stepped, "manifest.json")))!.AsObject();
        Assert.NotEqual(before["finalKinematicHash"]!.GetValue<string>(), after["finalKinematicHash"]!.GetValue<string>());
        Assert.DoesNotContain(typeof(CliProgram).Assembly.GetReferencedAssemblies(), reference =>
            reference.Name!.Contains("Raylib", StringComparison.OrdinalIgnoreCase) || reference.Name == "SwarmSim.Render");
    }

    [Theory]
    [InlineData("ticks", "ticks", "999", "$.ticks")]
    [InlineData("seed", "seed", "999", "$.seed")]
    [InlineData("framework", "framework", "\"shadow runtime\"", "$.runtime.framework")]
    [InlineData("TargetSpeed", "TargetSpeed", "999", "$.resolvedConfig.TargetSpeed")]
    [InlineData("Seed", "Seed", "999", "$.resolvedConfig.Seed")]
    [InlineData("Seed", "seed", "999", "$.resolvedConfig.Seed")]
    [InlineData("ticks", "t\\u0069cks", "999", "$.ticks")]
    [InlineData("seed", "Seed", "999", "$.seed")]
    public void DuplicateManifestFields_AreRejectedBeforeDeserialization(
        string field, string duplicate, string shadowValue, string expectedPath)
    {
        using var files = new TestFiles();
        string original = CreateManifest(files);
        string marker = $"\"{field}\":";
        string malformed = original.Replace(marker, $"\"{duplicate}\":{shadowValue},{marker}", StringComparison.Ordinal);
        Assert.NotEqual(original, malformed);
        AssertRejectedManifest(files, malformed, "duplicate", expectedPath);
    }

    [Fact]
    public void DuplicateManifestFields_AreRejectedInsideArrayObjectsBeforeUnknownFields()
    {
        using var files = new TestFiles();
        string original = CreateManifest(files);
        string malformed = "{\"extraMetadata\":[{\"name\":\"a\",\"name\":\"b\"}]," + original[1..];

        AssertRejectedManifest(files, malformed, "duplicate", "$.extraMetadata[0].name");
    }

    [Theory]
    [InlineData("runtime")]
    [InlineData("resolvedConfig")]
    public void UnknownNestedManifestFields_AreRejectedBeforeOutput(string parent)
    {
        using var files = new TestFiles();
        var manifest = JsonNode.Parse(CreateManifest(files))!.AsObject();
        manifest[parent]!.AsObject()["unexpectedField"] = 1;

        AssertRejectedManifest(files, manifest.ToJsonString(), "unexpectedField");
    }

    [Theory]
    [InlineData(40)]
    [InlineData(64)]
    public void ReplayAcceptsEmbeddedCommitWhenBuildDirtinessIsUnknown(int commitLength)
    {
        using var files = new TestFiles();
        var manifest = JsonNode.Parse(CreateManifest(files))!.AsObject();
        manifest["gitCommit"] = new string('a', commitLength);
        manifest["dirtyWorktree"] = null;
        string source = Path.Combine(files.Root, "manifest.json");
        File.WriteAllText(source, manifest.ToJsonString());
        string output = Path.Combine(files.Root, "replay");

        var result = Run("--manifest", source, "--out", output);

        Assert.True(result.Code == 0, result.Error);
        Assert.Single(Directory.GetDirectories(output));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(null, false)]
    [InlineData("short", null)]
    [InlineData("01234567890123456789012345678901234567890g", null)]
    public void InvalidGitManifestProvenance_IsRejected(string? commit, bool? dirty)
    {
        using var files = new TestFiles();
        var manifest = JsonNode.Parse(CreateManifest(files))!.AsObject();
        manifest["gitCommit"] = commit;
        manifest["dirtyWorktree"] = dirty;

        AssertRejectedManifest(files, manifest.ToJsonString(), "Git provenance");
    }

    public static IEnumerable<object?[]> EmbeddedCommitCases()
    {
        string sha1 = new('a', 40);
        string sha256 = new('B', 64);
        yield return [null, null, null];
        yield return [null, "1.0.0", null];
        yield return [null, "1.0.0+" + sha1, sha1];
        yield return [null, "1.0.0+ci.42." + sha256, sha256];
        yield return [null, "1.0.0+abc123", null];
        yield return [null, "1.0.0+" + sha1 + ".dirty", null];
        yield return [null, "1.0.0+" + new string('g', 40), null];
        yield return [sha1, "1.0.0+" + sha256, sha1];
        yield return [sha256, null, sha256];
        yield return ["invalid", "1.0.0+" + sha1, sha1];
    }

    [Theory]
    [MemberData(nameof(EmbeddedCommitCases))]
    public void BinaryProvenance_UsesOnlyValidatedEmbeddedRevisions(string? sourceRevision, string? informationalVersion, string? expected)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("ProvenanceFixture" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.RunAndCollect);
        if (sourceRevision is not null)
            assembly.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(AssemblyMetadataAttribute).GetConstructor([typeof(string), typeof(string)])!,
                ["SourceRevisionId", sourceRevision]));
        if (informationalVersion is not null)
            assembly.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!,
                [informationalVersion]));

        Assert.Equal(expected, SwarmSim.Cli.RunProvenance.ReadEmbeddedCommit(assembly));
    }

    [Fact]
    public async Task BinaryProvenance_IsIndependentOfUnrelatedCleanOrDirtyWorkingDirectory()
    {
        using var files = new TestFiles();
        string ordinaryDirectory = Path.Combine(files.Root, "ordinary");
        string unrelatedRepository = Path.Combine(files.Root, "unrelated");
        Directory.CreateDirectory(ordinaryDirectory);
        Directory.CreateDirectory(unrelatedRepository);
        await RunProcess("git", unrelatedRepository, "-c", "init.defaultBranch=main", "init", "--quiet");
        await RunProcess("git", unrelatedRepository, "-c", "user.name=CLI provenance test",
            "-c", "user.email=cli-provenance@example.invalid", "-c", "commit.gpgsign=false",
            "commit", "--quiet", "--allow-empty", "-m", "Unrelated test repository");
        string unrelatedCommit = (await RunProcess("git", unrelatedRepository, "rev-parse", "HEAD")).Trim();
        string cliAssembly = typeof(CliProgram).Assembly.Location;
        string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ??
            (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet" ? Environment.ProcessPath! : "dotnet");
        var provenance = new List<(string? Commit, bool? Dirty)>();
        for (int i = 0; i < 3; i++)
        {
            if (i == 2)
                File.WriteAllText(Path.Combine(unrelatedRepository, "untracked.txt"), "unrelated working copy change");
            string output = Path.Combine(files.Root, "run-" + i);
            await RunProcess(dotnet, i == 0 ? ordinaryDirectory : unrelatedRepository,
                cliAssembly, "--ticks", "0", "--agents", "1", "--out", output);
            string run = Assert.Single(Directory.GetDirectories(output));
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(run, "manifest.json")));
            JsonElement dirty = manifest.RootElement.GetProperty("dirtyWorktree");
            provenance.Add((manifest.RootElement.GetProperty("gitCommit").GetString(),
                dirty.ValueKind == JsonValueKind.Null ? null : dirty.GetBoolean()));
        }

        Assert.All(provenance, value => Assert.Equal(provenance[0], value));
        Assert.All(provenance, value => Assert.Null(value.Dirty));
        Assert.NotEqual(unrelatedCommit, provenance[0].Commit);
        string? information = typeof(CliProgram).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        string? embeddedRevision = information?.Contains('+') == true ? information.Split('+')[^1].Split('.')[^1] : null;
        bool hasEmbeddedCommit = embeddedRevision is { Length: 40 or 64 } && embeddedRevision.All(Uri.IsHexDigit);
        Assert.Equal(hasEmbeddedCommit ? embeddedRevision : null, provenance[0].Commit);
    }

    private static string CreateManifest(TestFiles files)
    {
        string output = Path.Combine(files.Root, "original");
        Assert.Equal(0, Run("--seed", "7", "--ticks", "0", "--agents", "1", "--out", output).Code);
        return File.ReadAllText(Path.Combine(Assert.Single(Directory.GetDirectories(output)), "manifest.json"));
    }

    private static void AssertRejectedManifest(TestFiles files, string json, params string[] diagnostics)
    {
        string source = Path.Combine(files.Root, "invalid.json");
        string output = Path.Combine(files.Root, "replay");
        File.WriteAllText(source, json);

        var result = Run("--manifest", source, "--out", output);

        Assert.Equal(1, result.Code);
        Assert.False(Directory.Exists(output));
        Assert.Equal(json, File.ReadAllText(source));
        foreach (string diagnostic in diagnostics)
            Assert.Contains(diagnostic, result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> RunProcess(string executable, string workingDirectory, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {executable}.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"{executable} timed out: {await stderr}");
        }
        string output = await stdout;
        string error = await stderr;
        Assert.True(process.ExitCode == 0, $"{executable} exited {process.ExitCode}. stdout: {output}; stderr: {error}");
        return output;
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        int code = CliProgram.Execute(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private sealed class TestFiles : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "swarm-cli-test-" + Guid.NewGuid().ToString("N"));

        public TestFiles() => Directory.CreateDirectory(Root);

        public void Dispose()
        {
            // Git object files can be read-only on Windows; all files belong to this test's temp root.
            foreach (string file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(Root, recursive: true);
        }
    }
}
