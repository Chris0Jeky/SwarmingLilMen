using SwarmSim.Core.Utils;

namespace SwarmSim.Tests;

public class MathUtilsWrapTests
{
    [Fact]
    public void MinimumImageDelta_FoldsAcrossSeam()
    {
        Assert.Equal(-10f, MathUtils.MinimumImageDelta(190f, 200f), 1e-4f);
        Assert.Equal(10f, MathUtils.MinimumImageDelta(-190f, 200f), 1e-4f);
    }
}
