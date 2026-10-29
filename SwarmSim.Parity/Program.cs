namespace SwarmSim.Parity;

internal static class Program
{
    public static int Main(string[] args) => ParityCommand.Run(args, Console.Out, Console.Error);
}
