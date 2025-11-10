using BenchmarkDotNet.Running;

namespace SwarmSim.Benchmarks;

internal static class Program
{
    private static void Main(string[] args)
    {
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
        else
        {
            Console.WriteLine("Running all benchmarks...");
            BenchmarkRunner.Run<WorldTickBenchmarks>();
            BenchmarkRunner.Run<GridBenchmarks>();
        }
    }
}
