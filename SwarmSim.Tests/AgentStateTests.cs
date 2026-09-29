using SwarmSim.Core;

namespace SwarmSim.Tests;

public class AgentStateTests
{
    [Fact]
    public void AgentState_Flags_RoundTrip()
    {
        // Arrange: start from None and set two flags.
        AgentState state = AgentState.None.SetFlag(AgentState.Hunting).SetFlag(AgentState.Foraging);

        // Assert: both flags set, unrelated flag not set.
        Assert.True(state.HasFlag(AgentState.Hunting));
        Assert.True(state.HasFlag(AgentState.Foraging));
        Assert.False(state.HasFlag(AgentState.Dead));

        // Assert: underlying shifted bit values are pinned.
        Assert.Equal((byte)(1 << 1), (byte)AgentState.Hunting);
        Assert.Equal((byte)(1 << 4), (byte)AgentState.Foraging);
        Assert.Equal((byte)((1 << 1) | (1 << 4)), (byte)state);

        // Act: clear one flag.
        state = state.ClearFlag(AgentState.Hunting);

        // Assert: cleared flag off, other flag stays set.
        Assert.False(state.HasFlag(AgentState.Hunting));
        Assert.True(state.HasFlag(AgentState.Foraging));

        // Act: toggle Foraging off and back on.
        state = state.ToggleFlag(AgentState.Foraging);
        Assert.False(state.HasFlag(AgentState.Foraging));

        state = state.ToggleFlag(AgentState.Foraging);
        Assert.True(state.HasFlag(AgentState.Foraging));
    }
}
