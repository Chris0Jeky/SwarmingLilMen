using SwarmSim.Core;

namespace SwarmSim.Tests;

public sealed class CompactionIntervalValidationTests
{
    [Fact]
    [Trait("Category", "InputValidationConfig")]
    public void Validate_RejectsZeroCompactionInterval()
    {
        var errors = new SimConfig { CompactionInterval = 0 }.Validate();
        Assert.Contains(errors, e => e.StartsWith("CompactionInterval ", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "InputValidationConfig")]
    public void Validate_RejectsNegativeCompactionInterval()
    {
        var errors = new SimConfig { CompactionInterval = -1 }.Validate();
        Assert.Contains(errors, e => e.StartsWith("CompactionInterval ", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "InputValidationConfig")]
    public void Validate_AcceptsDefaultCompactionInterval()
    {
        var config = new SimConfig();
        Assert.Equal(600, config.CompactionInterval);
        Assert.DoesNotContain(config.Validate(), e => e.StartsWith("CompactionInterval ", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "InputValidationConfig")]
    public void Validate_AcceptsMinimumCompactionInterval()
    {
        var errors = new SimConfig { CompactionInterval = 1 }.Validate();
        Assert.DoesNotContain(errors, e => e.StartsWith("CompactionInterval ", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "InputValidationConfig")]
    public void World_RejectsZeroCompactionInterval()
    {
        var config = new SimConfig { CompactionInterval = 0 };
        var ex = Assert.Throws<ArgumentException>(() => new World(config));
        Assert.Contains("CompactionInterval", ex.Message, StringComparison.Ordinal);
    }
}
