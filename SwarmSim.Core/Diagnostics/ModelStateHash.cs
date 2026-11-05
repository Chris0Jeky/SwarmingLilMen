namespace SwarmSim.Core.Diagnostics;

/// <summary>A model-owned state fingerprint and the exact versioned profile that defines its bytes.</summary>
/// <remarks>This is an observation, not a checkpoint or a state restoration format.</remarks>
public readonly record struct ModelStateHash(string Profile, string Hash);
