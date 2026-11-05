using SwarmSim.Core.Canonical;
using SwarmSim.Core.Diagnostics;
using SwarmSim.Core.Metrics;
using SwarmSim.Core.Simulation;
using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Firefly;

/// <summary>One stationary oscillator. Phase and natural frequency are unrelated to physical velocity.</summary>
public readonly record struct Oscillator(Vec2 Position, float Phase, float NaturalFrequency);

/// <summary>
/// Global, noiseless Kuramoto coupling: d(theta_i)/dt = omega_i + K/N sum_j sin(theta_j - theta_i).
/// Every oscillator reads the same previous population. A mean-field reduction makes the global
/// interaction O(N), without a spatial index, force arbitration or agent-specific neighbor arrays.
/// Positions only locate the oscillators for observation. See docs/FIREFLY.md for the exact subset.
/// </summary>
public sealed class FireflyWorld
{
    public const string PositionStream = "firefly.positions";
    public const string PhaseStream = "firefly.phases";
    public const string FrequencyStream = "firefly.frequencies";
    public const string ModelStateProfile = "firefly-state-v1";
    public const int MaxAgents = 1 << 24;

    private readonly DoubleBufferedState<Oscillator> _state;
    private readonly SimulationSchedule _schedule;
    private double _meanSin;
    private double _meanCos;

    public FireflyWorld(FireflySettings settings, int agentCount)
        : this(settings, Spawn(settings, agentCount)) { }

    /// <summary>Copies explicit initial state; positions and phases are normalized into their periodic domains.</summary>
    public FireflyWorld(FireflySettings settings, ReadOnlySpan<Oscillator> agents)
        : this(settings, Copy(settings, agents)) { }

    private FireflyWorld(FireflySettings settings, Oscillator[] agents)
    {
        Settings = settings;
        _state = DoubleBufferedState<Oscillator>.TakeOwnership(agents);
        _schedule = new SimulationSchedule(
            new SimulationPhase("mean-field", ComputeMeanField),
            new SimulationPhase("oscillators", ComputeNext),
            new SimulationPhase("commit", CommitTick));
    }

    public FireflySettings Settings { get; }
    public int Count => _state.Count;
    public long TickCount { get; private set; }
    public ReadOnlySpan<Oscillator> Agents => _state.Current;
    public IReadOnlyList<string> PhaseNames => _schedule.PhaseNames;

    public void Step() => _schedule.Run();

    private void ComputeMeanField()
    {
        double sumSin = 0d, sumCos = 0d;
        foreach (Oscillator agent in _state.Current)
        {
            (double sin, double cos) = Math.SinCos(agent.Phase);
            sumSin += sin;
            sumCos += cos;
        }
        _meanSin = sumSin / Count;
        _meanCos = sumCos / Count;
    }

    private void ComputeNext()
    {
        ReadOnlySpan<Oscillator> current = _state.Current;
        Span<Oscillator> next = _state.Next;
        for (int i = 0; i < current.Length; i++)
        {
            Oscillator self = current[i];
            (double sin, double cos) = Math.SinCos(self.Phase);
            double interaction = _meanSin * cos - _meanCos * sin;
            double drift = self.NaturalFrequency + (double)Settings.Coupling * interaction;
            next[i] = self with { Phase = WrapPhase(self.Phase + drift * Settings.FixedDeltaTime) };
        }
    }

    private void CommitTick()
    {
        _state.Commit();
        TickCount++;
    }

    /// <summary>Copies fixed positions and zero physical velocity. Oscillator phase is never encoded as heading.</summary>
    public void CopyKinematics(Span<KinematicSample> destination)
    {
        if (destination.Length < Count)
            throw new ArgumentException("Capture buffer must hold every oscillator; state is never truncated.", nameof(destination));
        ReadOnlySpan<Oscillator> agents = Agents;
        for (int i = 0; i < agents.Length; i++)
            destination[i] = new KinematicSample(agents[i].Position.X, agents[i].Position.Y, 0f, 0f);
    }

    /// <summary>
    /// Versioned model state at a completed tick. Includes resolved settings and ordered phase/frequency
    /// state, which a kinematic hash cannot see. Initialization streams are discarded; Step has no RNG.
    /// The mean-field scratch is overwritten before each read and therefore is not continuation state.
    /// This is an observation, not a checkpoint. Do not call concurrently with mutation.
    /// </summary>
    public ModelStateHash StateHash()
    {
        using var hash = new StateHashWriter(ModelStateProfile);
        hash.WriteSingle(Settings.Coupling);
        hash.WriteSingle(Settings.MeanFrequency);
        hash.WriteSingle(Settings.FrequencySpread);
        hash.WriteSingle(Settings.WorldWidth);
        hash.WriteSingle(Settings.WorldHeight);
        hash.WriteSingle(Settings.FixedDeltaTime);
        hash.WriteUInt32(Settings.Seed);
        hash.WriteInt64(TickCount);
        hash.WriteInt32(Count);
        foreach (Oscillator agent in Agents)
        {
            hash.WriteSingle(agent.Position.X);
            hash.WriteSingle(agent.Position.Y);
            hash.WriteSingle(agent.Phase);
            hash.WriteSingle(agent.NaturalFrequency);
        }
        return hash.Finish();
    }

    private static Oscillator[] Spawn(FireflySettings settings, int count)
    {
        Validate(settings);
        ValidateCount(count);
        var streams = new RandomStreams(settings.Seed);
        Pcg32Rng positions = streams.CreateStream(PositionStream);
        Pcg32Rng phases = streams.CreateStream(PhaseStream);
        Pcg32Rng frequencies = streams.CreateStream(FrequencyStream);
        var agents = new Oscillator[count];
        for (int i = 0; i < agents.Length; i++)
        {
            // The separate streams keep initial phases/frequencies unchanged when only display geometry changes.
            (float x, float y) = MathUtils.WrapPosition(positions.NextFloat() * settings.WorldWidth,
                positions.NextFloat() * settings.WorldHeight, settings.WorldWidth, settings.WorldHeight);
            float phase = WrapPhase(phases.NextFloat() * Math.Tau);
            float frequency = (float)(settings.MeanFrequency + (frequencies.NextFloat() * 2d - 1d) * settings.FrequencySpread);
            agents[i] = new Oscillator(new Vec2(x, y), phase, frequency);
        }
        return agents;
    }

    private static Oscillator[] Copy(FireflySettings settings, ReadOnlySpan<Oscillator> agents)
    {
        Validate(settings);
        ValidateCount(agents.Length);
        var copy = new Oscillator[agents.Length];
        for (int i = 0; i < agents.Length; i++)
        {
            Oscillator agent = agents[i];
            if (!float.IsFinite(agent.Position.X) || !float.IsFinite(agent.Position.Y)
                || !float.IsFinite(agent.Phase) || !float.IsFinite(agent.NaturalFrequency))
                throw new ArgumentException("Initial oscillator state must be finite.", nameof(agents));
            (float x, float y) = MathUtils.WrapPosition(agent.Position.X, agent.Position.Y,
                settings.WorldWidth, settings.WorldHeight);
            copy[i] = new Oscillator(new Vec2(x, y), WrapPhase(agent.Phase), agent.NaturalFrequency);
        }
        return copy;
    }

    private static float WrapPhase(double phase)
    {
        double wrapped = phase % Math.Tau;
        if (wrapped < 0d) wrapped += Math.Tau;
        float result = (float)wrapped;
        // A value immediately below 2*pi can round up when stored as binary32.
        return result >= MathF.Tau || result == 0f ? 0f : result;
    }

    private static void ValidateCount(int count)
    {
        if (count <= 0 || count > MaxAgents)
            throw new ArgumentOutOfRangeException(nameof(count), $"Oscillator count must be from 1 through {MaxAgents}.");
    }

    private static void Validate(FireflySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        List<string> errors = settings.Validate();
        if (errors.Count > 0)
            throw new ArgumentException($"Invalid firefly settings: {string.Join(", ", errors)}", nameof(settings));
    }
}
