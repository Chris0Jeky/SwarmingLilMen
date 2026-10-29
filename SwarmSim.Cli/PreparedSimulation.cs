using SwarmSim.Core;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Diagnostics;
using SwarmSim.Core.Utils;

namespace SwarmSim.Cli;

internal readonly record struct MetricRow(int Tick, int Population, double MeanSpeed, double Polarization)
{
    internal void Write(TextWriter output) => output.WriteLine(FormattableString.Invariant($"{Tick},{Population},{MeanSpeed:R},{Polarization:R}"));
}

/// <summary>A single initialized experiment, with no renderer or wall-clock stepping.</summary>
internal sealed class PreparedSimulation
{
    private readonly RunRequest _request;
    private readonly World? _legacy;
    private readonly CanonicalWorld? _canonical;

    internal PreparedSimulation(RunRequest request)
    {
        _request = request;
        if (request.Canonical is not null)
        {
            CanonicalWorldSettings settings = request.Canonical;
            if (request.AgentCount > Math.Max(1, settings.InitialCapacity))
                throw new ArgumentException("--agents exceeds the canonical InitialCapacity.");
            _canonical = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
            Spawn(settings.WorldWidth, settings.WorldHeight, settings.TargetSpeed);
        }
        else
        {
            SimConfig config = request.Legacy!;
            if (request.AgentCount > config.InitialCapacity)
                throw new ArgumentException("--agents exceeds the legacy InitialCapacity.");
            _legacy = new World(config);
            Spawn(config.WorldWidth, config.WorldHeight, config.MaxSpeed);
        }
        InitialMetrics = Measure(0); // Non-finite spawn state fails before any output is created.
    }

    internal MetricRow InitialMetrics { get; }

    internal void Step()
    {
        if (_canonical is not null)
            _canonical.Step(_request.FixedDeltaTime);
        else
            _legacy!.Tick();
    }

    internal string KinematicHash() => _canonical is not null
        ? SimulationKinematicHash.Compute(_canonical)
        : SimulationKinematicHash.Compute(_legacy!);

    internal MetricRow Measure(int tick)
    {
        var totals = new MetricTotals();
        if (_canonical is not null)
        {
            foreach (Boid boid in _canonical.Boids)
                totals.Add(boid.Position.X, boid.Position.Y, boid.Velocity.X, boid.Velocity.Y);
        }
        else
        {
            World world = _legacy!;
            for (int i = 0; i < world.Count; i++)
                if ((world.State[i] & AgentState.Dead) == 0)
                    totals.Add(world.X[i], world.Y[i], world.Vx[i], world.Vy[i]);
        }
        return totals.Row(tick);
    }

    private void Spawn(float width, float height, float speed)
    {
        var random = new Rng(_request.Seed);
        for (int i = 0; i < _request.AgentCount; i++)
        {
            float x = random.NextFloat(0f, width);
            float y = random.NextFloat(0f, height);
            float angle = random.NextFloat(0f, 2f * MathF.PI);
            float dx = MathF.Cos(angle);
            float dy = MathF.Sin(angle);
            if (_canonical is not null)
            {
                if (!_canonical.TryAddBoid(new Vec2(x, y), new Vec2(dx, dy)))
                    throw new InvalidOperationException("Canonical initialization rejected an agent.");
            }
            else
            {
                int index = _legacy!.AddAgent(x, y);
                if (index < 0)
                    throw new InvalidOperationException("Legacy initialization rejected an agent.");
                _legacy.Vx[index] = dx * speed;
                _legacy.Vy[index] = dy * speed;
            }
        }
    }

    private struct MetricTotals
    {
        private int _population;
        private double _speedSum;
        private double _headingX;
        private double _headingY;

        internal void Add(float x, float y, float vx, float vy)
        {
            if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(vx) || !float.IsFinite(vy))
                throw new InvalidOperationException("Simulation produced non-finite kinematic state.");
            double speed = Math.Sqrt((double)vx * vx + (double)vy * vy);
            _population++;
            _speedSum += speed;
            if (speed > 0d)
            {
                _headingX += vx / speed;
                _headingY += vy / speed;
            }
        }

        internal readonly MetricRow Row(int tick) => _population == 0
            ? new(tick, 0, 0d, 0d)
            : new(tick, _population, _speedSum / _population,
                Math.Clamp(Math.Sqrt(_headingX * _headingX + _headingY * _headingY) / _population, 0d, 1d));
    }
}
