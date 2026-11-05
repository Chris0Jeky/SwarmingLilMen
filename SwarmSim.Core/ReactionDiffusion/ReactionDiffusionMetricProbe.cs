using SwarmSim.Core.Metrics;

namespace SwarmSim.Core.ReactionDiffusion;

/// <summary>Ordered U/V statistics from the field. No fake kinematic population is used.</summary>
public sealed class ReactionDiffusionMetricProbe(ReactionDiffusionWorld? world) : IMetricProbe
{
    private static readonly IReadOnlyList<string> Names = Array.AsReadOnly(new[]
    {
        "reactionDiffusion.uMean", "reactionDiffusion.uMin", "reactionDiffusion.uMax",
        "reactionDiffusion.vMean", "reactionDiffusion.vMin", "reactionDiffusion.vMax"
    });

    public IReadOnlyList<string> Channels => Names;
    public bool IsDeterministic => true;

    public void Sample(in MetricFrame frame, Span<double> output)
    {
        MetricMath.RequireOutput(output, Names.Count);
        if (world is null)
            throw new InvalidOperationException("This probe holds only channel metadata.");
        double sumU = 0d, sumV = 0d;
        double minU = double.PositiveInfinity, minV = double.PositiveInfinity;
        double maxU = double.NegativeInfinity, maxV = double.NegativeInfinity;
        foreach (CellState cell in world.Cells)
        {
            sumU += cell.U;
            sumV += cell.V;
            minU = Math.Min(minU, cell.U);
            maxU = Math.Max(maxU, cell.U);
            minV = Math.Min(minV, cell.V);
            maxV = Math.Max(maxV, cell.V);
        }
        output[0] = sumU / world.CellCount;
        output[1] = minU;
        output[2] = maxU;
        output[3] = sumV / world.CellCount;
        output[4] = minV;
        output[5] = maxV;
    }
}
