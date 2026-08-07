namespace SwarmSim.Core.Canonical;

public sealed class CanonicalWorldSettings
{
    public int InitialCapacity { get; init; } = 1024;
    public float TargetSpeed { get; init; } = 1f;
    /// <summary>
    /// Hard per-tick ceiling on the TOTAL composed steering magnitude for one boid, in units of
    /// speed per second. It is a single shared budget, not a per-contribution allowance: whisker
    /// avoidance, separation, alignment, cohesion and wander all draw from the same remainder in
    /// that order, and once it is spent later contributions receive nothing. Separation
    /// additionally withholds whatever it leaves unspent from alignment, cohesion and wander -
    /// that priority behaviour is unchanged; what it may no longer do is spend beyond this
    /// ceiling.
    /// </summary>
    /// <remarks>
    /// Enforced since issue #19. Before that fix separation clamped to a fresh MaxForce and added
    /// on top of an already-spent whisker budget, so one tick could compose up to 2x this value.
    /// <see cref="RuleInstrumentation.SteeringMagnitudesSquared"/> exposes the composed magnitude
    /// so the invariant is checkable from outside; the composition order itself is a separate
    /// design property owned by the steering-arbitration work.
    /// </remarks>
    public float MaxForce { get; init; } = 0.2f;
    public float SenseRadius { get; init; } = 10f;
    public float FieldOfView { get; init; } = 270f;
    public int MaxNeighbors { get; init; } = 32;
    public float SeparationRadius { get; init; } = 5f;
    public float SeparationWeight { get; init; } = 1.5f;
    public float AlignmentWeight { get; init; } = 1f;
    public float CohesionWeight { get; init; } = 1f;
    public float SeparationPriorityRadiusFactor { get; init; } = 0.20f;
    public float SeparationPriorityExitFactor { get; init; } = 0.45f;
    public float SeparationPriorityBoost { get; init; } = 2.5f;
    public float SeparationPriorityHoldTime { get; init; } = 0.08f;
    public float SeparationPriorityRampInTime { get; init; } = 0.08f;
    public float SeparationPriorityRampOutTime { get; init; } = 0.1f;
    public float SeparationSpeedDroop { get; init; } = 0.03f;
    public float MaxTurnRateDegPerSecond { get; init; } = 360f;
    public float WanderStrength { get; init; } = 0f;
    public float WanderRate { get; init; } = 1.5f;
    public float WhiskerTimeHorizon { get; init; } = 0.4f;
    public float WhiskerWeight { get; init; } = 1.2f;
    /// <summary>
    /// External deterministic seed. Supported values are 0 through
    /// <see cref="SwarmSim.Core.Utils.Rng.MaxSupportedSeed"/>, inclusive.
    /// </summary>
    public uint Seed { get; init; } = 123456u;
    public float WorldWidth { get; init; } = 1920f;
    public float WorldHeight { get; init; } = 1080f;
    public float FixedDeltaTime { get; init; } = 1f / 60f;

    public CanonicalWorldSettings() { }
}
