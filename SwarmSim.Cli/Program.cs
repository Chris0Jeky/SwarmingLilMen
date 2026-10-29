using System.Text.Json;

namespace SwarmSim.Cli;

public static class Program
{
    public static int Main(string[] args) => Execute(args, Console.Out, Console.Error);

    internal static int Execute(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            RunOptions options = RunOptions.Parse(args);
            if (options.Help)
            {
                output.WriteLine("""
                    SwarmSim.Cli — deterministic headless experiments
                    Usage: dotnet run --project SwarmSim.Cli -- [options]

                      --engine legacy|canonical  Engine and native config schema (default: canonical)
                      --scenario NAME            Shared named scenario before file/seed overrides
                      --config FILE              Native engine JSON configuration
                      --seed N                   Seed in 0..2147483647 (default: resolved config)
                      --ticks N                  Non-negative logical step count (default: 600)
                      --agents N                 Positive initial population (default: 400)
                      --out DIRECTORY            Parent for a new run directory (default: runs)
                      --manifest FILE            Replay recorded settings; only --out may override
                      --help                     Show this help

                    Outputs: manifest.json, metrics.csv, log.txt in a new run-<id> child.
                    Native canonical files use TargetSpeed; legacy files use MaxSpeed.
                    Exit codes: 0 success, 1 run/config/replay/I/O failure, 2 invalid usage.
                    """);
                return 0;
            }
            string directory = ExperimentRunner.Run(options);
            output.WriteLine(directory);
            return 0;
        }
        catch (UsageException failure)
        {
            error.WriteLine($"{failure.Message} Use --help for usage.");
            return 2;
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException or
            IOException or InvalidDataException or UnauthorizedAccessException or JsonException or NotSupportedException or OverflowException)
        {
            error.WriteLine($"Run failed: {failure.Message}");
            return 1;
        }
    }
}
