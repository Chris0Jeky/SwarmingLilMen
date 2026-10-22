using SwarmSim.Core.Utils;
using Xunit;

namespace SwarmSim.Tests;

public class MathUtilsGuardTests
{
    static (float pos, float vel) Reflect(float pos, float vel, float max)
    {
        MathUtils.ReflectPosition(ref pos, ref vel, max);
        return (pos, vel);
    }

    [Fact]
    public void MathUtils_ReflectPosition_MirrorsAndInverts()
    {
        var (pos1, vel1) = Reflect(-3f, -2f, 10f);
        Assert.Equal(3f, pos1, 4);
        Assert.Equal(2f, vel1, 4);

        var (pos2, vel2) = Reflect(13f, 2f, 10f);
        Assert.Equal(7f, pos2, 4);
        Assert.Equal(-2f, vel2, 4);

        var (pos3, vel3) = Reflect(0f, -2f, 10f);
        Assert.Equal(0f, pos3, 4);
        Assert.Equal(-2f, vel3, 4);

        var (pos4, vel4) = Reflect(10f, 2f, 10f);
        Assert.Equal(10f, pos4, 4);
        Assert.Equal(2f, vel4, 4);

        var (pos5, vel5) = Reflect(4f, -1f, 10f);
        Assert.Equal(4f, pos5, 4);
        Assert.Equal(-1f, vel5, 4);
    }

    [Theory]
    [InlineData(13f, 2f, 7f, -1f)]      // single bounce off max
    [InlineData(-3f, -2f, 3f, 1f)]      // single bounce off 0
    [InlineData(35f, 30f, 5f, -1f)]     // triple bounce (the reported case, previously ended at -15)
    [InlineData(25f, 1f, 5f, 1f)]       // double bounce: heading outward again
    [InlineData(-25f, -1f, 5f, 1f)]     // negative overshoot, two bounces plus the origin one
    [InlineData(-35f, -30f, 5f, -1f)]   // negative triple overshoot
    [InlineData(10f, 2f, 10f, 1f)]      // exact wall, in range: untouched
    [InlineData(0f, -2f, 0f, -1f)]      // exact origin, in range: untouched
    [InlineData(20f, 1f, 0f, 1f)]       // exact period multiple lands on the origin
    public void ReflectPosition_FoldsAnyDisplacementIntoRange(float pos, float vel, float expectedPos, float expectedSign)
    {
        const float max = 10f;
        var (p, v) = Reflect(pos, vel, max);

        Assert.InRange(p, 0f, max);
        Assert.Equal(expectedPos, p, 3);
        Assert.Equal(expectedSign, MathF.Sign(v));
        Assert.Equal(MathF.Abs(vel), MathF.Abs(v));
    }
}
