using SwarmSim.Core;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Diagnostics;
using SwarmSim.Core.Metrics;
using SwarmSim.Core.Utils;

namespace SwarmSim.Cli;

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
            if (request.AgentCount > config.MaxCapacity)
                throw new ArgumentException("--agents exceeds the legacy MaxCapacity.");
            _legacy = new World(config);
            Spawn(config.WorldWidth, config.WorldHeight, config.MaxSpeed);
        }
    }

    internal CanonicalWorld? Canonical => _canonical;
    internal int MetricCapacity => _canonical is not null ? Math.Max(1, _request.Canonical!.InitialCapacity) : _legacy!.Capacity;
    internal MetricDomain Domain => _canonical is not null
        ? MetricDomain.Toroidal(_request.Canonical!.WorldWidth, _request.Canonical.WorldHeight)
        : _request.Legacy!.BoundaryMode == BoundaryMode.Wrap
            ? MetricDomain.Toroidal(_request.Legacy.WorldWidth, _request.Legacy.WorldHeight)
            : MetricDomain.Euclidean;

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

    internal int Capture(Span<KinematicSample> destination)
    {
        int required = _canonical?.Count ?? _legacy!.Count;
        if (destination.Length < required)
            throw new ArgumentException("Capture buffer must hold every simulation slot; state is never truncated.", nameof(destination));
        int count = 0;
        if (_canonical is not null)
        {
            foreach (Boid boid in _canonical.Boids)
                destination[count++] = new(boid.Position.X, boid.Position.Y, boid.Velocity.X, boid.Velocity.Y);
        }
        else
        {
            World world = _legacy!;
            for (int i = 0; i < world.Count; i++)
                if ((world.State[i] & AgentState.Dead) == 0)
                    destination[count++] = new(world.X[i], world.Y[i], world.Vx[i], world.Vy[i]);
        }
        return count;
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

}
