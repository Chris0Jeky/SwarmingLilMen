using SwarmSim.Core.Utils;
using Xunit;

namespace SwarmSim.Tests;

/// <summary>
/// Wrap-around coverage for <see cref="MathUtils.AngleDifference"/>: angles near
/// +PI and -PI must take the short way around instead of the raw subtraction.
/// </summary>
public sealed class MathUtilsAngleTests
{
    [Fact]
    public void AngleDifference_WrapsToPiRange()
    {
        const float tolerance = 1e-3f;
        float expected = MathF.Tau - 6f;

        float forward = MathUtils.AngleDifference(3.0f, -3.0f);
        float backward = MathUtils.AngleDifference(-3.0f, 3.0f);

        Assert.True(Math.Abs(forward) <= MathF.PI, $"forward {forward} outside [-PI, PI]");
        Assert.True(Math.Abs(backward) <= MathF.PI, $"backward {backward} outside [-PI, PI]");

        Assert.True(MathF.Abs(forward - expected) <= tolerance, $"forward {forward} expected ~{expected}");
        Assert.True(MathF.Abs(backward + expected) <= tolerance, $"backward {backward} expected ~{-expected}");

        Assert.True(MathF.Abs(forward + backward) <= tolerance, $"not symmetric: {forward} vs {backward}");
    }
}
