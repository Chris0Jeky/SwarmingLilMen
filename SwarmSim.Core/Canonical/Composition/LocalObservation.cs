using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Canonical;

/// <summary>A visible neighbor, with no global index or access to the world arrays.</summary>
/// <remarks>
/// Both displacement directions and the original velocity are retained deliberately. Replacing
/// the reverse displacement with negation, or rebuilding velocity from relative velocity, can
/// change signed-zero, half-world tie, or float-rounding behavior in existing rules.
/// </remarks>
public readonly record struct NeighborView(
    Vec2 RelativePosition,
    Vec2 ReverseRelativePosition,
    Vec2 RelativeVelocity,
    Vec2 Velocity,
    byte Group,
    float FieldOfViewWeight,
    float Distance);

/// <summary>A borrowed, bounded policy view valid only during the current policy call.</summary>
public readonly ref struct LocalObservation(Boid self, ReadOnlySpan<NeighborView> neighbors)
{
    public Boid Self { get; } = self;
    public ReadOnlySpan<NeighborView> Neighbors { get; } = neighbors;
}

/// <summary>Per-call scalar inputs for a local policy; no global boid span or index is exposed.</summary>
public readonly ref struct LocalPolicyContext
{
    public float TargetSpeed { get; }
    public float MaxForce { get; }
    public float SenseRadius { get; }
    public float FieldOfViewCos { get; }
    public float DeltaTime { get; }
    public float SeparationPriorityBoost { get; }
    public float PriorityBlend { get; }

    // Built-in stochastic/diagnostic state is owned by the world. External policies cannot use
    // these handles to obtain an identity or any other agent's state.
    internal Span<Pcg32State> WanderRandom { get; }
    internal Span<float> WanderAngle { get; }
    internal Span<int> WhiskerHits { get; }

    public LocalPolicyContext(float targetSpeed, float maxForce, float senseRadius,
        float fieldOfViewCos, float deltaTime, float separationPriorityBoost = 1f, float priorityBlend = 0f)
        : this(targetSpeed, maxForce, senseRadius, fieldOfViewCos, deltaTime,
            separationPriorityBoost, priorityBlend, default, default, default) { }

    internal LocalPolicyContext(float targetSpeed, float maxForce, float senseRadius,
        float fieldOfViewCos, float deltaTime, float separationPriorityBoost, float priorityBlend,
        Span<Pcg32State> wanderRandom, Span<float> wanderAngle, Span<int> whiskerHits)
    {
        TargetSpeed = targetSpeed;
        MaxForce = maxForce;
        SenseRadius = senseRadius;
        FieldOfViewCos = fieldOfViewCos;
        DeltaTime = deltaTime;
        SeparationPriorityBoost = separationPriorityBoost;
        PriorityBlend = priorityBlend;
        WanderRandom = wanderRandom;
        WanderAngle = wanderAngle;
        WhiskerHits = whiskerHits;
    }
}

/// <summary>A world-owned steering policy. Emit cannot retain the borrowed observation or writer.</summary>
public interface ILocalSteeringPolicy
{
    PolicyMetadata Metadata { get; }
    /// <summary>Maximum intents per call. Registration fixes this bound; overflow is an error.</summary>
    int MaxIntents { get; }
    void Emit(in LocalObservation observation, in LocalPolicyContext context, ref IntentWriter intents);
}
