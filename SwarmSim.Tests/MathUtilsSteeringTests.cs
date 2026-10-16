using SwarmSim.Core.Utils;
using Xunit;

namespace SwarmSim.Tests;

public class MathUtilsSteeringTests
{
    [Fact]
    public void ApplySteeringForce_ClampsForceThenSpeed()
    {
        // Case 1: huge force from rest — force clamp (maxForce) dominates.
        var (vx1, vy1) = MathUtils.ApplySteeringForce(0f, 0f, 1000f, 0f, 5f, 10f, 1f);
        Assert.Equal(5f, MathUtils.Length(vx1, vy1), 4);
        Assert.Equal(5f, vx1, 4);
        Assert.Equal(0f, vy1, 4);

        // Case 2: large initial velocity with modest force — speed clamp (maxSpeed) dominates.
        var (vx2, vy2) = MathUtils.ApplySteeringForce(1000f, 0f, 1f, 0f, 5f, 10f, 1f);
        float speed2 = MathUtils.Length(vx2, vy2);
        Assert.True(speed2 <= 10f + 1e-3f);
        Assert.Equal(10f, speed2, 3);
    }
}
