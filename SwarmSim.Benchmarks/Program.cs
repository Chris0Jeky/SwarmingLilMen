using BenchmarkDotNet.Running;

namespace SwarmSim.Benchmarks;

internal static class Program
{
    internal static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--canonical-experiment")
        {
            if (args.Length != 1) throw new ArgumentException("--canonical-experiment takes no additional arguments.");
            CanonicalExperiment.Run(Console.Out);
            return;
        }

        Console.WriteLine("SwarmSim.Benchmarks - BenchmarkDotNet suite");
        Console.WriteLine();

        if (args.Length > 0 && args[0] == "--grid")
        {
            Console.WriteLine("Running Grid benchmarks...");
            BenchmarkRunner.Run<GridBenchmarks>();
        }
        else if (args.Length > 0 && args[0] == "--tick")
        {
            Console.WriteLine("Running World Tick benchmarks...");
            BenchmarkRunner.Run<WorldTickBenchmarks>();
        }
        else if (args.Length > 0 && args[0] == "--canonical")
        {
            Console.WriteLine("Running Canonical World Step benchmarks (telemetry capture off)...");
            BenchmarkSwitcher.FromTypes(new[] { typeof(CanonicalWorldStepBenchmarks) }).RunAll(args: args[1..]);
        }
        else
        {
            Console.WriteLine("Running legacy World Tick and Grid benchmarks...");
            BenchmarkRunner.Run<WorldTickBenchmarks>();
            BenchmarkRunner.Run<GridBenchmarks>();
        }
    }
}
