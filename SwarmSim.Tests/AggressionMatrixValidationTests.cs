using SwarmSim.Core;

namespace SwarmSim.Tests;

public sealed class AggressionMatrixValidationTests
{
    [Fact]
    public void Validate_NullAggressionMatrix_ReturnsError()
    {
        var config = new SimConfig { AggressionMatrix = null! };

        var errors = config.Validate();

        Assert.Contains("AggressionMatrix must not be null", errors);
    }
}
