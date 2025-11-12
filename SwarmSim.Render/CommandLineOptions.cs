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
                    if (TryGetValue(args, ref i, out var preset))
                    {
                        options.PresetName = preset;
                    }
                    break;

                case "--config":
                case "-c":
                    if (TryGetValue(args, ref i, out var configPath))
                    {
                        options.ConfigFile = configPath;
                    }
                    break;

                case "--agent-count":
                case "-n":
                    if (TryGetValue(args, ref i, out var countText) &&
                        int.TryParse(countText, out int count) &&
                        count > 0)
                    {
                        options.AgentCount = count;
                    }
                    break;

                case "--minimal":
                    options.RunMinimalTest = true;
                    break;
            }
        }

        return options;
    }

    public static string GetHelpText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Usage: SwarmSim.Render [OPTIONS]");
        sb.AppendLine();
        sb.AppendLine("Options:");
        sb.AppendLine("  -h, --help                Show this help message and exit");
        sb.AppendLine("  -v, --version             Show version information");
        sb.AppendLine("  -l, --list-presets        List built-in presets and exit");
        sb.AppendLine("  -p, --preset NAME         Load preset configuration (e.g., peaceful, warbands)");
        sb.AppendLine("  -c, --config FILE         Load configuration from JSON file");
        sb.AppendLine("  -n, --agent-count N       Override initial agent count (default: 400)");
        sb.AppendLine("  -b, --benchmark           Run in headless benchmark mode (no window)");
        sb.AppendLine("      --canonical           Launch the single-group canonical boids renderer");
        sb.AppendLine("      --minimal             Launch the minimal debugging harness");
        sb.AppendLine();
        sb.AppendLine("Examples:");
        sb.AppendLine("  SwarmSim.Render");
        sb.AppendLine("  SwarmSim.Render --preset peaceful");
        sb.AppendLine("  SwarmSim.Render --config configs/warbands.json -n 5000");
        sb.AppendLine("  SwarmSim.Render --benchmark --agent-count 20000");
        sb.AppendLine();
        sb.AppendLine("Interactive Controls:");
        sb.AppendLine("  Press H inside the application to toggle the help overlay.");
        sb.AppendLine("  Press F12 to view snapshot/debug information.");
        return sb.ToString();
    }

    private static bool TryGetValue(string[] args, ref int index, out string value)
    {
        if (index + 1 < args.Length)
        {
            value = args[++index];
            return true;
        }

        value = string.Empty;
        return false;
    }
}
