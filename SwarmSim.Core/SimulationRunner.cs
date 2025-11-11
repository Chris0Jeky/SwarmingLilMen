namespace SwarmSim.Core;

/// <summary>
/// Fixed-timestep simulation runner that decouples elapsed wall time from
/// simulation ticks and publishes immutable snapshots after each tick.
/// </summary>
public sealed class SimulationRunner
{
    private readonly World _world;
    private readonly double _fixedDeltaSeconds;
    private readonly Action<SimSnapshot>? _snapshotCallback;
    private readonly int _maxStepsPerAdvance;

    private double _accumulatorSeconds;

    /// <summary>
    /// Creates a new runner for the given world.
    /// </summary>
    /// <param name="world">World instance to drive.</param>
    /// <param name="snapshotCallback">Optional callback invoked after each tick with a copied snapshot.</param>
    /// <param name="maxStepsPerAdvance">Safety cap to avoid spiral-of-death when falling behind.</param>
    public SimulationRunner(
        World world,
        Action<SimSnapshot>? snapshotCallback = null,
        int maxStepsPerAdvance = 8)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        if (world.Config.FixedDeltaTime <= 0)
            throw new ArgumentException("World must use a positive FixedDeltaTime.", nameof(world));

        if (maxStepsPerAdvance <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxStepsPerAdvance), "Max steps must be positive.");

        _fixedDeltaSeconds = world.Config.FixedDeltaTime;
        _snapshotCallback = snapshotCallback;
        _maxStepsPerAdvance = maxStepsPerAdvance;
    }

    /// <summary>The world being simulated.</summary>
    public World World => _world;

    /// <summary>Fixed timestep duration in seconds.</summary>
    public double FixedDeltaTime => _fixedDeltaSeconds;

    /// <summary>Current accumulated time (seconds) waiting to be simulated.</summary>
    public double Accumulator => _accumulatorSeconds;

    /// <summary>
    /// Advances the simulation using elapsed wall time measured in seconds.
    /// Returns the number of ticks processed during this call.
    /// </summary>
    public int Advance(double elapsedSeconds)
    {
        if (elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "Elapsed time must be non-negative.");

        _accumulatorSeconds += elapsedSeconds;
        int steps = 0;

        while (_accumulatorSeconds + 1e-9 >= _fixedDeltaSeconds && steps < _maxStepsPerAdvance)
        {
            StepInternal();
            _accumulatorSeconds -= _fixedDeltaSeconds;
            steps++;
        }

        // Prevent unbounded accumulation when max step cap is hit.
        double maxCarry = _fixedDeltaSeconds * _maxStepsPerAdvance;
        if (_accumulatorSeconds > maxCarry)
        {
            _accumulatorSeconds = maxCarry;
        }

        return steps;
    }

    /// <summary>
    /// Advances the simulation using elapsed wall time as a <see cref="TimeSpan"/>.
    /// Returns the number of ticks processed during this call.
    /// </summary>
    public int Advance(TimeSpan elapsed) => Advance(elapsed.TotalSeconds);

    /// <summary>
    /// Forces a single tick regardless of the accumulator and returns the produced snapshot.
    /// </summary>
    public SimSnapshot Step()
    {
        StepInternal();
        _accumulatorSeconds = Math.Max(0, _accumulatorSeconds - _fixedDeltaSeconds);
        return SimSnapshot.FromWorld(_world);
    }

    /// <summary>
    /// Captures the current state without advancing the simulation.
    /// </summary>
    public SimSnapshot CaptureSnapshot() => SimSnapshot.FromWorld(_world);

    private void StepInternal()
    {
        _world.Tick();
        _snapshotCallback?.Invoke(SimSnapshot.FromWorld(_world));
    }
}
