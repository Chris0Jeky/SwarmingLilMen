using SwarmSim.Core;

namespace SwarmSim.Tests;

public sealed class SimSnapshotTests
{
    [Fact]
    public void FromWorld_NullWorld_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => SimSnapshot.FromWorld(null!, 0, 0));
    }
}
