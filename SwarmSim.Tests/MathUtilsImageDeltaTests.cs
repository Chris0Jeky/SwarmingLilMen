using SwarmSim.Core.Utils;

namespace SwarmSim.Tests;

public class MathUtilsImageDeltaTests
{
    [Fact]
    public void MinimumImageDelta_SeamWrapsToNegativeHalfExtent()
    {
        Assert.Equal(-2.5f, MathUtils.MinimumImageDelta(7.5f, 10f), 1e-4f);
        Assert.Equal(5f, MathUtils.MinimumImageDelta(5f, 10f), 1e-4f);
        Assert.Equal(-5f, MathUtils.MinimumImageDelta(-5f, 10f), 1e-4f);
    }
}
