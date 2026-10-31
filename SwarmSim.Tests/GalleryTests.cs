using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SwarmSim.Cli;
using SwarmSim.Cli.Visual;
using SwarmSim.Core.Canonical;

namespace SwarmSim.Tests;

public sealed class GalleryTests
{
    // A tiny canonical world defined inline, so no recipe file is needed and 8 ticks are enough.
    private const string Small = """{"InitialCapacity":16,"WorldWidth":96,"WorldHeight":64,"FixedDeltaTime":0.05,"SenseRadius":12,"SeparationRadius":5,"MaxForce":0.2,"WanderStrength":0,"TargetSpeed":3}""";
    private const string Holds = """{ "population": { "min": 12, "max": 12 } }""";
    private const string Broken = """{ "population": { "min": 13 } }""";

    private static string Entry(string name, string expect = Holds, int ticks = 8, string metrics = "\"population\",\"meanSpeed\"") => $$"""
        { "name": "{{name}}", "engine": "canonical", "settings": {{Small}}, "agents": 12, "ticks": {{ticks}}, "seed": 7,
          "metrics": [{{metrics}}], "expect": {{expect}} }
        """;

    private static string Recipe(params string[] entries) => $$"""{ "entries": [{{string.Join(",", entries)}}] }""";

    // ---- exit codes, publishing and ordering ------------------------------------------------

    [Fact]
    public void PassingGalleryExitsZeroAndPublishesEveryEntry()
    {
        using var files = new GalleryFiles();
        var result = SweepCliTests.Run("--gallery", files.Write(Recipe(Entry("alpha"), Entry("beta"))), "--out", files.Output);

        Assert.True(result.Code == 0, result.Error + result.Output);
        string[] lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string gallery = lines[0];
        Assert.StartsWith(Path.Combine(files.Output, "gallery-"), gallery);
        Assert.Matches(@"^PASS  alpha\s+population 12 \(12\.\.12\)\s+\d+ ms$", lines[1]);
        Assert.Matches(@"^PASS  beta\s+population 12 \(12\.\.12\)\s+\d+ ms$", lines[2]);
        Assert.Matches(@"^2/2 passed in \d+ ms$", lines[3]);
        Assert.Equal(4, lines.Length);
        foreach (string entry in new[] { "01-alpha", "02-beta" })
            foreach (string file in new[] { "manifest.json", "metrics.csv", "log.txt", "filmstrip.png", "report.html" })
                Assert.True(File.Exists(Path.Combine(gallery, entry, file)), entry + "/" + file);
        Assert.Equal(new[] { "01-alpha", "02-beta", "index.html", "summary.json" },
            Directory.GetFileSystemEntries(gallery).Select(Path.GetFileName).Order(StringComparer.Ordinal));

        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(gallery, "summary.json")));
        Assert.True(summary.RootElement.GetProperty("passed").GetBoolean());
        Assert.Empty(Directory.GetDirectories(files.Output, ".*")); // staging never survives
    }

    [Fact]
    public void FailingGalleryExitsOneStillPublishesAndSortsFailuresFirst()
    {
        using var files = new GalleryFiles();
        var result = SweepCliTests.Run("--gallery",
            files.Write(Recipe(Entry("ok-a"), Entry("bad-b", Broken), Entry("ok-c"), Entry("bad-d", Broken))), "--out", files.Output);

        Assert.Equal(1, result.Code);
        Assert.Equal("", result.Error);
        string[] lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string gallery = lines[0];
        Assert.True(Directory.Exists(gallery));
        Assert.Matches(@"^PASS  ok-a\s", lines[1]);
        Assert.Matches(@"^FAIL  bad-b\s+population 12 \(>= 13\)\s+\d+ ms$", lines[2]);
        Assert.Equal("2/4 passed", Regex.Match(lines[^1], @"^\d+/\d+ passed").Value);
        foreach (string entry in new[] { "01-ok-a", "02-bad-b", "03-ok-c", "04-bad-d" })
            Assert.True(File.Exists(Path.Combine(gallery, entry, "metrics.csv")), entry); // every run published, failures included

        string html = File.ReadAllText(Path.Combine(gallery, "index.html"));
        Assert.Contains("FAIL: 2 of 4 entries", html);
        int[] order = new[] { "bad-b", "bad-d", "ok-a", "ok-c" }.Select(name => html.IndexOf("/report.html\">" + name + "</a>", StringComparison.Ordinal)).ToArray();
        Assert.All(order, position => Assert.True(position > 0));
        Assert.Equal(order.Order(), order); // failures keep recipe order, then the passes do

        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(gallery, "summary.json")));
        Assert.False(summary.RootElement.GetProperty("passed").GetBoolean());
        JsonElement[] entries = summary.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        Assert.Equal(new[] { "ok-a", "bad-b", "ok-c", "bad-d" }, entries.Select(e => e.GetProperty("entry").GetString()));
        Assert.Equal(new[] { true, false, true, false }, entries.Select(e => e.GetProperty("passed").GetBoolean()));
        Assert.Equal("02-bad-b", entries[1].GetProperty("runDirectory").GetString());
        JsonElement failed = entries[1].GetProperty("expectations")[0];
        Assert.Equal("population", failed.GetProperty("metric").GetString());
        Assert.Equal(13, failed.GetProperty("min").GetDouble());
        Assert.Equal(JsonValueKind.Null, failed.GetProperty("max").ValueKind);
        Assert.Equal(12, failed.GetProperty("measured").GetDouble());
        Assert.False(failed.GetProperty("passed").GetBoolean());
    }

    [Fact]
    public void MeasuredValueIsTheFinalQuarterMeanOfTheRecordedCsv()
    {
        using var files = new GalleryFiles();
        var result = SweepCliTests.Run("--gallery",
            files.Write(Recipe(Entry("order", """{ "polarization": { "min": 0 } }""", metrics: "\"polarization\""))), "--out", files.Output);
        Assert.True(result.Code == 0, result.Error);
        string gallery = result.Output.Split('\n')[0].Trim();

        // Independent oracle: ticks 0..8, so the last 8/4 = 2 rows are ticks 7 and 8.
        double[] polarization = File.ReadAllLines(Path.Combine(gallery, "01-order", "metrics.csv")).Skip(1)
            .Select(row => double.Parse(row.Split(',')[1], CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(9, polarization.Length);
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(gallery, "summary.json")));
        double measured = summary.RootElement.GetProperty("entries")[0].GetProperty("expectations")[0].GetProperty("measured").GetDouble();
        Assert.InRange(measured, (polarization[7] + polarization[8]) / 2 - 1e-12, (polarization[7] + polarization[8]) / 2 + 1e-12);
        Assert.NotEqual(polarization.Average(), measured); // not the whole-run mean
    }

    // ---- final-quarter mean -----------------------------------------------------------------

    [Theory]
    [InlineData(8, 7.5)] // window 2: ticks 7 and 8
    [InlineData(4, 4.0)] // window 1
    [InlineData(7, 7.0)] // 7 / 4 = 1
    [InlineData(3, 3.0)] // too short for a quarter: the last tick alone
    [InlineData(1, 1.0)]
    [InlineData(12, 11.0)] // window 3: ticks 10, 11, 12
    public void FinalQuarterMean_UsesTheLastQuarterOrTheFinalTick(int ticks, double expected)
    {
        // Column "a" holds the tick number, so the mean is the mean of the window's tick numbers.
        string csv = "tick,a\n" + string.Concat(Enumerable.Range(0, ticks + 1).Select(t => $"{t},{t}\n"));
        Assert.Equal(expected, Gallery.FinalQuarterMean(csv, "a"));
    }

    [Fact]
    public void FinalQuarterMean_IgnoresEmptyCellsAndReturnsNaNWhenNoneAreDefined()
    {
        // Ticks 0..8, window = ticks 7 and 8. Empty cells are undefined values, never zero.
        const string csv = "tick,a,b,c\r\n0,9,9,\r\n1,9,9,\r\n2,9,9,\r\n3,9,9,\r\n4,9,9,\r\n5,9,9,\r\n6,9,9,\r\n7,2,,\r\n8,4,6,\r\n";
        Assert.Equal(3.0, Gallery.FinalQuarterMean(csv, "a"));
        Assert.Equal(6.0, Gallery.FinalQuarterMean(csv, "b")); // tick 7 is undefined, so the mean is tick 8 alone
        Assert.True(double.IsNaN(Gallery.FinalQuarterMean(csv, "c")));
        Assert.Throws<InvalidDataException>(() => Gallery.FinalQuarterMean(csv, "tick")); // not a metric column
        Assert.Throws<InvalidDataException>(() => Gallery.FinalQuarterMean(csv, "missing"));
    }

    [Fact]
    public void AnUndefinedMeasurementFailsAnyBound()
    {
        Assert.False(new GalleryExpectation("m", 0, null).Holds(double.NaN));
        Assert.False(new GalleryExpectation("m", null, 1).Holds(double.NaN));
        Assert.True(new GalleryExpectation("m", 1, 2).Holds(1)); // both bounds inclusive
        Assert.True(new GalleryExpectation("m", 1, 2).Holds(2));
        Assert.False(new GalleryExpectation("m", 1, 2).Holds(2.0000001));
        Assert.False(new GalleryExpectation("m", 1, 2).Holds(0.9999999));
    }

    // ---- refusals before any output ---------------------------------------------------------

    [Theory]
    [MemberData(nameof(Refusals))]
    public void InvalidGalleryIsRefusedBeforeAnyOutput(string description, string json, string diagnostic)
    {
        using var files = new GalleryFiles();
        var result = SweepCliTests.Run("--gallery", files.Write(json), "--out", files.Output);

        Assert.True(result.Code == 1, description + ": " + result.Output);
        Assert.Contains(diagnostic, result.Error, StringComparison.Ordinal);
        Assert.Equal("", result.Output);
        Assert.False(Directory.Exists(files.Output), description + " created output");
    }

    public static TheoryData<string, string, string> Refusals() => new()
    {
        { "unknown top-level field", $$"""{ "entries": [{{Entry("a")}}], "extra": 1 }""", "unknown field 'extra'" },
        { "unknown entry field", Recipe(Entry("a").Replace("\"seed\": 7", "\"seed\": 7, \"colour\": \"red\"")), "unknown field 'colour'" },
        { "duplicate name", Recipe(Entry("a"), Entry("a")), "name 'a' is duplicated" },
        { "non-slug name", Recipe(Entry("Not A Slug")), "must be a lowercase slug" },
        { "path-like name", Recipe(Entry("..")), "must be a lowercase slug" },
        { "unknown metric", Recipe(Entry("a", """{ "nonsense": { "min": 0 } }""", metrics: "\"nonsense\"")), "nonsense" },
        { "expectation on a metric the run does not record", Recipe(Entry("a", """{ "polarization": { "min": 0 } }""")), "expects 'polarization', which the run does not record" },
        { "missing metrics", Recipe(Entry("a").Replace("\"metrics\": [\"population\",\"meanSpeed\"],", "")), "missing required field 'metrics'" },
        { "missing expect", Recipe(Entry("a").Replace(", \"expect\": " + Holds, "")), "missing required field 'expect'" },
        { "empty expect", Recipe(Entry("a", "{}")), "expect must name at least one metric" },
        { "expectation without a bound", Recipe(Entry("a", """{ "population": {} }""")), "needs a min or a max" },
        { "unknown bound field", Recipe(Entry("a", """{ "population": { "min": 1, "mean": 2 } }""")), "unknown field 'mean'" },
        { "min above max", Recipe(Entry("a", """{ "population": { "min": 5, "max": 4 } }""")), "min above max" },
        { "zero ticks", Recipe(Entry("a", ticks: 0)), "'ticks' must be an integer of at least 1" },
        { "seed inside settings", Recipe(Entry("a").Replace("\"WanderStrength\":0", "\"WanderStrength\":0,\"Seed\":3")), "sets the seed in 'seed'" },
        { "unknown engine", Recipe(Entry("a").Replace("\"canonical\"", "\"boids\"")), "engine must be" },
        { "unknown setting", Recipe(Entry("a").Replace("\"WanderStrength\":0", "\"Wander\":0")), "Wander" },
        { "runtime cost metric", Recipe(Entry("a", Holds, metrics: "\"population\",\"runtime.wallMilliseconds\"")), "deterministic" },
        { "duplicate JSON field", Recipe(Entry("a").Replace("\"seed\": 7", "\"seed\": 7, \"seed\": 8")), "Duplicate field" },
        { "empty entries", """{ "entries": [] }""", "nonempty array" },
        { "a later entry fails after an earlier one is fine", Recipe(Entry("a"), Entry("b", Holds, metrics: "\"nonsense\"")), "nonsense" }
    };

    [Theory]
    [InlineData("--ticks", "5")]
    [InlineData("--engine", "aco")]
    [InlineData("--visual")]
    [InlineData("--sweep", "other.json")]
    [InlineData("--manifest", "other.json")]
    [InlineData("--list-metrics")]
    public void GalleryCombinesOnlyWithOutAndHelp(params string[] extra)
    {
        using var files = new GalleryFiles();
        var result = SweepCliTests.Run(["--gallery", files.Write(Recipe(Entry("a"))), "--out", files.Output, .. extra]);

        Assert.Equal(2, result.Code);
        Assert.Contains("Use --help for usage.", result.Error);
        Assert.Equal("", result.Output);
        Assert.False(Directory.Exists(files.Output));
    }

    [Fact]
    public void GalleryNeedsAFileAndHelpDocumentsItsExitCodes()
    {
        Assert.Equal(2, SweepCliTests.Run("--gallery").Code);
        var help = SweepCliTests.Run("--gallery", "x.json", "--help");
        Assert.Equal(0, help.Code);
        Assert.Contains("--gallery FILE", help.Output);
        Assert.Contains("every band holds", help.Output);
        Assert.Contains("or any band fails", help.Output);
    }

    // ---- the page ---------------------------------------------------------------------------

    [Fact]
    public void IndexHtmlIsSelfContainedWithRelativeLinksToFilesThatExist()
    {
        using var files = new GalleryFiles();
        var result = SweepCliTests.Run("--gallery",
            files.Write(Recipe(Entry("alpha"), Entry("beta", Broken))), "--out", files.Output);
        Assert.Equal(1, result.Code);
        string gallery = result.Output.Split('\n')[0].Trim();
        string html = File.ReadAllText(Path.Combine(gallery, "index.html"));

        Assert.DoesNotContain("http://", html);
        Assert.DoesNotContain("https://", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("\r", html);
        Assert.Contains("prefers-color-scheme:dark", html);
        Assert.Contains("name=\"viewport\"", html);
        string[] links = Regex.Matches(html, "(?:href|src)=\"([^\"]+)\"").Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(6, links.Length); // per card: picture link, picture, title link
        foreach (string link in links)
        {
            Assert.DoesNotContain(":", link);
            Assert.False(Path.IsPathRooted(link) || link.StartsWith('/'), link);
            Assert.True(File.Exists(Path.Combine(gallery, link)), link);
        }
    }

    [Fact]
    public void IndexHtmlEscapesNamesAndBoundText()
    {
        // Real names are slugs, so this drives the page writer directly with hostile ones.
        using var files = new GalleryFiles();
        Directory.CreateDirectory(files.Root);
        const string hostile = "a<b>&\"c'";
        var request = new RunRequest("canonical", 1, 1, new CanonicalWorldSettings(), null);
        var entry = new GalleryEntry(0, hostile, request, [new GalleryExpectation("m<x>", 1, null)]);
        var results = new[] { new EntryResult(entry, [new ExpectationResult(entry.Expectations[0], 0.5)], 1) };

        GalleryReport.Write(files.Root, results);

        string html = File.ReadAllText(Path.Combine(files.Root, "index.html"));
        Assert.Contains("a&lt;b&gt;&amp;&quot;c", html);
        Assert.DoesNotContain("<b>", html);
        Assert.Contains("m&lt;x&gt; 0.5 (&gt;= 1)", html);
        Assert.DoesNotContain("m<x>", html);
    }

    // ---- every entry is a normal replayable run ---------------------------------------------

    [Fact]
    public void EveryEntryManifestReplaysWithVerifiedHashes()
    {
        using var files = new GalleryFiles();
        var result = SweepCliTests.Run("--gallery", files.Write(Recipe(Entry("alpha"), Entry("beta", Broken))), "--out", files.Output);
        string gallery = result.Output.Split('\n')[0].Trim();

        foreach (string entry in new[] { "01-alpha", "02-beta" })
        {
            string source = Path.Combine(gallery, entry);
            var replay = SweepCliTests.Run("--manifest", Path.Combine(source, "manifest.json"), "--out", Path.Combine(files.Root, "replays"));
            Assert.True(replay.Code == 0, entry + ": " + replay.Error);
            string replayed = replay.Output.Trim();
            Assert.Equal(File.ReadAllBytes(Path.Combine(source, "metrics.csv")), File.ReadAllBytes(Path.Combine(replayed, "metrics.csv")));
            Assert.Contains("Verified replay of run-", File.ReadAllText(Path.Combine(replayed, "log.txt")));
        }
    }

    // ---- the committed recipe ---------------------------------------------------------------

    [Fact]
    public void CommittedGalleryPassesQuickly()
    {
        using var files = new GalleryFiles();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var result = SweepCliTests.Run("--gallery", Path.Combine(RepositoryRoot(), "scenarios", "gallery.json"), "--out", files.Output);
        timer.Stop();

        Assert.True(result.Code == 0, result.Output + result.Error);
        string gallery = result.Output.Split('\n')[0].Trim();
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(gallery, "summary.json")));
        Assert.Equal(6, summary.RootElement.GetProperty("entries").GetArrayLength());
        // The suite budget for this test; cut the recipe's ticks rather than raising it.
        Assert.InRange(timer.Elapsed.TotalSeconds, 0, 5);
    }

    private static string RepositoryRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "SwarmingLilMen.sln")))
            directory = Path.GetDirectoryName(directory);
        return directory ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class GalleryFiles : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "swarm-gallery-test-" + Guid.NewGuid().ToString("N"));
        internal string Output => Path.Combine(Root, "runs");

        internal string Write(string json)
        {
            Directory.CreateDirectory(Root);
            string path = Path.Combine(Root, "gallery.json");
            File.WriteAllText(path, json);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
