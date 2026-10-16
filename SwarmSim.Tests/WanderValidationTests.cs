using SwarmSim.Core;
using SwarmSim.Core.Systems;

namespace SwarmSim.Tests;

public sealed class WanderValidationTests
{
    [Fact]
    [Trait("Category", "WanderValidation")]
    public void Ctor_NaN_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WanderSystem(float.NaN));
    }

    [Fact]
    [Trait("Category", "WanderValidation")]
    public void Ctor_PositiveInfinity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WanderSystem(float.PositiveInfinity));
    }

    [Fact]
    [Trait("Category", "WanderValidation")]
    public void Ctor_NegativeInfinity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WanderSystem(float.NegativeInfinity));
    }

    [Fact]
    [Trait("Category", "WanderValidation")]
    public void Ctor_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WanderSystem(-1f));
    }

    [Fact]
    [Trait("Category", "WanderValidation")]
    public void Validate_PositiveInfinity_FlagsError()
    {
        var config = new SimConfig { WanderStrength = float.PositiveInfinity };
        Assert.NotEmpty(config.Validate());
    }

    [Fact]
    [Trait("Category", "WanderValidation")]
    public void Validate_NaN_FlagsError()
    {
        var config = new SimConfig { WanderStrength = float.NaN };
        Assert.NotEmpty(config.Validate());
    }

    [Fact]
    [Trait("Category", "WanderValidation")]
    public void Validate_Negative_FlagsError()
    {
        var config = new SimConfig { WanderStrength = -1f };
        Assert.NotEmpty(config.Validate());
    }

    [Fact]
    [Trait("Category", "WanderValidation")]
    public void Validate_Zero_IsValid()
    {
        var config = new SimConfig { WanderStrength = 0f };
        Assert.DoesNotContain(config.Validate(), e => e.Contains("WanderStrength"));
    }
}
