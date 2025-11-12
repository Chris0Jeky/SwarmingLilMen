namespace SwarmSim.Core.Canonical;

public readonly struct RuleContext
{
    public float TargetSpeed { get; }
    public float MaxForce { get; }
    public float SenseRadius { get; }
    public float FieldOfViewCos { get; }
    public float DeltaTime { get; }

    public RuleContext(float targetSpeed, float maxForce, float senseRadius, float fieldOfViewDegrees, float deltaTime)
    {
        TargetSpeed = targetSpeed;
        MaxForce = maxForce;
        SenseRadius = senseRadius;
        float halfAngleRad = (fieldOfViewDegrees * MathF.PI / 180f) * 0.5f;
        FieldOfViewCos = MathF.Cos(halfAngleRad);
        DeltaTime = deltaTime;
    }
}
