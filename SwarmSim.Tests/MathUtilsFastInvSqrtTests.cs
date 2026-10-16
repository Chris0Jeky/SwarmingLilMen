using SwarmSim.Core.Utils;
using Xunit;

namespace SwarmSim.Tests;

public class MathUtilsFastInvSqrtTests
{
    [Fact]
    public void FastInvSqrt_KnownValues_AreExact()
    {
        Assert.Equal(0.5f, MathUtils.FastInvSqrt(4f));
        Assert.Equal(1f, MathUtils.FastInvSqrt(1f));
    }
}
