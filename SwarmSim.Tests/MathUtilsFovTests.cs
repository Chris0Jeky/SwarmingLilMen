using SwarmSim.Core.Utils;
using Xunit;

namespace SwarmSim.Tests;

public class MathUtilsFovTests
{
    [Fact]
    public void IsWithinFieldOfView_Branches()
    {
        Assert.False(MathUtils.IsWithinFieldOfView(1f, 0f, 0f, 1f, 90f));
        Assert.True(MathUtils.IsWithinFieldOfView(1f, 0f, 0f, 1f, 360f));
        Assert.False(MathUtils.IsWithinFieldOfView(1f, 0f, 0f, 0f, 90f));
        Assert.True(MathUtils.IsWithinFieldOfView(0f, 0f, 1f, 0f, 90f));
        Assert.True(MathUtils.IsWithinFieldOfView(1f, 0f, 1f, 0.1f, 90f));
        Assert.False(MathUtils.IsWithinFieldOfView(1f, 0f, -1f, 0f, 90f));
        Assert.True(MathUtils.IsWithinFieldOfView(1f, 0f, 0f, 1f, 270f));
    }
}
