using System.Globalization;
using System.Text.Json;
using SwarmSim.Core;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Configuration;
using SwarmSim.Core.Utils;
using SwarmSim.Core.Vicsek;

namespace SwarmSim.Cli;

internal sealed record SweepCell(int Index, RunRequest Request, string[] ParameterValues)
{
    internal string Id => "cell-" + Index.ToString("D6", CultureInfo.InvariantCulture);
}

/// <summary>A fully resolved and construction-checked sweep; loading it creates no output.</summary>
internal sealed class SweepPlan
{
    internal const int MaximumCells = 10_000;
    private static readonly HashSet<string> Fields = new(StringComparer.Ordinal)
        { "base", "seeds", "grid", "ticks", "metrics", "engine", "agents" };

    private SweepPlan(string[] parameters, string[] metrics, SweepCell[] cells)
    {
        Parameters = Array.AsReadOnly(parameters);
        Metrics = Array.AsReadOnly(metrics);
        Cells = Array.AsReadOnly(cells);
    }

    internal IReadOnlyList<string> Parameters { get; }
    internal IReadOnlyList<string> Metrics { get; }
    internal IReadOnlyList<SweepCell> Cells { get; }

    internal static SweepPlan Load(string path)
    {
        string absolutePath = Path.GetFullPath(path);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(absolutePath));
        Dictionary<string, JsonElement> fields = ReadObject(document.RootElement, "sweep");
        foreach (string name in fields.Keys)
            if (!Fields.Contains(name)) throw new ArgumentException($"Unknown sweep field '{name}'.");

        string baseFile = ReadString(Require(fields, "base"), "base");
        string engine = fields.TryGetValue("engine", out JsonElement engineValue)
            ? ReadString(engineValue, "engine").ToLowerInvariant() : "canonical";
        if (engine is not ("canonical" or "legacy" or "vicsek"))
            throw new ArgumentException("Sweep engine must be canonical, legacy or vicsek.");
        int ticks = ReadCount(Require(fields, "ticks"), "ticks", allowZero: true);
        int agents = fields.TryGetValue("agents", out JsonElement agentValue)
            ? ReadCount(agentValue, "agents", allowZero: false) : 400;
        uint[] seeds = ReadSeeds(Require(fields, "seeds"));
        string[] metrics = ReadMetrics(Require(fields, "metrics"));
        if (!MetricCatalog.ValidateSelection(engine, metrics))
            throw new ArgumentException("Sweeps require deterministic metrics; runtime cost channels cannot produce a byte-repeatable summary or exact cell replay.");

        using JsonDocument schema = JsonDocument.Parse(engine switch
        {
            "vicsek" => new VicsekSettings().ToJson(),
            "canonical" => new CanonicalWorldSettings().ToJson(),
            _ => new SimConfig().ToJson()
        });
        SweepAxis[] axes = ReadAxes(Require(fields, "grid"), schema.RootElement);
        int count = seeds.Length;
        foreach (SweepAxis axis in axes)
        {
            // Bound before multiplying: even a product exceeding Int64 cannot overflow here.
            if (count > MaximumCells / axis.Values.Length)
                throw new ArgumentException($"A sweep may contain at most {MaximumCells} cells.");
            count *= axis.Values.Length;
        }

        string basePath = Path.GetFullPath(baseFile, Path.GetDirectoryName(absolutePath)!);
        using JsonDocument baseDocument = JsonDocument.Parse(File.ReadAllText(basePath), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        Dictionary<string, JsonElement> baseValues = ReadObject(baseDocument.RootElement, "base configuration");
        // Resolve the base once; do not reread a mutable file between cells.
        CanonicalWorldSettings? canonical = engine == "canonical"
            ? ConfigurationResolver.ResolveCanonicalSettings(overrides: baseValues) : null;
        SimConfig? legacy = engine == "legacy"
            ? ConfigurationResolver.ResolveSimConfig(overrides: baseValues) : null;
        VicsekSettings? vicsek = engine == "vicsek"
            ? ConfigurationResolver.ResolveVicsekSettings(overrides: baseValues) : null;

        var cells = new SweepCell[count];
        int combinations = count / seeds.Length;
        int[] selected = new int[axes.Length];
        for (int seedIndex = 0; seedIndex < seeds.Length; seedIndex++)
        {
            uint seed = seeds[seedIndex];
            for (int combination = 0; combination < combinations; combination++)
            {
                int remainder = combination;
                for (int axis = axes.Length - 1; axis >= 0; axis--)
                {
                    selected[axis] = remainder % axes[axis].Values.Length;
                    remainder /= axes[axis].Values.Length;
                }
                var overrides = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                for (int axis = 0; axis < axes.Length; axis++)
                    overrides.Add(axes[axis].Name, axes[axis].Values[selected[axis]]);
                overrides.Add(nameof(SimConfig.Seed), JsonSerializer.SerializeToElement(seed));
                RunRequest request = vicsek is not null
                    ? RunRequest.ForVicsek(ticks, agents, ConfigurationResolver.ResolveVicsekSettings(overrides: overrides, defaults: vicsek), null)
                    : canonical is not null
                    ? new(engine, ticks, agents, null, ConfigurationResolver.ResolveCanonicalSettings(overrides: overrides, defaults: canonical), null)
                    : new(engine, ticks, agents, ConfigurationResolver.ResolveSimConfig(overrides: overrides, defaults: legacy), null, null);
                request = request with { RequestedMetrics = metrics.ToArray() };

                // Check every configuration and initial metric state before any sweep output.
                // Only one world is retained during preflight; execution constructs it afresh.
                var simulation = new PreparedSimulation(request);
                _ = new ExperimentMetrics(request, simulation);
                using JsonDocument resolved = JsonDocument.Parse(request.ConfigJson);
                string[] values = axes.Select(axis => resolved.RootElement.GetProperty(axis.Name).GetRawText()).ToArray();
                int index = seedIndex * combinations + combination;
                cells[index] = new SweepCell(index, request, values);
            }
        }
        return new SweepPlan(axes.Select(axis => axis.Name).ToArray(), metrics, cells);
    }

    private static SweepAxis[] ReadAxes(JsonElement grid, JsonElement schema)
    {
        Dictionary<string, JsonElement> properties = ReadObject(grid, "grid");
        var names = schema.EnumerateObject().ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);
        var axes = new List<SweepAxis>();
        foreach ((string name, JsonElement choices) in properties)
        {
            if (!names.TryGetValue(name, out JsonProperty property))
                throw new ArgumentException($"Unknown sweep grid property '{name}' for the selected engine.");
            if (property.Name == nameof(SimConfig.Seed))
                throw new ArgumentException("Seed belongs in the sweep seeds array, not in grid.");
            if (property.Value.ValueKind != JsonValueKind.Number)
                throw new ArgumentException($"Sweep v1 grid property '{name}' must be a numeric scalar setting; structured settings belong in base.");
            if (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                throw new ArgumentException($"Grid property '{name}' must have a nonempty array of numeric values.");
            JsonElement[] values = choices.EnumerateArray().Select(value => value.Clone()).ToArray();
            foreach (JsonElement value in values)
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double number) || !double.IsFinite(number))
                    throw new ArgumentException($"Grid property '{name}' requires finite numeric values.");
            axes.Add(new SweepAxis(property.Name, values));
        }
        return axes.OrderBy(axis => axis.Name, StringComparer.Ordinal).ToArray();
    }

    private static uint[] ReadSeeds(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() == 0)
            throw new ArgumentException("Sweep seeds must be a nonempty array.");
        if (element.GetArrayLength() > MaximumCells)
            throw new ArgumentException($"A sweep may contain at most {MaximumCells} cells.");
        var seeds = new List<uint>();
        var unique = new HashSet<uint>();
        foreach (JsonElement value in element.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetUInt32(out uint seed) || seed > Rng.MaxSupportedSeed)
                throw new ArgumentException($"Sweep seeds must be integers from 0 through {Rng.MaxSupportedSeed}.");
            if (!unique.Add(seed)) throw new ArgumentException($"Sweep seed {seed} is duplicated.");
            seeds.Add(seed);
        }
        return seeds.ToArray();
    }

    private static string[] ReadMetrics(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("Sweep metrics must be an array of metric names.");
        return element.EnumerateArray().Select(value => ReadString(value, "metrics")).ToArray();
    }

    private static JsonElement Require(Dictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out JsonElement value) ? value : throw new ArgumentException($"Sweep is missing required field '{name}'.");

    private static string ReadString(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()! : throw new ArgumentException($"Sweep '{name}' must be a nonempty string.");

    private static int ReadCount(JsonElement value, string name, bool allowZero)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int count) || count < (allowZero ? 0 : 1))
            throw new ArgumentException($"Sweep '{name}' must be a {(allowZero ? "non-negative" : "positive")} integer no greater than {int.MaxValue}.");
        return count;
    }

    private static Dictionary<string, JsonElement> ReadObject(JsonElement element, string description)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new ArgumentException($"The {description} must be a JSON object.");
        RejectDuplicates(element, description);
        return element.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
    }

    private static void RejectDuplicates(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                string next = path + "." + property.Name;
                if (!names.Add(property.Name)) throw new ArgumentException($"Duplicate field '{next}'.");
                RejectDuplicates(property.Value, next);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in element.EnumerateArray()) RejectDuplicates(item, path + "[]");
    }

    private sealed record SweepAxis(string Name, JsonElement[] Values);
}
