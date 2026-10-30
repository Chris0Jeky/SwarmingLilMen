using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using SwarmSim.Cli;
using SwarmSim.Cli.Visual;
using SwarmSim.Core.Metrics;
using CliProgram = SwarmSim.Cli.Program;

namespace SwarmSim.Tests;

public sealed class VisualReportTests
{
    private static readonly string[] PlainFiles = ["log.txt", "manifest.json", "metrics.csv"];
    private static readonly string[] VisualFiles = ["filmstrip.png", "log.txt", "manifest.json", "metrics.csv", "report.html"];

    // ---- PNG writer -------------------------------------------------------------------------

    [Fact]
    public void Crc32_MatchesPublishedCheckValues()
    {
        Assert.Equal(0xCBF43926u, PngWriter.Crc32("123456789"u8));
        Assert.Equal(0xAE426082u, PngWriter.Crc32("IEND"u8)); // the CRC every PNG ends with
    }

    [Fact]
    public void PngWriter_EncodesTinyKnownImage()
    {
        byte[] rgba =
        [
            255, 0, 0, 255,   0, 255, 0, 255,
            0, 0, 255, 255,   10, 20, 30, 40
        ];
        byte[] png = PngWriter.Encode(2, 2, rgba);

        DecodedPng decoded = DecodedPng.Parse(png);
        Assert.Equal((2, 2), (decoded.Width, decoded.Height));
        Assert.Equal(rgba, decoded.Rgba);
        // Fixed trailer: length 0, "IEND", CRC.
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 }, png[^12..]);
        Assert.Equal(png, PngWriter.Encode(2, 2, rgba)); // byte-stable
    }

    [Fact]
    public void PngWriter_RejectsMismatchedBuffer() =>
        Assert.Throws<ArgumentException>(() => PngWriter.Encode(2, 2, new byte[15]));

    // ---- Filmstrip renderer -----------------------------------------------------------------

    [Theory]
    [InlineData(600, new[] { 0, 85, 171, 257, 342, 428, 514, 600 })]
    [InlineData(7, new[] { 0, 1, 2, 3, 4, 5, 6, 7 })]
    [InlineData(3, new[] { 0, 1, 2, 3 })]
    [InlineData(1, new[] { 0, 1 })]
    [InlineData(0, new[] { 0 })]
    [InlineData(int.MaxValue, new[] { 0, 306783378, 613566756, 920350134, 1227133512, 1533916890, 1840700268, int.MaxValue })]
    public void SelectTicks_AreEvenlySpacedAndDeduplicated(int ticks, int[] expected) =>
        Assert.Equal(expected, Filmstrip.SelectTicks(ticks));

    [Fact]
    public void Recorder_CopiesOnlySelectedTicks()
    {
        var recorder = new FrameRecorder(3, 10f, 10f);
        for (int tick = 0; tick <= 3; tick++)
            recorder.Capture(tick, [new KinematicSample(tick, 0, 1, 0)]);
        Assert.Equal(new[] { 0, 1, 2, 3 }, recorder.Ticks);
        Assert.Equal(new[] { 0f, 1f, 2f, 3f }, recorder.Frames.Select(frame => Assert.Single(frame).X));

        var sparse = new FrameRecorder(600, 10f, 10f);
        for (int tick = 0; tick <= 600; tick++)
            sparse.Capture(tick, [new KinematicSample(tick, 0, 1, 0)]);
        Assert.Equal(sparse.Ticks.Select(tick => (float)tick), sparse.Frames.Select(frame => frame[0].X));
    }

    [Fact]
    public void Panels_PreserveWorldAspectRatio()
    {
        Assert.Equal(270, Filmstrip.PanelHeight(1920f, 1080f));
        Assert.Equal(480, Filmstrip.PanelHeight(10f, 10f));
        Assert.Equal(48, Filmstrip.PanelHeight(10000f, 1f)); // degenerate worlds are clamped, not zero-height

        Raster raster = Filmstrip.Render([0], [[new KinematicSample(5, 5, 1, 0)]], 10f, 5f);
        Assert.Equal(4 * 480 + 5 * 8, raster.Width);
        Assert.Equal(2 * 240 + 3 * 8, raster.Height);
    }

    [Fact]
    public void AllEast_RendersExactlyOneRedAgentHue()
    {
        KinematicSample[] flock = Enumerable.Range(0, 40)
            .Select(i => new KinematicSample(5 + i * 2, 5 + (i % 4) * 10, 3f, 0f)).ToArray();

        var hues = AgentColors(Filmstrip.Render([0], [flock], 100f, 50f));

        (byte r, byte g, byte b) = Assert.Single(hues);
        Assert.True(r > 200 && g < 100 && b < 100, $"east should be red but was ({r},{g},{b})");
    }

    [Fact]
    public void OppositeHeadings_RenderTwoDistinctHues()
    {
        KinematicSample[] frame =
        [
            new(20, 10, 2f, 0f),   // east
            new(60, 30, -2f, 0f)   // west
        ];

        var hues = AgentColors(Filmstrip.Render([0], [frame], 100f, 50f)).OrderBy(c => c.R).ToArray();

        Assert.Equal(2, hues.Length);
        Assert.True(hues[0].G > 200 && hues[0].B > 200 && hues[0].R < 100, "west should be cyan");
        Assert.True(hues[1].R > 200 && hues[1].G < 100, "east should be red");
    }

    [Fact]
    public void DisorderIsARainbow_AndStationaryAgentsAreGrey()
    {
        KinematicSample[] frame =
        [
            new(10, 10, 1, 0), new(30, 10, 0, 1), new(50, 10, -1, 0), new(70, 10, 0, -1), // four headings
            new(20, 30, 0, 0), new(40, 30, 0, 0)                                          // stationary
        ];

        var colors = AgentColors(Filmstrip.Render([0], [frame], 100f, 50f));

        Assert.Equal(5, colors.Count);
        Assert.Contains(Filmstrip.Stationary, colors);
    }

    [Fact]
    public void OutOfWorldAndNonFiniteAgents_DoNotThrowOrEscapeThePanel()
    {
        KinematicSample[] frame =
        [
            new(-50, -50, 1, 1), new(1e9f, 1e9f, -1, -1), new(100, 50, 1, 0), new(0, 0, 0, 1),
            new(float.NaN, 5, 1, 0)
        ];
        Raster raster = Filmstrip.Render([0], [frame], 100f, 50f);
        Assert.Equal(raster.Width * raster.Height * 4, raster.Rgba.Length);
        DecodedPng.Parse(PngWriter.Encode(raster.Width, raster.Height, raster.Rgba));
    }

    // ---- CLI: determinism, files, report ----------------------------------------------------

    [Theory]
    [InlineData("canonical")]
    [InlineData("legacy")]
    public void Visual_DoesNotChangeTheSimulation_AndPngIsRepeatable(string engine)
    {
        using var files = new TempDir();
        string[] args = RunArgs(engine, files.Out);

        string plain = RunOk(args);
        string visual1 = RunOk([.. args, "--visual"]);
        string visual2 = RunOk([.. args, "--visual"]);

        Assert.Equal(PlainFiles, FileNames(plain));
        Assert.Equal(VisualFiles, FileNames(visual1));
        Assert.Equal(File.ReadAllBytes(Path.Combine(plain, "metrics.csv")), File.ReadAllBytes(Path.Combine(visual1, "metrics.csv")));
        foreach (string field in new[] { "finalKinematicHash", "metricsSha256" })
            Assert.Equal(Manifest(plain).GetProperty(field).GetString(), Manifest(visual1).GetProperty(field).GetString());
        Assert.Equal(File.ReadAllBytes(Path.Combine(visual1, "filmstrip.png")), File.ReadAllBytes(Path.Combine(visual2, "filmstrip.png")));
        Assert.Equal(File.ReadAllText(Path.Combine(visual1, "report.html")).Replace(Manifest(visual1).GetProperty("runId").GetString()!, "RUN"),
            File.ReadAllText(Path.Combine(visual2, "report.html")).Replace(Manifest(visual2).GetProperty("runId").GetString()!, "RUN"));

        DecodedPng png = DecodedPng.Parse(File.ReadAllBytes(Path.Combine(visual1, "filmstrip.png")));
        Assert.Equal(4 * 480 + 5 * 8, png.Width);
        Assert.Equal(2 * PanelHeightOf(visual1) + 3 * 8, png.Height);
    }

    [Theory]
    [InlineData("0", 1)]
    [InlineData("3", 4)]
    [InlineData("20", 8)]
    public void Visual_HandlesFewTicks(string ticks, int panels)
    {
        using var files = new TempDir();
        string run = RunOk(["--engine", "canonical", "--scenario", "balanced", "--seed", "7", "--ticks", ticks, "--agents", "12", "--out", files.Out, "--visual"]);

        DecodedPng png = DecodedPng.Parse(File.ReadAllBytes(Path.Combine(run, "filmstrip.png")));
        Assert.Equal(panels, CountPanels(png, PanelHeightOf(run)));
    }

    [Fact]
    public void Report_IsSelfContained_HasOneSvgPerMetric_AndSurvivesUndefinedCells()
    {
        using var files = new TempDir();
        string run = RunOk(["--engine", "canonical", "--scenario", "balanced", "--seed", "7", "--ticks", "8", "--agents", "12", "--out", files.Out,
            "--metrics", "population,meanSpeed,boids.steeringMagnitudeMean,boids.truncatedQueryFraction", "--visual"]);
        string[] rows = File.ReadAllLines(Path.Combine(run, "metrics.csv"));
        Assert.EndsWith(",,", rows[1]); // precondition: rule metrics are undefined at tick 0

        string html = File.ReadAllText(Path.Combine(run, "report.html"));

        AssertSelfContained(html);
        Assert.Equal(4, Regex.Count(html, "<svg "));
        Assert.DoesNotContain("NaN", html);
        Assert.DoesNotContain("Infinity", html);
        Assert.Contains("prefers-color-scheme:dark", html);
        Assert.Contains("name=\"viewport\"", html);
        Assert.Contains("data:image/png;base64,", html);
        JsonElement manifest = Manifest(run);
        Assert.Contains(manifest.GetProperty("finalKinematicHash").GetString()!, html);
        Assert.Contains("canonical", html);
        Assert.DoesNotContain("\r", html);
        string commit = manifest.GetProperty("gitCommit").GetString() ?? "unknown";
        Assert.Contains(commit[..Math.Min(12, commit.Length)], html);
        // first/last/min/max for the defined column, and the embedded PNG decodes.
        Assert.Contains("<span>first 12</span><span>last 12</span><span>min 12</span><span>max 12</span>", html);
        string base64 = Regex.Match(html, "base64,([A-Za-z0-9+/=]+)\"").Groups[1].Value;
        DecodedPng.Parse(Convert.FromBase64String(base64));
    }

    [Fact]
    public void Report_SkipsGapsAndDrawsLonePoints_NeverZero()
    {
        using var files = new TempDir();
        string run = RunOk(["--engine", "canonical", "--scenario", "balanced", "--seed", "7", "--ticks", "2", "--agents", "4", "--out", files.Out]);
        RunManifest manifest = RunManifest.Read(Path.Combine(run, "manifest.json"));
        string csv = "tick,gap,alldead,solo\n0,5,,\n1,,,\n2,15,,7\n3,25,,\n";

        string html = RunReport.Html(manifest, PngWriter.Encode(1, 1, new byte[4]), csv);

        Assert.Equal(2, Regex.Count(html, "<svg "));
        Assert.Equal(1, Regex.Count(html, "no defined values"));
        // "gap": two segments (tick 0 alone; ticks 2-3) -> the lone point is a two-vertex line.
        Assert.Contains("<span>first 5</span><span>last 25</span><span>min 5</span><span>max 25</span>", html);
        Assert.Equal(3, Regex.Count(html, "<polyline "));
        Assert.DoesNotContain("NaN", html);
    }

    [Theory]
    [InlineData("canonical")]
    [InlineData("legacy")]
    public void Replay_WithVisual_StillVerifiesHashes(string engine)
    {
        using var files = new TempDir();
        string original = RunOk(RunArgs(engine, files.Out));
        string manifestPath = Path.Combine(original, "manifest.json");

        var replay = SweepCliTests.Run("--manifest", manifestPath, "--visual", "--out", files.Out);

        Assert.True(replay.Code == 0, replay.Error);
        string replayed = replay.Output.Trim();
        Assert.Equal(VisualFiles, FileNames(replayed));
        Assert.Contains("Verified replay of", File.ReadAllText(Path.Combine(replayed, "log.txt")));
        Assert.Equal(File.ReadAllBytes(Path.Combine(original, "metrics.csv")), File.ReadAllBytes(Path.Combine(replayed, "metrics.csv")));

        // Verification still fails closed, and a failed run leaves no partial directory behind.
        string tampered = File.ReadAllText(manifestPath).Replace(
            Manifest(original).GetProperty("finalKinematicHash").GetString()!, new string('0', 64));
        string tamperedPath = Path.Combine(files.Root, "tampered.json");
        File.WriteAllText(tamperedPath, tampered);
        string failOut = Path.Combine(files.Root, "fail");
        Assert.Equal(1, SweepCliTests.Run("--manifest", tamperedPath, "--visual", "--out", failOut).Code);
        Assert.Empty(Directory.GetFileSystemEntries(failOut));
    }

    [Theory]
    [InlineData("--manifest", "m.json", "--seed", "1")]
    [InlineData("--manifest", "m.json", "--seed", "1", "--visual")]
    [InlineData("--sweep", "s.json", "--engine", "legacy", "--visual")]
    [InlineData("--sweep", "s.json", "--ticks", "5")]
    [InlineData("--list-metrics", "--visual")]
    [InlineData("--visual", "--visual")]
    public void InvalidFlagCombinations_ExitTwo(params string[] args)
    {
        using var files = new TempDir();
        var result = SweepCliTests.Run([.. args, "--out", files.Out]);
        Assert.Equal(2, result.Code);
        Assert.False(Directory.Exists(files.Out));
    }

    [Fact]
    public void Help_DocumentsVisual_AndRelaxedRules()
    {
        string help = SweepCliTests.Run("--help").Output;
        Assert.Contains("--visual", help);
        Assert.Contains("filmstrip.png", help);
        string error = SweepCliTests.Run("--manifest", "m.json", "--seed", "1").Error;
        Assert.Contains("--manifest can be combined only with --out, --visual or --help", error);
        error = SweepCliTests.Run("--sweep", "s.json", "--seed", "1").Error;
        Assert.Contains("--sweep can be combined only with --out, --visual or --help", error);
    }

    // ---- sweeps -----------------------------------------------------------------------------

    [Theory]
    [InlineData("canonical")]
    [InlineData("legacy")]
    public void VisualSweep_WritesCellReportsAndRelativeIndex(string engine)
    {
        using var files = new SweepCliTests.SweepFiles(engine);
        string sweep = files.WriteSweep();

        var plain = SweepCliTests.Run("--sweep", sweep, "--out", files.Output);
        var visual = SweepCliTests.Run("--sweep", sweep, "--visual", "--out", files.Output);
        Assert.True(plain.Code == 0 && visual.Code == 0, plain.Error + visual.Error);
        string plainRoot = plain.Output.Trim(), root = visual.Output.Trim();

        // Without --visual the sweep root is unchanged; with it, the simulation outputs are identical.
        Assert.Equal(new[] { "summary.csv" }, FileNames(plainRoot, directories: false));
        Assert.Equal(new[] { "index.html", "summary.csv" }, FileNames(root, directories: false));
        Assert.Equal(File.ReadAllBytes(Path.Combine(plainRoot, "summary.csv")), File.ReadAllBytes(Path.Combine(root, "summary.csv")));
        string[] cells = Directory.GetDirectories(root).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(12, cells.Length);
        foreach (string cell in cells)
        {
            Assert.Equal(VisualFiles, FileNames(cell));
            DecodedPng.Parse(File.ReadAllBytes(Path.Combine(cell, "filmstrip.png")));
        }

        string html = File.ReadAllText(Path.Combine(root, "index.html"));
        AssertSelfContained(html);
        Assert.Equal(12, Regex.Count(html, "<tr><td><a "));
        string[] summary = File.ReadAllLines(Path.Combine(root, "summary.csv"));
        for (int i = 0; i < 12; i++)
        {
            string id = $"cell-{i:D6}";
            Assert.Contains($"href=\"{id}/report.html\"", html);
            Assert.Contains($"src=\"{id}/filmstrip.png\"", html);
            string[] columns = summary[i + 1].Split(',');
            string row = Regex.Match(html, $"<tr><td><a href=\"{id}/report.html\".*?</tr>").Value;
            Assert.Contains($"<td>{columns[5]}</td>", row); // SeparationWeight
            Assert.Contains($"<td>{columns[2]}</td>", row); // seed
            string terminalSpeed = double.Parse(columns[6], CultureInfo.InvariantCulture).ToString("G6", CultureInfo.InvariantCulture);
            Assert.Contains($"<td>{terminalSpeed}</td>", row);
        }
        foreach (Match link in Regex.Matches(html, "(?:href|src)=\"([^\"]+)\""))
        {
            string target = link.Groups[1].Value;
            Assert.DoesNotMatch(@"^(?:[a-zA-Z][a-zA-Z0-9+.-]*:|/|\\)", target);
            Assert.DoesNotContain("..", target);
            Assert.True(File.Exists(Path.Combine(root, target)), target);
        }
    }

    [Fact]
    public void SweepRunner_PassesVisualToTheCellDelegate_AndKeepsItsBehaviour()
    {
        using var files = new SweepCliTests.SweepFiles();
        SweepPlan plan = SweepPlan.Load(files.WriteSweep());
        var seen = new List<bool>();

        string Execute(RunRequest request, string work)
        {
            seen.Add(request.Visual);
            return ExperimentRunner.Run(request, work);
        }

        string off = SweepRunner.Run(plan, files.Output, Execute);
        string on = SweepRunner.Run(plan, files.Output, Execute, visual: true);

        Assert.Equal(12, seen.Take(12).Count(visual => !visual));
        Assert.Equal(12, seen.Skip(12).Count(visual => visual));
        Assert.False(File.Exists(Path.Combine(off, "index.html")));
        Assert.True(File.Exists(Path.Combine(on, "index.html")));
    }

    // ---- helpers ----------------------------------------------------------------------------

    private static string[] RunArgs(string engine, string output) =>
        ["--engine", engine, "--scenario", "balanced", "--seed", "7", "--ticks", "12", "--agents", "24", "--out", output];

    private static string RunOk(string[] args)
    {
        var result = SweepCliTests.Run(args);
        Assert.True(result.Code == 0, result.Error);
        return result.Output.Trim();
    }

    private static string[] FileNames(string directory, bool directories = false) =>
        (directories ? Directory.GetDirectories(directory) : Directory.GetFiles(directory))
            .Select(path => Path.GetFileName(path)!).Order(StringComparer.Ordinal).ToArray();

    private static JsonElement Manifest(string run)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(run, "manifest.json")));
        return document.RootElement.Clone();
    }

    private static int PanelHeightOf(string run)
    {
        JsonElement config = Manifest(run).GetProperty("resolvedConfig");
        return Filmstrip.PanelHeight(config.GetProperty("WorldWidth").GetSingle(), config.GetProperty("WorldHeight").GetSingle());
    }

    private static void AssertSelfContained(string html)
    {
        Assert.DoesNotContain("http://", html);
        Assert.DoesNotContain("https://", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("url(", html);
    }

    /// <summary>Distinct pixel colours that are neither background, panel background, border nor tick text.</summary>
    private static List<(byte R, byte G, byte B)> AgentColors(Raster raster)
    {
        var known = new HashSet<(byte, byte, byte)> { Filmstrip.Background, Filmstrip.PanelBackground, Filmstrip.Border, Filmstrip.Text };
        var found = new HashSet<(byte R, byte G, byte B)>();
        for (int i = 0; i < raster.Rgba.Length; i += 4)
        {
            var color = (raster.Rgba[i], raster.Rgba[i + 1], raster.Rgba[i + 2]);
            if (!known.Contains(color)) found.Add(color);
        }
        return found.ToList();
    }

    /// <summary>A panel exists where its left border column is drawn.</summary>
    private static int CountPanels(DecodedPng png, int height)
    {
        int count = 0;
        for (int row = 0; row < Filmstrip.Rows; row++)
            for (int column = 0; column < Filmstrip.Columns; column++)
            {
                int x = 8 + column * (480 + 8), y = 8 + row * (height + 8) + height / 2; // left border column
                int offset = (y * png.Width + x) * 4;
                if ((png.Rgba[offset], png.Rgba[offset + 1], png.Rgba[offset + 2]) == Filmstrip.Border) count++;
            }
        return count;
    }

    private sealed class TempDir : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "swarm-visual-test-" + Guid.NewGuid().ToString("N"));
        internal string Out => Path.Combine(Root, "runs");
        internal TempDir() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    /// <summary>Independent PNG reader: signature, chunk order, every CRC, inflate, unfilter.</summary>
    private sealed class DecodedPng
    {
        internal int Width { get; private init; }
        internal int Height { get; private init; }
        internal byte[] Rgba { get; private init; } = [];

        internal static DecodedPng Parse(byte[] png)
        {
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);
            int position = 8, width = 0, height = 0;
            var chunks = new List<string>();
            using var idat = new MemoryStream();
            while (position < png.Length)
            {
                int length = ReadInt(png, position);
                string type = System.Text.Encoding.ASCII.GetString(png, position + 4, 4);
                byte[] body = png[(position + 8)..(position + 8 + length)];
                uint stored = (uint)ReadInt(png, position + 8 + length);
                Assert.Equal(stored, PngWriter.Crc32(png.AsSpan(position + 4, 4 + length))); // independent of Crc32(type, data) split
                chunks.Add(type);
                if (type == "IHDR")
                {
                    Assert.Equal(13, length);
                    width = ReadInt(body, 0);
                    height = ReadInt(body, 4);
                    Assert.Equal(new byte[] { 8, 6, 0, 0, 0 }, body[8..13]); // 8-bit RGBA, deflate, adaptive filter, no interlace
                }
                else if (type == "IDAT") idat.Write(body);
                position += 12 + length;
            }
            Assert.Equal(png.Length, position);
            Assert.Equal("IHDR", chunks[0]);
            Assert.Equal("IEND", chunks[^1]);
            Assert.Contains("IDAT", chunks);

            idat.Position = 0;
            using var inflated = new MemoryStream();
            using (var zlib = new ZLibStream(idat, CompressionMode.Decompress)) zlib.CopyTo(inflated);
            byte[] raw = inflated.ToArray();
            int stride = width * 4;
            Assert.Equal(height * (stride + 1), raw.Length);
            var pixels = new byte[height * stride];
            for (int y = 0; y < height; y++)
            {
                byte filter = raw[y * (stride + 1)];
                for (int x = 0; x < stride; x++)
                {
                    int a = x >= 4 ? pixels[y * stride + x - 4] : 0;
                    int b = y > 0 ? pixels[(y - 1) * stride + x] : 0;
                    int c = x >= 4 && y > 0 ? pixels[(y - 1) * stride + x - 4] : 0;
                    int value = raw[y * (stride + 1) + 1 + x];
                    pixels[y * stride + x] = (byte)(value + filter switch
                    {
                        0 => 0, 1 => a, 2 => b, 3 => (a + b) / 2, 4 => Paeth(a, b, c),
                        _ => throw new InvalidDataException($"filter {filter}")
                    });
                }
            }
            return new DecodedPng { Width = width, Height = height, Rgba = pixels };
        }

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }

        private static int ReadInt(byte[] data, int offset) =>
            (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
    }
}
