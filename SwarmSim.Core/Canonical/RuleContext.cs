namespace SwarmSim.Core.Canonical;

public readonly struct RuleContext
{
    public float TargetSpeed { get; }
    public float MaxForce { get; }
    public float SenseRadius { get; }
    public float FieldOfViewCos { get; }
    public float FieldOfViewRange { get; }
    public float DeltaTime { get; }
    public float SeparationPriorityBoost { get; }
    public RuleInstrumentation? Instrumentation { get; }

    public RuleContext(
        float targetSpeed,
        float maxForce,
        float senseRadius,
        float fieldOfViewCos,
        float deltaTime,
        float separationPriorityBoost,
        RuleInstrumentation? instrumentation = null)
    {
        TargetSpeed = targetSpeed;
        MaxForce = maxForce;
        SenseRadius = senseRadius;
        FieldOfViewCos = fieldOfViewCos;
        FieldOfViewRange = 1f - FieldOfViewCos;
        DeltaTime = deltaTime;
        SeparationPriorityBoost = separationPriorityBoost;
        Instrumentation = instrumentation;
    }
}
