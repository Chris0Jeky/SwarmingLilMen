using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwarmSim.Parity;

public sealed record ParityEnvelope([property: JsonRequired] string Scenario, [property: JsonRequired] string Channel,
    [property: JsonRequired] double AbsoluteTolerance, [property: JsonRequired] double MaximumObservedSeedDifference,
    [property: JsonRequired] double MeanObservedDifference);

public sealed record ParityBaseline([property: JsonRequired] int Version, [property: JsonRequired] string Contract,
    [property: JsonRequired] string Platform, [property: JsonRequired] string Runtime, [property: JsonRequired] string Architecture,
    [property: JsonRequired] string[] Models, [property: JsonRequired] uint[] Seeds, [property: JsonRequired] string[] Channels,
    [property: JsonRequired] Dictionary<string, string> RecipeHashes, [property: JsonRequired] ParityRunResult[] Calibration,
    [property: JsonRequired] ParityEnvelope[] Envelopes)
{
    public const string CurrentContract = "parity-v1/reynolds-f32-periodic-v1/sha256-pcg32-v1";
    private static readonly (string Id, string Layout, int Count, int Ticks)[] Inventory = {
        ("S1_TWO_BOID_HEAD_ON", "head-on", 2, 240), ("S2_RING_16", "ring", 16, 600),
        ("S3_TWO_LINE_CROSSING", "crossing", 32, 400), ("S4_UNIFORM_RANDOM_512", "uniform", 512, 1800),
        ("S5_DENSE_CLUMP_256", "clump", 256, 900) };
    private static readonly string[] ComparedScenarios = { "S2_RING_16", "S4_UNIFORM_RANDOM_512", "S5_DENSE_CLUMP_256" };
    private static readonly string[] ComparedChannels = { "polarization", "nearestNeighborMean", "nearestNeighborP50" };

    public static ParityBaseline FromJson(string json, IReadOnlyList<ScenarioRecipe> recipes)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        RejectDuplicateProperties(document.RootElement);
        var value = JsonSerializer.Deserialize<ParityBaseline>(json, new JsonSerializerOptions
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new JsonException("A parity baseline cannot be null.");
        try { value.Validate(recipes); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        { throw new JsonException(error.Message, error); }
        return value;
    }

    public static ParityBaseline Create(IReadOnlyList<ScenarioRecipe> recipes, ParityRunResult[] results)
    {
        ValidateMatrix(recipes, results, verifyInitialIdentities: true);
        return new(1, CurrentContract, RuntimeInformation.OSDescription, RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            ParityRunner.Models.ToArray(), ParityRunner.Seeds.ToArray(), ParityRunner.Channels.ToArray(),
            recipes.ToDictionary(x => x.Id, RecipeHash, StringComparer.Ordinal), results, BuildEnvelopes(recipes, results));
    }

    public IReadOnlyList<string> Compare(IReadOnlyList<ScenarioRecipe> recipes, ParityRunResult[] results)
    {
        Validate(recipes);
        ValidateMatrix(recipes, results, verifyInitialIdentities: true);
        var errors = new List<string>();
        foreach (ParityEnvelope envelope in Envelopes)
        {
            int channel = Array.IndexOf(ParityRunner.Channels.ToArray(), envelope.Channel);
            double difference = Mean(results, envelope.Scenario, "classic", channel) - Mean(results, envelope.Scenario, "reference", channel);
            if (!double.IsFinite(difference)) throw new ArgumentException("An ensemble difference is not finite.");
            if (Math.Abs(difference) > envelope.AbsoluteTolerance)
                errors.Add(FormattableString.Invariant($"{envelope.Scenario} {envelope.Channel}: |classic-reference|={Math.Abs(difference):R} exceeds {envelope.AbsoluteTolerance:R}."));
        }
        return errors;
    }

    public static string RecipeHash(ScenarioRecipe recipe) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(recipe))));

    public static void ValidateSuite(IReadOnlyList<ScenarioRecipe> recipes)
    {
        if (recipes.Count != Inventory.Length) throw new ArgumentException("The baseline requires all five prescribed scenarios.");
        for (int i = 0; i < Inventory.Length; i++)
        {
            ScenarioRecipe recipe = recipes[i];
            recipe.Validate();
            (string id, string layout, int count, int ticks) = Inventory[i];
            if (recipe.Id != id || recipe.Layout != layout || recipe.Count != count || recipe.Ticks != ticks)
                throw new ArgumentException("Scenario identity, layout, population, duration and order must match the five-case contract.");
        }
    }

    private void Validate(IReadOnlyList<ScenarioRecipe> recipes)
    {
        if (Version != 1 || Contract != CurrentContract || string.IsNullOrWhiteSpace(Platform) || string.IsNullOrWhiteSpace(Runtime) ||
            string.IsNullOrWhiteSpace(Architecture) ||
            Models is null || Seeds is null || Channels is null || RecipeHashes is null || Calibration is null || Envelopes is null ||
            !Models.SequenceEqual(ParityRunner.Models) || !Seeds.SequenceEqual(ParityRunner.Seeds) || !Channels.SequenceEqual(ParityRunner.Channels))
            throw new ArgumentException("Baseline version, contract or complete ordered inventories do not match.");
        bool matchingPlatform = Platform == RuntimeInformation.OSDescription && Runtime == RuntimeInformation.FrameworkDescription &&
            Architecture == RuntimeInformation.ProcessArchitecture.ToString();
        ValidateMatrix(recipes, Calibration, verifyInitialIdentities: matchingPlatform);
        if (RecipeHashes.Count != recipes.Count || recipes.Any(x => !RecipeHashes.TryGetValue(x.Id, out string? hash) || hash != RecipeHash(x)))
            throw new ArgumentException("Baseline recipe identities do not match these inputs.");
        ParityEnvelope[] expected = BuildEnvelopes(recipes, Calibration);
        if (!Envelopes.SequenceEqual(expected)) throw new ArgumentException("Envelopes do not match the fixed calibration method and complete seed distribution.");
    }

    private static void ValidateMatrix(IReadOnlyList<ScenarioRecipe> recipes, ParityRunResult[] results, bool verifyInitialIdentities)
    {
        ValidateSuite(recipes);
        if (results is null || results.Length != recipes.Count * ParityRunner.Seeds.Count * ParityRunner.Models.Count)
            throw new ArgumentException("The comparison requires every scenario, seed and model exactly once.");
        int index = 0;
        foreach (ScenarioRecipe recipe in recipes)
            foreach (uint seed in ParityRunner.Seeds)
            {
                string? requested = null;
                foreach (string model in ParityRunner.Models)
                {
                    ParityRunResult result = results[index++];
                    int window = recipe.Ticks - recipe.Ticks * 4 / 5;
                    if (result is null || result.Scenario != recipe.Id || result.Seed != seed || result.Model != model ||
                        result.WindowMeans is null || result.WindowFiniteCounts is null ||
                        result.WindowMeans.Length != ParityRunner.Channels.Count || result.WindowFiniteCounts.Length != ParityRunner.Channels.Count ||
                        !IsHash(result.RequestedInitialHash) || !IsHash(result.InitialHash) || !IsHash(result.FinalHash) ||
                        !IsHash(result.StateTraceHash) || !IsHash(result.CsvHash))
                        throw new ArgumentException("Result identities, metric schema or ordering are incomplete.");
                    requested ??= result.RequestedInitialHash;
                    if (requested != result.RequestedInitialHash) throw new ArgumentException("Models did not receive the same requested initial state.");
                    if (verifyInitialIdentities)
                    {
                        (string expectedRequested, string expectedActual) = ParityRunner.InitialIdentities(recipe, seed, model);
                        if (result.RequestedInitialHash != expectedRequested || result.InitialHash != expectedActual)
                            throw new ArgumentException("A result's requested or post-ingest identity does not match its recipe, seed and model.");
                    }
                    for (int channel = 0; channel < result.WindowMeans.Length; channel++)
                    {
                        double? value = result.WindowMeans[channel];
                        int count = result.WindowFiniteCounts[channel];
                        if (count < 0 || count > window || (count == 0) != !value.HasValue || value.HasValue && !double.IsFinite(value.Value))
                            throw new ArgumentException("A window mean has invalid finite-sample coverage.");
                        if (channel is not (6 or 7 or 8 or 9) && count != window)
                            throw new ArgumentException("Every defined metric must have complete finite window coverage.");
                        if (value.HasValue)
                        {
                            double maximum = channel switch
                            {
                                0 => recipe.Count,
                                1 or 14 or 15 => recipe.TargetSpeed * 1.0001,
                                2 or 5 => 1,
                                3 or 4 or 9 => Math.Sqrt((double)recipe.Width * recipe.Width + (double)recipe.Height * recipe.Height) * 0.5,
                                6 or 7 or 8 => Math.PI / recipe.DeltaTime,
                                10 or 11 or 12 => recipe.Count - 1,
                                13 => recipe.Count * (recipe.Count - 1) * 0.5,
                                _ => throw new ArgumentException("Unknown metric index.")
                            };
                            if (value.Value < 0 || value.Value > maximum || channel == 0 && value.Value != recipe.Count)
                                throw new ArgumentException("A metric mean is outside its physical range.");
                        }
                    }
                }
            }
    }

    private static ParityEnvelope[] BuildEnvelopes(IReadOnlyList<ScenarioRecipe> recipes, ParityRunResult[] rows)
    {
        var output = new List<ParityEnvelope>();
        foreach (string scenario in ComparedScenarios)
            foreach (string channelName in ComparedChannels)
            {
                int channel = Array.IndexOf(ParityRunner.Channels.ToArray(), channelName);
                double maximum = 0, differenceSum = 0;
                foreach (uint seed in ParityRunner.Seeds)
                {
                    double classic = rows.Single(x => x.Scenario == scenario && x.Seed == seed && x.Model == "classic").WindowMeans[channel]!.Value;
                    double reference = rows.Single(x => x.Scenario == scenario && x.Seed == seed && x.Model == "reference").WindowMeans[channel]!.Value;
                    double difference = classic - reference;
                    if (!double.IsFinite(difference)) throw new ArgumentException("A calibration difference is not finite.");
                    maximum = Math.Max(maximum, Math.Abs(difference)); differenceSum += difference;
                }
                bool polarization = channelName == "polarization";
                double tolerance = Math.Max(polarization ? 0.02 : 0.25, Math.Ceiling(maximum * 1.25 * 1e6) / 1e6);
                if (!double.IsFinite(tolerance) || !double.IsFinite(differenceSum)) throw new ArgumentException("A calibration aggregate is not finite.");
                double ceiling = polarization ? 0.2 : recipes.Single(x => x.Id == scenario).SenseRadius * 0.1;
                if (tolerance > ceiling)
                    throw new InvalidOperationException(FormattableString.Invariant($"Calibration is not tight enough: {scenario} {channelName} needs {tolerance:R}, above the predeclared {ceiling:R} ceiling."));
                output.Add(new(scenario, channelName, tolerance, maximum, differenceSum / ParityRunner.Seeds.Count));
            }
        return output.ToArray();
    }

    private static double Mean(ParityRunResult[] rows, string scenario, string model, int channel)
    {
        double value = rows.Where(x => x.Scenario == scenario && x.Model == model).Average(x => x.WindowMeans[channel]!.Value);
        return double.IsFinite(value) ? value : throw new ArgumentException("An ensemble mean is not finite.");
    }

    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!seen.Add(property.Name)) throw new JsonException($"Duplicate baseline property '{property.Name}'.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (JsonElement value in element.EnumerateArray()) RejectDuplicateProperties(value);
    }
}
