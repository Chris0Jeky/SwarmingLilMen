using SwarmSim.Core.Canonical;
using SwarmSim.Core.Metrics;

namespace SwarmSim.Cli;

/// <summary>A single initialized experiment, built by its engine definition.</summary>
internal sealed class PreparedSimulation(RunRequest request)
{
    internal IHeadlessSimulation Simulation { get; } = request.Definition.Create(request);

    internal CanonicalWorld? Canonical => (Simulation as CanonicalSimulation)?.World;
    internal int MetricCapacity => Simulation.MetricCapacity;
    internal float WorldWidth => Simulation.WorldWidth;
    internal float WorldHeight => Simulation.WorldHeight;
    internal MetricDomain Domain => Simulation.Domain;
    internal void Step() => Simulation.Step();
    internal string KinematicHash() => Simulation.KinematicHash();
    internal int Capture(Span<KinematicSample> destination) => Simulation.Capture(destination);
}
