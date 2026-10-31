using SwarmSim.Core.Metrics;

namespace SwarmSim.Core.Aco;

/// <summary>
/// World-state channels the kinematic frame cannot carry. The frame is unused: food and the field
/// are not agents, so this reads the world directly and allocates nothing. aco.foodReturned and
/// aco.carrying are exact integers; aco.fieldTotal is the ordered sum of all cells. A probe built
/// without a world is metadata for discovery and refuses to sample.
/// </summary>
public sealed class AntColonyMetricProbe(AntColonyWorld? world) : IMetricProbe
{
    private static readonly IReadOnlyList<string> Names = Array.AsReadOnly(new[]
        { "aco.foodReturned", "aco.carrying", "aco.fieldTotal" });

    public IReadOnlyList<string> Channels => Names;
    public bool IsDeterministic => true;

    public void Sample(in MetricFrame frame, Span<double> output)
    {
        MetricMath.RequireOutput(output, Names.Count);
        if (world is null)
            throw new InvalidOperationException("This probe holds only channel metadata.");
        output[0] = world.FoodReturned;
        output[1] = world.CarryingCount;
        output[2] = world.Field.Total();
    }
}
