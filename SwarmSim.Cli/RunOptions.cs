using System.Globalization;
using SwarmSim.Core.Configuration;
using SwarmSim.Core.Utils;

namespace SwarmSim.Cli;

internal sealed class UsageException(string message) : Exception(message) { }

internal sealed class RunOptions
{
    internal string Engine { get; private set; } = "canonical";
    internal string? ConfigPath { get; private set; }
    internal string? Scenario { get; private set; }
    internal string? ManifestPath { get; private set; }
    internal string? SweepPath { get; private set; }
    internal uint? Seed { get; private set; }
    internal int Ticks { get; private set; } = 600;
    internal int AgentCount { get; private set; } = 400;
    internal string OutputDirectory { get; private set; } = "runs";
    internal string[] RequestedMetrics { get; private set; } = MetricCatalog.DefaultMetrics.ToArray();
    internal bool ListMetrics { get; private set; }
    internal bool Help { get; private set; }

    internal static RunOptions Parse(string[] args)
    {
        var options = new RunOptions();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (!seen.Add(option))
                throw new UsageException($"Option '{option}' was specified more than once.");
            if (option is "--help" or "--list-metrics")
            {
                if (option == "--help") options.Help = true;
                else options.ListMetrics = true;
                continue;
            }
            if (option is not ("--engine" or "--config" or "--scenario" or "--manifest" or "--sweep" or "--seed" or "--ticks" or "--agents" or "--out" or "--metrics"))
                throw new UsageException($"Unknown option or argument '{option}'.");
            if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]) || args[index].StartsWith("--", StringComparison.Ordinal))
                throw new UsageException($"Option '{option}' requires a value.");
            string value = args[index];
            switch (option)
            {
                case "--engine":
                    options.Engine = value.ToLowerInvariant();
                    if (options.Engine is not ("legacy" or "canonical" or "vicsek"))
                        throw new UsageException("--engine must be legacy, canonical or vicsek.");
                    break;
                case "--config": options.ConfigPath = value; break;
                case "--scenario":
                    if (!ConfigurationResolver.ScenarioNames.Contains(value, StringComparer.OrdinalIgnoreCase))
                        throw new UsageException($"Unknown scenario '{value}'.");
                    options.Scenario = value;
                    break;
                case "--manifest": options.ManifestPath = value; break;
                case "--sweep": options.SweepPath = value; break;
                case "--out": options.OutputDirectory = value; break;
                case "--metrics":
                    options.RequestedMetrics = value.Split(',', StringSplitOptions.TrimEntries);
                    if (options.RequestedMetrics.Any(string.IsNullOrEmpty))
                        throw new UsageException("--metrics requires comma-separated names with no empty entries.");
                    break;
                case "--seed":
                    if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out uint seed) || seed > Rng.MaxSupportedSeed)
                        throw new UsageException($"--seed must be an integer from 0 through {Rng.MaxSupportedSeed}.");
                    options.Seed = seed;
                    break;
                case "--ticks": options.Ticks = ParseCount(option, value, allowZero: true); break;
                case "--agents": options.AgentCount = ParseCount(option, value, allowZero: false); break;
            }
        }

        if (options.Engine == "vicsek" && options.Scenario is not null)
            throw new UsageException("--scenario names boids recipes; the vicsek engine takes --config.");
        if (options.SweepPath is not null && seen.Any(option =>
            option is not ("--sweep" or "--out" or "--help")))
            throw new UsageException("--sweep can be combined only with --out or --help; all run settings belong in the sweep file.");
        if (options.ManifestPath is not null && seen.Any(option =>
            option is not ("--manifest" or "--out" or "--help")))
            throw new UsageException("--manifest can be combined only with --out or --help; run-setting overrides are not accepted during replay.");
        if (options.ListMetrics && seen.Any(option => option is not ("--list-metrics" or "--engine" or "--help")))
            throw new UsageException("--list-metrics can be combined only with --engine or --help.");
        return options;
    }

    private static int ParseCount(string option, string value, bool allowZero)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count < (allowZero ? 0 : 1))
            throw new UsageException($"{option} must be a {(allowZero ? "non-negative" : "positive")} integer no greater than {int.MaxValue}.");
        return count;
    }
}
