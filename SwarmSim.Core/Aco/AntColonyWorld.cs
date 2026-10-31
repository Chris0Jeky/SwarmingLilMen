using SwarmSim.Core.Canonical;
using SwarmSim.Core.Metrics;
using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Aco;

/// <summary>One ant. Its shape is its own: a heading and a load, with no stored velocity.</summary>
public readonly record struct Ant(Vec2 Position, float Heading, bool Carrying);

/// <summary>
/// Minimal stigmergic foraging. Searching ants walk at random and lean toward stronger pheromone;
/// returning ants know the way home by path integration and lay pheromone as they go. The only
/// channel between ants is the field, and nothing in the agent phase writes it: agents read
/// field(t) and buffer deposits, and the field phase applies them and evaporates. Each ant retains
/// its own stream, so its draw count follows its own state. See docs/ACO.md.
/// </summary>
public sealed class AntColonyWorld
{
    /// <summary>Stream names belong to the replay contract; renaming one changes every trajectory.</summary>
    public const string SpawnStream = "aco.spawn";
    public const string ForageStream = "aco.forage";

    private readonly Ant[] _ants;
    private readonly Pcg32Rng[] _forage;
    private readonly Vec2[] _deposits;
    private readonly float _step;
    private int _depositCount;
    private bool _agentPhaseDone;

    /// <summary>Largest supported colony; each ant retains its own generator object.</summary>
    public const int MaxAnts = 1 << 24;

    /// <summary>Starts every ant at the nest centre with a uniform heading from the spawn stream.</summary>
    public AntColonyWorld(AntColonySettings settings, int antCount)
        : this(settings, Spawn(settings, antCount)) { }

    /// <summary>Creates explicit initial state; positions wrap into the periodic box.</summary>
    public AntColonyWorld(AntColonySettings settings, ReadOnlySpan<Ant> ants)
        : this(settings, Copy(settings, ants)) { }

    private AntColonyWorld(AntColonySettings settings, Ant[] ants)
    {
        Settings = settings;
        _ants = ants;
        _deposits = new Vec2[ants.Length];
        _step = settings.Speed * settings.FixedDeltaTime;
        Field = new ScalarField(settings.FieldColumns, settings.FieldRows, settings.WorldWidth, settings.WorldHeight);
        FoodRemaining = settings.FoodAmount;
        var streams = new RandomStreams(settings.Seed);
        _forage = new Pcg32Rng[ants.Length];
        for (int i = 0; i < _forage.Length; i++)
            _forage[i] = streams.CreateStream(ForageStream, (ulong)i);
        foreach (Ant ant in ants)
            if (ant.Carrying) CarryingCount++;
        // A supplied load came from the food supply, so food stays conserved from tick zero.
        if (CarryingCount > FoodRemaining)
            throw new ArgumentException("Initial ants cannot carry more than FoodAmount.", nameof(ants));
        FoodRemaining -= CarryingCount;
    }

    public AntColonySettings Settings { get; }
    public ScalarField Field { get; }
    public long TickCount { get; private set; }
    public ReadOnlySpan<Ant> Ants => _ants;
    public int FoodRemaining { get; private set; }
    public long FoodReturned { get; private set; }
    public int CarryingCount { get; private set; }
    /// <summary>Deposits buffered by the agent phase and not yet applied.</summary>
    public int PendingDeposits => _depositCount;

    /// <summary>One agent phase, then one field phase.</summary>
    public void Step()
    {
        AgentPhase();
        FieldPhase();
    }

    /// <summary>
    /// Moves every ant in index order. Reads field(t) only and buffers deposits; resources (food,
    /// not the field) change here, so a contended last unit goes to the lowest index.
    /// </summary>
    public void AgentPhase()
    {
        if (_agentPhaseDone)
            throw new InvalidOperationException("The field phase must follow each agent phase.");
        Span<Ant> ants = _ants;
        for (int i = 0; i < ants.Length; i++)
            ants[i] = ants[i].Carrying ? Return(ants[i], _forage[i]) : Search(ants[i], _forage[i]);
        _agentPhaseDone = true;
    }

    /// <summary>Applies buffered deposits in stable ant order, then evaporates.</summary>
    public void FieldPhase()
    {
        if (!_agentPhaseDone)
            throw new InvalidOperationException("The field phase needs a completed agent phase.");
        ReadOnlySpan<Vec2> deposits = _deposits.AsSpan(0, _depositCount);
        float amount = Settings.Deposit;
        foreach (Vec2 point in deposits)
            Field.Deposit(point.X, point.Y, amount);
        Field.Evaporate(Settings.Evaporation);
        _depositCount = 0;
        _agentPhaseDone = false;
        TickCount++;
    }

    /// <summary>Copies ordered position and heading-derived velocity for analysis.</summary>
    public void CopyKinematics(Span<KinematicSample> destination)
    {
        if (destination.Length < _ants.Length)
            throw new ArgumentException("Capture buffer must hold every ant; state is never truncated.", nameof(destination));
        ReadOnlySpan<Ant> ants = _ants;
        float speed = Settings.Speed;
        for (int i = 0; i < ants.Length; i++)
        {
            (float sin, float cos) = MathF.SinCos(ants[i].Heading);
            destination[i] = new(ants[i].Position.X, ants[i].Position.Y, cos * speed, sin * speed);
        }
    }

    /// <summary>
    /// One wander draw every tick, and one choice draw only when a sensor reads pheromone. With a
    /// zero field the second draw never happens, which is what makes Deposit = 0 a valid control.
    /// </summary>
    private Ant Search(in Ant ant, Pcg32Rng random)
    {
        float angle = Settings.SensorAngle;
        float wander = (random.NextFloat() - 0.5f) * Settings.TurnNoise;
        float left = Sense(ant, ant.Heading - angle);
        float centre = Sense(ant, ant.Heading);
        float right = Sense(ant, ant.Heading + angle);
        float total = left + centre + right;
        float turn = 0f;
        if (total > 0f)
        {
            float choice = random.NextFloat() * total;
            if (choice < left) turn = -angle;
            else if (choice >= left + centre) turn = angle;
        }

        float heading = ant.Heading + turn + wander;
        Vec2 position = Advance(ant.Position, heading);
        bool found = FoodRemaining > 0 && Within(position, Settings.FoodX, Settings.FoodY, Settings.FoodRadius);
        if (found)
        {
            FoodRemaining--;
            CarryingCount++;
        }
        return new Ant(position, heading, found);
    }

    /// <summary>Homes straight to the nest and marks the way; a delivery draws a fresh heading.</summary>
    private Ant Return(in Ant ant, Pcg32Rng random)
    {
        float toNestX = MathUtils.MinimumImageDelta(Settings.NestX - ant.Position.X, Settings.WorldWidth);
        float toNestY = MathUtils.MinimumImageDelta(Settings.NestY - ant.Position.Y, Settings.WorldHeight);
        float heading = MathF.Atan2(toNestY, toNestX);
        Vec2 position = Advance(ant.Position, heading);
        _deposits[_depositCount++] = position;
        if (!Within(position, Settings.NestX, Settings.NestY, Settings.NestRadius))
            return new Ant(position, heading, Carrying: true);
        FoodReturned++;
        CarryingCount--;
        // ponytail: no memory of the food's direction. Reversing would hand the control a route
        // too, and the A/B must measure the field alone.
        return new Ant(position, random.NextFloat() * MathF.Tau, Carrying: false);
    }

    private float Sense(in Ant ant, float bearing)
    {
        (float sin, float cos) = MathF.SinCos(bearing);
        float reach = Settings.SensorDistance;
        return Field.Sample(ant.Position.X + cos * reach, ant.Position.Y + sin * reach);
    }

    private Vec2 Advance(Vec2 position, float heading)
    {
        (float sin, float cos) = MathF.SinCos(heading);
        (float x, float y) = MathUtils.WrapPosition(position.X + cos * _step, position.Y + sin * _step,
            Settings.WorldWidth, Settings.WorldHeight);
        return new Vec2(x, y);
    }

    private bool Within(Vec2 position, float x, float y, float radius)
    {
        float dx = MathUtils.MinimumImageDelta(x - position.X, Settings.WorldWidth);
        float dy = MathUtils.MinimumImageDelta(y - position.Y, Settings.WorldHeight);
        return dx * dx + dy * dy <= radius * radius;
    }

    private static Ant[] Spawn(AntColonySettings settings, int antCount)
    {
        Validate(settings);
        if (antCount <= 0 || antCount > MaxAnts)
            throw new ArgumentOutOfRangeException(nameof(antCount), $"Ant count must be from 1 through {MaxAnts}.");
        Pcg32Rng random = new RandomStreams(settings.Seed).CreateStream(SpawnStream);
        var ants = new Ant[antCount];
        for (int i = 0; i < ants.Length; i++)
            ants[i] = new Ant(new Vec2(settings.NestX, settings.NestY), random.NextFloat() * MathF.Tau, Carrying: false);
        return ants;
    }

    private static Ant[] Copy(AntColonySettings settings, ReadOnlySpan<Ant> ants)
    {
        Validate(settings);
        if (ants.Length == 0)
            throw new ArgumentException("Initial state must hold at least one ant.", nameof(ants));
        var copy = new Ant[ants.Length];
        for (int i = 0; i < copy.Length; i++)
        {
            Ant ant = ants[i];
            if (!float.IsFinite(ant.Position.X) || !float.IsFinite(ant.Position.Y) || !float.IsFinite(ant.Heading))
                throw new ArgumentException("Initial state must be finite.", nameof(ants));
            (float x, float y) = MathUtils.WrapPosition(ant.Position.X, ant.Position.Y, settings.WorldWidth, settings.WorldHeight);
            copy[i] = ant with { Position = new Vec2(x, y) };
        }
        return copy;
    }

    private static void Validate(AntColonySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        List<string> errors = settings.Validate();
        if (errors.Count > 0)
            throw new ArgumentException($"Invalid ant colony settings: {string.Join(", ", errors)}", nameof(settings));
    }
}
