namespace SwarmSim.Core.Simulation;

/// <summary>One named, synchronous phase. Actions are retained once at world construction.</summary>
public sealed class SimulationPhase
{
    public SimulationPhase(string name, Action execute)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A simulation phase needs a nonempty name.", nameof(name));
        ArgumentNullException.ThrowIfNull(execute);
        Name = name;
        Execute = execute;
    }

    public string Name { get; }
    internal Action Execute { get; }
}

/// <summary>
/// A fixed, ordered sequence of world phases. Each run executes every phase once, in registration
/// order, without allocating. The model owns state, timestep, tick count and commit boundaries.
/// Instances are single-threaded; recursive runs are rejected. A failing phase stops the run and
/// propagates its exception: completed writes are not rolled back, and recovery belongs to the model.
/// </summary>
public sealed class SimulationSchedule
{
    private readonly SimulationPhase[] _phases;
    private bool _running;

    public SimulationSchedule(params SimulationPhase[] phases)
    {
        ArgumentNullException.ThrowIfNull(phases);
        if (phases.Length == 0)
            throw new ArgumentException("A simulation schedule needs at least one phase.", nameof(phases));
        _phases = (SimulationPhase[])phases.Clone();
        var names = new string[phases.Length];
        var unique = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < _phases.Length; i++)
        {
            SimulationPhase phase = _phases[i]
                ?? throw new ArgumentException("A simulation phase cannot be null.", nameof(phases));
            if (!unique.Add(phase.Name))
                throw new ArgumentException($"Duplicate simulation phase '{phase.Name}'.", nameof(phases));
            names[i] = phase.Name;
        }
        PhaseNames = Array.AsReadOnly(names);
    }

    /// <summary>Execution order, fixed at construction; inspection cannot alter registration.</summary>
    public IReadOnlyList<string> PhaseNames { get; }

    public void Run()
    {
        if (_running)
            throw new InvalidOperationException("A simulation schedule cannot run recursively.");
        _running = true;
        try
        {
            for (int i = 0; i < _phases.Length; i++)
                _phases[i].Execute();
        }
        finally
        {
            _running = false;
        }
    }
}
