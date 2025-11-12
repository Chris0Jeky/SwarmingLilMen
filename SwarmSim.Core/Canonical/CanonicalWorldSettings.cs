namespace SwarmSim.Core.Canonical;

public sealed class CanonicalWorldSettings
{
    public int InitialCapacity { get; init; } = 1024;
    public float TargetSpeed { get; init; } = 1f;
    public float MaxForce { get; init; } = 0.2f;
    public float SenseRadius { get; init; } = 10f;
    public float FieldOfView { get; init; } = 270f;
    public int MaxNeighbors { get; init; } = 32;

    public CanonicalWorldSettings() { }
}
