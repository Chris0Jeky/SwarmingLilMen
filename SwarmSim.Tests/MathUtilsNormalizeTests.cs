using SwarmSim.Core.Utils;
using Xunit;

namespace SwarmSim.Tests;

public class MathUtilsNormalizeTests
{
    [Fact]
    public void TryNormalize_SmallVector_ZeroesAndReturnsFalse()
    {
        float x = 1e-9f;
        float y = 0f;

        bool result = MathUtils.TryNormalize(ref x, ref y);

        Assert.False(result);
        Assert.Equal(0f, x);
        Assert.Equal(0f, y);
    }

    [Fact]
    public void TryNormalize_UnitVector_ReturnsTrueAndLengthOne()
    {
        float x = 3f;
        float y = 4f;

        bool result = MathUtils.TryNormalize(ref x, ref y);

        Assert.True(result);
        Assert.Equal(1f, MathUtils.Length(x, y), 5);
    }

    [Fact]
    public void Normalize_ZeroVector_ReturnsZero()
    {
        var (x, y) = MathUtils.Normalize(0f, 0f);

        Assert.Equal(0f, x);
        Assert.Equal(0f, y);
    }
}
