using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using SwarmSim.Core;
using SwarmSim.Core.Utils;

namespace SwarmSim.Parity;

public static class ParityCommand
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            if (args.Length == 0 || args[0] is not ("run" or "record" or "verify"))
                throw new ArgumentException("Usage: run|record|verify --out NEW_DIRECTORY [--baseline FILE] [run only: --scenario ID --seed SEED --model MODEL] [verify only: --alignment-off]");
            string command = args[0];
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 1; i < args.Length; i++)
            {
                string key = args[i];
                if (key == "--alignment-off")
                {
                    if (!options.TryAdd(key, "true")) throw new ArgumentException("Duplicate argument.");
                    continue;
                }
                if (key is not ("--out" or "--baseline" or "--scenario" or "--seed" or "--model") ||
                    ++i == args.Length || !options.TryAdd(key, args[i]))
                    throw new ArgumentException("Unknown, duplicate or incomplete argument.");
            }
            bool alignmentDisabled = options.ContainsKey("--alignment-off");
            if (alignmentDisabled && command != "verify") throw new ArgumentException("--alignment-off is an explicit verify mutation.");
            if (command != "run" && options.Keys.Any(x => x is "--scenario" or "--seed" or "--model"))
                throw new ArgumentException("Recording/verification requires the entire fixed matrix.");
            if (command == "run" && options.ContainsKey("--baseline")) throw new ArgumentException("Run does not consume a baseline.");
            if (!options.TryGetValue("--out", out string? directory) || string.IsNullOrWhiteSpace(directory) ||
                Directory.Exists(directory) || File.Exists(directory)) throw new ArgumentException("--out must name a new directory.");
            string[] models = options.TryGetValue("--model", out string? chosenModel) ? new[] { chosenModel } : ParityRunner.Models.ToArray();
            if (models.Any(x => !ParityRunner.Models.Contains(x))) throw new ArgumentException("Unknown model.");
            uint[] seeds = ParityRunner.Seeds.ToArray();
            if (options.TryGetValue("--seed", out string? chosenSeed))
            {
                if (!uint.TryParse(chosenSeed, NumberStyles.None, CultureInfo.InvariantCulture, out uint seed) || seed > Rng.MaxSupportedSeed)
                    throw new ArgumentException("Seed is outside the supported unsigned range.");
                seeds = new[] { seed };
            }
            ScenarioRecipe[] recipes = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "scenarios"), "*.json")
                .OrderBy(x => x, StringComparer.Ordinal).Select(x => ScenarioRecipe.FromJson(File.ReadAllText(x))).ToArray();
            ParityBaseline.ValidateSuite(recipes);
            if (options.TryGetValue("--scenario", out string? chosenScenario))
            {
                recipes = recipes.Where(x => x.Id == chosenScenario).ToArray();
                if (recipes.Length != 1) throw new ArgumentException("Unknown scenario.");
            }
            string? baselinePath = options.GetValueOrDefault("--baseline");
            ParityBaseline? baseline = null;
            if (command != "run")
            {
                if (string.IsNullOrWhiteSpace(baselinePath)) throw new ArgumentException("Recording/verification requires an explicit --baseline file.");
                if (command == "record" && (File.Exists(baselinePath) || Directory.Exists(baselinePath)))
                    throw new ArgumentException("Recording never replaces an existing baseline; choose a new candidate path.");
                if (command == "verify") baseline = ParityBaseline.FromJson(File.ReadAllText(baselinePath), recipes);
            }
            // Validate generated inputs before creating even the experiment directory.
            foreach (ScenarioRecipe recipe in recipes) foreach (uint seed in seeds) recipe.Generate(seed);
            Directory.CreateDirectory(directory);
            var metadata = new
            {
                Version = 1, Command = command, Contract = ParityBaseline.CurrentContract,
                Recipes = recipes, Seeds = seeds, Models = models, Channels = ParityRunner.Channels,
                RequestedAndPostIngestInitialStatesAreRecordedSeparately = true,
                LegacyCandidateCapacity = 256, CanonicalCandidateCapacity = "whole population",
                Runtime = RuntimeInformation.FrameworkDescription, Platform = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(), AlignmentDisabled = alignmentDisabled,
                ImplementationHashes = new Dictionary<string, string>
                {
                    ["core"] = FileHash(typeof(World).Assembly.Location), ["parity"] = FileHash(typeof(ParityCommand).Assembly.Location)
                }
            };
            File.WriteAllText(Path.Combine(directory, "experiment.json"), JsonSerializer.Serialize(metadata, Json) + "\n");
            var results = new List<ParityRunResult>();
            var exactErrors = new List<string>();
            var clock = Stopwatch.StartNew();
            foreach (ScenarioRecipe recipe in recipes)
                foreach (uint seed in seeds)
                    foreach (string model in models)
                    {
                        double start = clock.Elapsed.TotalSeconds;
                        bool mutate = alignmentDisabled && model == "classic";
                        ParityRunResult result = ParityRunner.Run(recipe, seed, model,
                            Path.Combine(directory, recipe.Id, seed.ToString(CultureInfo.InvariantCulture), model), alignmentDisabled: mutate);
                        results.Add(result);
                        output.WriteLine(FormattableString.Invariant($"{recipe.Id} seed={seed} model={model} seconds={clock.Elapsed.TotalSeconds - start:F3}"));
                        output.Flush();
                        if (command == "verify" && !alignmentDisabled)
                        {
                            ParityRunResult repeat = ParityRunner.Run(recipe, seed, model);
                            if (!ExactSame(result, repeat)) exactErrors.Add($"Repeat mismatch: {recipe.Id}/{seed}/{model}.");
                            if (model is "classic" or "enhanced")
                            {
                                ParityRunResult naive = ParityRunner.Run(recipe, seed, model, naiveIndex: true);
                                if (!ExactSame(result, naive)) exactErrors.Add($"Spatial-index mismatch: {recipe.Id}/{seed}/{model}.");
                            }
                        }
                    }
            File.WriteAllText(Path.Combine(directory, "summary.json"), JsonSerializer.Serialize(results, Json) + "\n");
            string timing = FormattableString.Invariant($"Completed {results.Count} primary runs in {clock.Elapsed.TotalSeconds:F3}s.");
            output.WriteLine(timing);
            File.WriteAllText(Path.Combine(directory, "log.txt"), timing + "\n");
            if (command == "record")
            {
                ParityBaseline candidate = ParityBaseline.Create(recipes, results.ToArray());
                using var stream = new FileStream(baselinePath!, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                JsonSerializer.Serialize(stream, candidate, Json);
                output.WriteLine("Recorded a candidate calibration baseline; this does not prove repeat/index checks or the alignment-off witness.");
            }
            else if (command == "verify")
            {
                exactErrors.AddRange(baseline!.Compare(recipes, results.ToArray()));
                if (exactErrors.Count > 0)
                {
                    File.AppendAllText(Path.Combine(directory, "log.txt"), string.Join('\n', exactErrors) + "\n");
                }
                return WriteVerificationResult(exactErrors, alignmentDisabled, output, error);
            }
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or JsonException or InvalidOperationException)
        {
            error.WriteLine(exception.Message);
            return 2;
        }
    }

    private static bool ExactSame(ParityRunResult a, ParityRunResult b) =>
        a.RequestedInitialHash == b.RequestedInitialHash && a.InitialHash == b.InitialHash &&
        a.FinalHash == b.FinalHash && a.StateTraceHash == b.StateTraceHash && a.CsvHash == b.CsvHash;

    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    internal static int WriteVerificationResult(IReadOnlyList<string> errors, bool alignmentDisabled, TextWriter output, TextWriter error)
    {
        if (errors.Count > 0)
        {
            foreach (string failure in errors) error.WriteLine(failure);
            if (alignmentDisabled && !errors.Any(x => x.Contains(" polarization:", StringComparison.Ordinal)))
            {
                output.WriteLine("Alignment-off changed other metrics, but the polarization witness is NOT established.");
                return 3;
            }
            return 1;
        }
        output.WriteLine(alignmentDisabled
            ? "Alignment-off did not violate an envelope. The requested mutation witness is NOT established."
            : "Current-build repeat/index checks and locked metric envelopes passed; historical cross-platform hashes were not compared.");
        return alignmentDisabled ? 3 : 0;
    }
}
