namespace SwarmSim.Tests;

public class CiGateProbeTests
{
    [Fact]
    public void DeliberateFailure_ProvesCiRejectsRedTests()
    {
        Assert.Fail("Deliberate issue #11 CI gate probe");
    }
}
