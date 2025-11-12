namespace SwarmSim.Core.Canonical;

public sealed class CanonicalWorldSettings
{
    public int InitialCapacity { get; init; } = 1024;
    public float TargetSpeed { get; init; } = 1f;
    public float MaxForce { get; init; } = 0.2f;
    public float SenseRadius { get; init; } = 10f;
    public float FieldOfView { get; init; } = 270f;
    public int MaxNeighbors { get; init; } = 32;
    public float SeparationRadius { get; init; } = 5f;
    public float SeparationWeight { get; init; } = 1.5f;
    public float AlignmentWeight { get; init; } = 1f;
    public float CohesionWeight { get; init; } = 1f;
    public float SeparationPriorityRadiusFactor { get; init; } = 0.33f;
    public float SeparationPriorityBoost { get; init; } = 2.5f;
    public float WanderStrength { get; init; } = 0.1f;
    public uint Seed { get; init; } = 123456u;
    public float WhiskerTimeHorizon { get; init; } = 0.4f; // seconds ahead to check
    public float WhiskerWeight { get; init; } = 1.2f;      // lateral avoidance weight
    public float WorldWidth { get; init; } = 1920f;
    public float WorldHeight { get; init; } = 1080f;
    public float FixedDeltaTime { get; init; } = 1f / 60f;

    public CanonicalWorldSettings() { }
}
