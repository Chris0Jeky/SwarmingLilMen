using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SwarmSim.Cli.Visual;
using SwarmSim.Core.Utils;

namespace SwarmSim.Cli;

/// <summary>A bound on the final-quarter mean of one recorded metric. At least one side is set; both are inclusive.</summary>
internal sealed record GalleryExpectation(string Metric, double? Min, double? Max)
{
    internal bool Holds(double measured) =>
        !double.IsNaN(measured) && (Min is null || measured >= Min) && (Max is null || measured <= Max);

    internal string Bound => (Min, Max) switch
    {
        ({ } low, { } high) => $"{RunReport.Fmt(low)}..{RunReport.Fmt(high)}",
        ({ } low, null) => $">= {RunReport.Fmt(low)}",
        (null, { } high) => $"<= {RunReport.Fmt(high)}",
        _ => throw new InvalidOperationException("An expectation needs a bound.")
    };
}

internal sealed record GalleryEntry(int Index, string Name, RunRequest Request, IReadOnlyList<GalleryExpectation> Expectations)
{
    /// <summary>Stable, sortable child of the gallery directory; the normal run id stays in its manifest.</summary>
    internal string Directory => (Index + 1).ToString("D2", CultureInfo.InvariantCulture) + "-" + Name;
}

internal sealed record ExpectationResult(GalleryExpectation Expectation, double Measured)
{
    internal bool Passed => Expectation.Holds(Measured);

    /// <summary>"polarization 0.987 (>= 0.9)" - the same text on the console and in the page.</summary>
    internal string Text => $"{Expectation.Metric} {Gallery.Num(Measured)} ({Expectation.Bound})";
}

internal sealed record EntryResult(GalleryEntry Entry, IReadOnlyList<ExpectationResult> Expectations, long ElapsedMilliseconds)
{
    internal bool Passed => Expectations.All(result => result.Passed);
}

internal sealed record GalleryResult(string Directory, IReadOnlyList<EntryResult> Entries, IReadOnlyList<string> Lines)
{
    internal bool Passed => Entries.All(entry => entry.Passed);
}

/// <summary>
/// A resolved, construction-checked behaviour gallery: loading it creates no output. The bands are
/// coarse sanity checks on one seed each, not determinism or science gates.
/// </summary>
internal sealed class GalleryPlan
{
    private static readonly string[] EntryFields = ["name", "engine", "config", "settings", "agents", "ticks", "seed", "metrics", "expect"];
    private static readonly string[] RequiredEntryFields = ["name", "engine", "agents", "ticks", "seed", "metrics", "expect"];
    private const string SeedField = "Seed";
    private static readonly Regex Slug = new(@"\A[a-z0-9]+(-[a-z0-9]+)*\z", RegexOptions.CultureInvariant);

    private GalleryPlan(GalleryEntry[] entries) => Entries = Array.AsReadOnly(entries);

    internal IReadOnlyList<GalleryEntry> Entries { get; }

    internal static GalleryPlan Load(string path)
    {
        string absolutePath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(absolutePath)!;
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(absolutePath), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        SweepPlan.RejectDuplicates(document.RootElement, "gallery");
        Dictionary<string, JsonElement> root = Fields(document.RootElement, "gallery", ["entries"], ["entries"]);
        if (root["entries"].ValueKind != JsonValueKind.Array || root["entries"].GetArrayLength() == 0)
            throw new ArgumentException("Gallery 'entries' must be a nonempty array.");

        var entries = new List<GalleryEntry>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement element in root["entries"].EnumerateArray())
        {
            string where = $"entry {entries.Count + 1}";
            Dictionary<string, JsonElement> fields = Fields(element, where, EntryFields, RequiredEntryFields);
            string name = Text(fields["name"], where, "name");
            if (!Slug.IsMatch(name))
                throw new ArgumentException($"Gallery {where} name '{name}' must be a lowercase slug such as 'vicsek-low-noise'.");
            if (!names.Add(name))
                throw new ArgumentException($"Gallery entry name '{name}' is duplicated.");
            where = $"entry '{name}'";

            EngineDefinition definition = EngineRegistry.Find(Text(fields["engine"], where, "engine").ToLowerInvariant())
                ?? throw new ArgumentException($"Gallery {where} engine must be {EngineRegistry.NameList}.");
            int agents = Count(fields["agents"], where, "agents", minimum: 1);
            int ticks = Count(fields["ticks"], where, "ticks", minimum: 1); // 0 ticks has no final quarter to average
            if (fields["seed"].ValueKind != JsonValueKind.Number || !fields["seed"].TryGetUInt32(out uint seed) || seed > Rng.MaxSupportedSeed)
                throw new ArgumentException($"Gallery {where} seed must be an integer from 0 through {Rng.MaxSupportedSeed}.");

            string? configPath = fields.TryGetValue("config", out JsonElement config)
                ? Path.GetFullPath(Text(config, where, "config"), directory) : null;
            var overrides = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            if (fields.TryGetValue("settings", out JsonElement settings))
                foreach ((string key, JsonElement value) in Fields(settings, where + " settings", null, []))
                    overrides.Add(key, value);
            if (overrides.ContainsKey(SeedField))
                throw new ArgumentException($"Gallery {where} sets the seed in 'seed', not in 'settings'.");
            overrides.Add(SeedField, JsonSerializer.SerializeToElement(seed));

            if (fields["metrics"].ValueKind != JsonValueKind.Array || fields["metrics"].GetArrayLength() == 0)
                throw new ArgumentException($"Gallery {where} metrics must be a nonempty array of metric names.");
            string[] metrics = fields["metrics"].EnumerateArray().Select(value => Text(value, where, "metrics")).ToArray();
            if (!MetricCatalog.ValidateSelection(definition.Name, metrics))
                throw new ArgumentException($"Gallery {where} needs deterministic metrics; runtime cost channels cannot be replayed.");

            Dictionary<string, JsonElement> expect = Fields(fields["expect"], where + " expect", null, []);
            if (expect.Count == 0)
                throw new ArgumentException($"Gallery {where} expect must name at least one metric.");
            var expectations = new List<GalleryExpectation>();
            foreach ((string metric, JsonElement bounds) in expect)
            {
                if (!metrics.Contains(metric, StringComparer.Ordinal))
                    throw new ArgumentException($"Gallery {where} expects '{metric}', which the run does not record (metrics: {string.Join(", ", metrics)}).");
                Dictionary<string, JsonElement> sides = Fields(bounds, $"{where} expect '{metric}'", ["min", "max"], []);
                double? min = sides.TryGetValue("min", out JsonElement low) ? Bound(low, where, metric, "min") : null;
                double? max = sides.TryGetValue("max", out JsonElement high) ? Bound(high, where, metric, "max") : null;
                if (min is null && max is null)
                    throw new ArgumentException($"Gallery {where} expectation on '{metric}' needs a min or a max.");
                if (min > max)
                    throw new ArgumentException($"Gallery {where} expectation on '{metric}' has min above max.");
                expectations.Add(new GalleryExpectation(metric, min, max));
            }

            // Check every configuration and initial metric state before any gallery output.
            var request = new RunRequest(definition.Name, ticks, agents, definition.Resolve(null, configPath, overrides, null), null)
                { RequestedMetrics = metrics, Visual = true };
            var simulation = new PreparedSimulation(request);
            _ = new ExperimentMetrics(request, simulation);
            entries.Add(new GalleryEntry(entries.Count, name, request, expectations));
        }
        return new GalleryPlan(entries.ToArray());
    }

    private static Dictionary<string, JsonElement> Fields(JsonElement element, string where, string[]? allowed, string[] required)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new ArgumentException($"Gallery {where} must be a JSON object.");
        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (allowed is not null && !allowed.Contains(property.Name, StringComparer.Ordinal))
                throw new ArgumentException($"Gallery {where} has unknown field '{property.Name}'.");
            fields.Add(property.Name, property.Value.Clone());
        }
        foreach (string name in required)
            if (!fields.ContainsKey(name)) throw new ArgumentException($"Gallery {where} is missing required field '{name}'.");
        return fields;
    }

    private static string Text(JsonElement value, string where, string field) =>
        value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()! : throw new ArgumentException($"Gallery {where} '{field}' must be a nonempty string.");

    private static int Count(JsonElement value, string where, string field, int minimum) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int count) && count >= minimum
            ? count : throw new ArgumentException($"Gallery {where} '{field}' must be an integer of at least {minimum}.");

    private static double Bound(JsonElement value, string where, string metric, string side) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number) && double.IsFinite(number)
            ? number : throw new ArgumentException($"Gallery {where} '{metric}' {side} must be a finite number.");
}

internal static class Gallery
{
    internal static GalleryResult Run(string galleryFile, string outputDirectory) =>
        Run(GalleryPlan.Load(galleryFile), outputDirectory);

    internal static GalleryResult Run(GalleryPlan plan, string outputDirectory)
    {
        string parent = Path.GetFullPath(outputDirectory);
        string galleryId = "gallery-" + Guid.NewGuid().ToString("N");
        string staging = Path.Combine(parent, "." + galleryId + ".tmp");
        string destination = Path.Combine(parent, galleryId);
        Directory.CreateDirectory(parent);
        if (Directory.Exists(staging) || File.Exists(staging) || Directory.Exists(destination) || File.Exists(destination))
            throw new IOException("Generated gallery directory already exists; no files were changed.");
        Directory.CreateDirectory(staging);
        try
        {
            string work = Path.Combine(staging, ".entry-runs");
            var results = new List<EntryResult>();
            foreach (GalleryEntry entry in plan.Entries)
            {
                var elapsed = Stopwatch.StartNew();
                string completed = ExperimentRunner.Run(entry.Request, work);
                elapsed.Stop();
                string entryPath = Path.Combine(staging, entry.Directory);
                Directory.Move(completed, entryPath);
                string csv = File.ReadAllText(Path.Combine(entryPath, "metrics.csv"));
                results.Add(new EntryResult(entry,
                    entry.Expectations.Select(expectation => new ExpectationResult(expectation, FinalQuarterMean(csv, expectation.Metric))).ToArray(),
                    elapsed.ElapsedMilliseconds));
            }
            Directory.Delete(work); // Empty: every completed run was moved to its entry directory.
            File.WriteAllText(Path.Combine(staging, "summary.json"), SummaryJson(results), new UTF8Encoding(false));
            GalleryReport.Write(staging, results);
            Directory.Move(staging, destination);
            return new GalleryResult(destination, results, Table(results));
        }
        catch
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            throw;
        }
    }

    /// <summary>
    /// Mean of a column over the last max(1, floor(T / 4)) ticks, T being the last tick in the CSV,
    /// ignoring empty (undefined) cells. A run too short for a quarter therefore averages its final
    /// tick alone, and a window with no defined cell returns NaN, which no bound accepts.
    /// </summary>
    internal static double FinalQuarterMean(string metricsCsv, string metric)
    {
        string[] lines = metricsCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int column = lines.Length == 0 ? -1 : Array.IndexOf(lines[0].Split(','), metric);
        if (column < 1)
            throw new InvalidDataException($"metrics.csv has no column '{metric}'.");
        long lastTick = long.Parse(lines[^1].Split(',')[0], NumberStyles.None, CultureInfo.InvariantCulture);
        long window = Math.Max(1, lastTick / 4);
        double sum = 0;
        long count = 0;
        for (int row = lines.Length - 1; row >= 1; row--)
        {
            string[] cells = lines[row].Split(',');
            if (long.Parse(cells[0], NumberStyles.None, CultureInfo.InvariantCulture) <= lastTick - window) break;
            if (column < cells.Length && double.TryParse(cells[column], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value))
            {
                sum += value;
                count++;
            }
        }
        return count == 0 ? double.NaN : sum / count;
    }

    /// <summary>Console-and-page number: three decimals under 1000 are plenty for a coarse band.</summary>
    internal static string Num(double value) => double.IsNaN(value) ? "undefined"
        : Math.Abs(value) >= 1 ? value.ToString("0.###", CultureInfo.InvariantCulture) : value.ToString("G3", CultureInfo.InvariantCulture);

    private static List<string> Table(IReadOnlyList<EntryResult> results)
    {
        string[] texts = results.Select(result => string.Join("; ", result.Expectations.Select(e => e.Text))).ToArray();
        int nameWidth = results.Max(result => result.Entry.Name.Length);
        int textWidth = texts.Max(text => text.Length);
        var lines = new List<string>();
        for (int i = 0; i < results.Count; i++)
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"{(results[i].Passed ? "PASS" : "FAIL")}  {results[i].Entry.Name.PadRight(nameWidth)}  {texts[i].PadRight(textWidth)}  {results[i].ElapsedMilliseconds,6} ms"));
        int passed = results.Count(result => result.Passed);
        lines.Add(string.Create(CultureInfo.InvariantCulture,
            $"{passed}/{results.Count} passed in {results.Sum(result => result.ElapsedMilliseconds)} ms"));
        return lines;
    }

    private static string SummaryJson(IReadOnlyList<EntryResult> results)
    {
        // ponytail: wall-clock time is left out so summary.json is a pure function of the recipe on one platform.
        static JsonNode? Number(double? value) => value is { } number && double.IsFinite(number) ? JsonValue.Create(number) : null;
        var entries = new JsonArray();
        foreach (EntryResult result in results)
        {
            var expectations = new JsonArray();
            foreach (ExpectationResult expectation in result.Expectations)
                expectations.Add(new JsonObject
                {
                    ["metric"] = expectation.Expectation.Metric,
                    ["min"] = Number(expectation.Expectation.Min),
                    ["max"] = Number(expectation.Expectation.Max),
                    ["measured"] = Number(expectation.Measured),
                    ["passed"] = expectation.Passed
                });
            entries.Add(new JsonObject
            {
                ["entry"] = result.Entry.Name,
                ["engine"] = result.Entry.Request.Engine,
                ["passed"] = result.Passed,
                ["runDirectory"] = result.Entry.Directory,
                ["expectations"] = expectations
            });
        }
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["passed"] = results.All(result => result.Passed),
            ["entries"] = entries
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n") + "\n";
    }
}
