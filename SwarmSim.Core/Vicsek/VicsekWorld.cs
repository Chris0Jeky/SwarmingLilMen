using SwarmSim.Core.Canonical;
using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Vicsek;

/// <summary>
/// Synchronous Vicsek self-propelled particles (Vicsek et al. 1995, equations 1-3): every heading
/// reads the previous population, each agent counts itself exactly once, and positions advance
/// with the previous velocity. The reused <see cref="Boid"/> holds only position and velocity
/// here so the neighborhood contract applies unchanged; no steering budget, field of view or
/// separation exists in this model. See docs/VICSEK.md for the seam inventory.
/// </summary>
public sealed class VicsekWorld
{
    /// <summary>Stream names belong to the replay contract; renaming one changes every trajectory.</summary>
    public const string SpawnStream = "vicsek.spawn";
    public const string NoiseStream = "vicsek.noise";

    private readonly ISpatialIndex _index;
    private readonly Pcg32Rng _noise;
    private readonly int[] _neighbors;
    private readonly float _zeroResultantSquared;
    private Boid[] _current;
    private Boid[] _next;

    /// <summary>Creates uniformly placed agents with uniform headings from the named spawn stream.</summary>
    public VicsekWorld(VicsekSettings settings, int agentCount, ISpatialIndex? index = null)
        : this(settings, index, Spawn(settings, agentCount)) { }

    /// <summary>Creates explicit initial state; positions wrap into the periodic box.</summary>
    public VicsekWorld(VicsekSettings settings, ReadOnlySpan<Vec2> positions, ReadOnlySpan<float> headings, ISpatialIndex? index = null)
        : this(settings, index, Create(settings, positions, headings)) { }

    private VicsekWorld(VicsekSettings settings, ISpatialIndex? index, Boid[] agents)
    {
        Settings = settings;
        _index = index ?? new GridSpatialIndex(settings.InteractionRadius, settings.WorldWidth, settings.WorldHeight);
        if (_index.WorldWidth != settings.WorldWidth || _index.WorldHeight != settings.WorldHeight)
            throw new ArgumentException("The spatial index must use the configured world extents.", nameof(index));
        _index.Initialize(agents.Length);
        _noise = new RandomStreams(settings.Seed).CreateStream(NoiseStream);
        _current = agents;
        _next = new Boid[agents.Length];
        // Every other agent fits, so a query can never truncate the neighborhood the model defines.
        _neighbors = new int[agents.Length];
        // Below rounding in a sum of N velocities the resultant has no meaningful direction.
        float zeroResultant = 1e-6f * settings.Speed;
        _zeroResultantSquared = zeroResultant * zeroResultant;
    }

    public VicsekSettings Settings { get; }
    public long TickCount { get; private set; }
    public ReadOnlySpan<Boid> Agents => _current;

    /// <summary>
    /// Advances one tick. Each agent consumes exactly one noise draw in index order, whatever its
    /// neighborhood, so runs that differ only in eta share one noise sequence.
    /// </summary>
    public void Step()
    {
        ReadOnlySpan<Boid> current = _current;
        Span<Boid> next = _next;
        float eta = Settings.Noise;
        float speed = Settings.Speed;
        float radius = Settings.InteractionRadius;
        float dt = Settings.FixedDeltaTime;
        _index.Rebuild(current);

        for (int i = 0; i < current.Length; i++)
        {
            Boid self = current[i];
            SpatialQueryResult query = _index.QueryNeighbors(current, i, radius, _neighbors);
            if (query.IsTruncated)
                throw new InvalidOperationException("A Vicsek neighborhood must never be truncated.");

            // The query excludes self, so this is the only place an agent counts itself.
            double sumX = self.Velocity.X, sumY = self.Velocity.Y;
            ReadOnlySpan<int> neighbors = _neighbors.AsSpan(0, query.Count);
            foreach (int neighbor in neighbors)
            {
                Vec2 velocity = current[neighbor].Velocity;
                sumX += velocity.X;
                sumY += velocity.Y;
            }

            double members = query.Count + 1;
            double scaledX = sumX / members, scaledY = sumY / members;
            // Opposed headings have no mean direction: keep the agent's own, then apply noise.
            bool hasResultant = scaledX * scaledX + scaledY * scaledY > _zeroResultantSquared;
            float mean = hasResultant
                ? (float)Math.Atan2(sumY, sumX)
                : MathF.Atan2(self.Velocity.Y, self.Velocity.X);
            float heading = mean + (_noise.NextFloat() - 0.5f) * eta;
            (float sin, float cos) = MathF.SinCos(heading);

            (float x, float y) = MathUtils.WrapPosition(
                self.Position.X + self.Velocity.X * dt,
                self.Position.Y + self.Velocity.Y * dt,
                Settings.WorldWidth, Settings.WorldHeight);
            next[i] = new Boid(new Vec2(x, y), new Vec2(cos * speed, sin * speed));
        }

        (_current, _next) = (_next, _current);
        TickCount++;
    }

    private static Boid[] Spawn(VicsekSettings settings, int agentCount)
    {
        Validate(settings);
        if (agentCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(agentCount), "Agent count must be positive.");
        Pcg32Rng random = new RandomStreams(settings.Seed).CreateStream(SpawnStream);
        var agents = new Boid[agentCount];
        for (int i = 0; i < agents.Length; i++)
        {
            float x = random.NextFloat() * settings.WorldWidth;
            float y = random.NextFloat() * settings.WorldHeight;
            float heading = random.NextFloat() * MathF.Tau;
            agents[i] = Create(settings, x, y, heading);
        }
        return agents;
    }

    private static Boid[] Create(VicsekSettings settings, ReadOnlySpan<Vec2> positions, ReadOnlySpan<float> headings)
    {
        Validate(settings);
        if (positions.Length == 0 || positions.Length != headings.Length)
            throw new ArgumentException("Initial positions and headings must be nonempty and of equal length.");
        var agents = new Boid[positions.Length];
        for (int i = 0; i < agents.Length; i++)
        {
            if (!float.IsFinite(positions[i].X) || !float.IsFinite(positions[i].Y) || !float.IsFinite(headings[i]))
                throw new ArgumentException("Initial state must be finite.");
            agents[i] = Create(settings, positions[i].X, positions[i].Y, headings[i]);
        }
        return agents;
    }

    private static Boid Create(VicsekSettings settings, float x, float y, float heading)
    {
        (float wrappedX, float wrappedY) = MathUtils.WrapPosition(x, y, settings.WorldWidth, settings.WorldHeight);
        (float sin, float cos) = MathF.SinCos(heading);
        return new Boid(new Vec2(wrappedX, wrappedY), new Vec2(cos * settings.Speed, sin * settings.Speed));
    }

    private static void Validate(VicsekSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        List<string> errors = settings.Validate();
        if (errors.Count > 0)
            throw new ArgumentException($"Invalid Vicsek settings: {string.Join(", ", errors)}", nameof(settings));
    }
}
