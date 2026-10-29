using System.Globalization;
using System.Text;

namespace SwarmSim.Render;

/// <summary>
/// Simple command-line parser for SwarmSim.Render. Keeps dependencies minimal and
/// focuses on discoverability (matching DeveloperExperienceImprovementsPlan).
/// </summary>
    public sealed class CommandLineOptions
    {
        public bool ShowHelp { get; private set; }
        public bool ShowVersion { get; private set; }
        public bool ListPresets { get; private set; }
        public bool BenchmarkMode { get; private set; }
        public bool RunMinimalTest { get; private set; }
        public string? PresetName { get; private set; }
        public string? ConfigFile { get; private set; }
        public int? AgentCount { get; private set; }
        public bool UseCanonicalMode { get; private set; }

        /// <summary>First usage error (missing, empty or malformed option value, unrecognised option, unexpected argument); null when the arguments parsed cleanly.</summary>
        public string? Error { get; private set; }

    public static CommandLineOptions Parse(string[] args)
    {
        var options = new CommandLineOptions();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "--help":
                case "-h":
                case "/?":
                    options.ShowHelp = true;
                    break;

                case "--version":
                case "-v":
                    options.ShowVersion = true;
                    break;

                case "--list-presets":
                case "-l":
                    options.ListPresets = true;
                    break;

                case "--benchmark":
                case "-b":
                    options.BenchmarkMode = true;
                    break;

                case "--canonical":
                    options.UseCanonicalMode = true;
                    break;

                case "--preset":
                case "-p":
                    if (!TryGetValue(args, ref i, out var preset))
                    {
                        options.Fail($"{arg} requires a value.");
                    }
                    else if (string.IsNullOrWhiteSpace(preset))
                    {
                        options.Fail($"{arg} requires a non-empty value.");
                    }
                    else
                    {
                        options.PresetName = preset;
                    }
                    break;

                case "--config":
                case "-c":
                    if (!TryGetValue(args, ref i, out var configPath))
                    {
                        options.Fail($"{arg} requires a value.");
                    }
                    else if (string.IsNullOrWhiteSpace(configPath))
                    {
                        options.Fail($"{arg} requires a non-empty value.");
                    }
                    else
                    {
                        options.ConfigFile = configPath;
                    }
                    break;

                case "--agent-count":
                case "-n":
                    if (!TryGetValue(args, ref i, out var countText))
                    {
                        options.Fail($"{arg} requires a value.");
                    }
                    else if (int.TryParse(countText, CountStyles, CultureInfo.InvariantCulture, out int count) &&
                             count > 0)
                    {
                        options.AgentCount = count;
                    }
                    else
                    {
                        options.Fail($"Invalid value '{countText}' for {arg}: expected a positive integer.");
                    }
                    break;

                case "--minimal":
                    options.RunMinimalTest = true;
                    break;

                default:
                    // The program takes no positional arguments, so anything unmatched is a mistake
                    // (typically a mistyped option) that must not silently run with defaults.
                    options.Fail(arg.StartsWith('-')
                        ? $"Unrecognised option '{arg}'."
                        : $"Unexpected argument '{arg}'.");
                    break;
            }
        }

        return options;
    }

    public static string GetHelpText()
    {
        // Derived from the runtime registry: every preset named below must resolve through the
        // same lookup Program.Main uses, so the help text cannot advertise unusable names.
        var presetExamples = string.Join(", ", Program.PresetIds.Take(2));
        var presetExample = Program.PresetIds[0];

        var sb = new StringBuilder();
        sb.AppendLine("Usage: SwarmSim.Render [OPTIONS]");
        sb.AppendLine();
        sb.AppendLine("Options:");
        sb.AppendLine("  -h, --help                Show this help message and exit");
        sb.AppendLine("  -v, --version             Show version information");
        sb.AppendLine("  -l, --list-presets        List built-in presets and exit");
        sb.AppendLine($"  -p, --preset NAME         Load a registered preset (e.g., {presetExamples})");
        sb.AppendLine("  -c, --config FILE         Load configuration from JSON file (e.g., configs/peaceful.json)");
        sb.AppendLine("  -n, --agent-count N       Override initial agent count (default: 400)");
        sb.AppendLine("  -b, --benchmark           Print migration guidance for SwarmSim.Cli (deprecated)");
        sb.AppendLine("      --canonical           Launch the single-group canonical boids renderer");
        sb.AppendLine("      --minimal             Launch the minimal debugging harness");
        sb.AppendLine();
        sb.AppendLine("Examples:");
        sb.AppendLine("  SwarmSim.Render");
        sb.AppendLine($"  SwarmSim.Render --preset {presetExample}");
        sb.AppendLine("  SwarmSim.Render --config configs/warbands.json -n 5000");
        sb.AppendLine("  dotnet run --project SwarmSim.Cli -- --engine canonical --ticks 600");
        sb.AppendLine();
        sb.AppendLine("Legacy interactive controls (default renderer):");
        sb.AppendLine(Program.Controls.FormatHelp(RendererMode.Legacy, SwarmSim.Core.SpeedModel.ConstantSpeed));
        sb.AppendLine();
        sb.AppendLine("Canonical interactive controls (--canonical):");
        sb.AppendLine(Program.Controls.FormatHelp(RendererMode.Canonical, SwarmSim.Core.SpeedModel.ConstantSpeed));
        return sb.ToString();
    }

    // Leading/trailing white (incl. a captured CR) and a leading sign; no thousands separators or exponents.
    private const NumberStyles CountStyles =
        NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowLeadingSign;

    // Every token Parse matches by name. A value-taking option followed by one of these has lost its value.
    private static readonly HashSet<string> KnownOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "--help", "-h", "/?", "--version", "-v", "--list-presets", "-l", "--benchmark", "-b",
        "--canonical", "--preset", "-p", "--config", "-c", "--agent-count", "-n", "--minimal"
    };

    private void Fail(string message) => Error ??= message;

    private static bool TryGetValue(string[] args, ref int index, out string value)
    {
        if (index + 1 < args.Length && !KnownOptions.Contains(args[index + 1]))
        {
            value = args[++index];
            return true;
        }

        value = string.Empty;
        return false;
    }
}
