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
    /// <summary>Gets the toroidal world width, or positive infinity for an unbounded compatibility context.</summary>
    public float WorldWidth { get; }

    /// <summary>Gets the toroidal world height, or positive infinity for an unbounded compatibility context.</summary>
    public float WorldHeight { get; }

    public RuleContext(
        float targetSpeed,
        float maxForce,
        float senseRadius,
        float fieldOfViewCos,
        float deltaTime,
        float separationPriorityBoost,
        RuleInstrumentation? instrumentation = null,
        float worldWidth = float.PositiveInfinity,
        float worldHeight = float.PositiveInfinity)
    {
        TargetSpeed = targetSpeed;
        MaxForce = maxForce;
        SenseRadius = senseRadius;
        FieldOfViewCos = fieldOfViewCos;
        FieldOfViewRange = 1f - FieldOfViewCos;
        DeltaTime = deltaTime;
        SeparationPriorityBoost = separationPriorityBoost;
        Instrumentation = instrumentation;
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
    }

    /// <summary>
    /// Returns the shortest toroidal displacement from one position to another in this world.
    /// </summary>
    /// <param name="from">Starting position.</param>
    /// <param name="to">Destination position.</param>
    /// <returns>The minimum-image displacement.</returns>
    public Vec2 MinimumImageDelta(Vec2 from, Vec2 to) => Vec2.MinimumImageDelta(from, to, WorldWidth, WorldHeight);
}
