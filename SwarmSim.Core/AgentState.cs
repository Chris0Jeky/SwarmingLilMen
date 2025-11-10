namespace SwarmSim.Core;

/// <summary>
/// Bit flags representing agent behavioral state.
/// Stored as byte for compact SoA representation.
/// </summary>
[Flags]
public enum AgentState : byte
{
    /// <summary>No special state</summary>
    None = 0,

    /// <summary>Agent is fleeing from enemies</summary>
    Fleeing = 1 << 0,

    /// <summary>Agent is hunting/attacking</summary>
    Hunting = 1 << 1,

    /// <summary>Agent is ready to reproduce</summary>
    Reproducing = 1 << 2,

    /// <summary>Agent is dead and slot can be recycled</summary>
    Dead = 1 << 3,

    /// <summary>Agent is foraging for food</summary>
    Foraging = 1 << 4,

    /// <summary>Agent is injured (low health)</summary>
    Injured = 1 << 5,

    /// <summary>Agent is exhausted (very low energy)</summary>
    Exhausted = 1 << 6,

    /// <summary>Reserved for future use</summary>
    Reserved = 1 << 7
}

/// <summary>
/// Extension methods for AgentState flags.
/// </summary>
public static class AgentStateExtensions
{
    /// <summary>Checks if the given flag is set.</summary>
    public static bool HasFlag(this AgentState state, AgentState flag)
        => (state & flag) == flag;

    /// <summary>Sets the given flag.</summary>
    public static AgentState SetFlag(this AgentState state, AgentState flag)
        => state | flag;

    /// <summary>Clears the given flag.</summary>
    public static AgentState ClearFlag(this AgentState state, AgentState flag)
        => state & ~flag;

    /// <summary>Toggles the given flag.</summary>
    public static AgentState ToggleFlag(this AgentState state, AgentState flag)
        => state ^ flag;
}
