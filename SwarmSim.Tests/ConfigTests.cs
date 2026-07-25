using SwarmSim.Core;

namespace SwarmSim.Tests;

public class ConfigTests
{
    [Fact]
    public void LoadFromJson_ReadsValues()
    {
        string path = Path.Combine(Path.GetTempPath(), $"swarm_config_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """
            {
              "MaxSpeed": 12,
              "SenseRadius": 80,
              "SeparationWeight": 4.5
            }
            """);

            var config = SimConfig.LoadFromJson(path);

            Assert.Equal(12f, config.MaxSpeed);
            Assert.Equal(80f, config.SenseRadius);
            Assert.Equal(4.5f, config.SeparationWeight);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Validate_RejectsInvalidDeterminismInputs()
    {
        var supported = new SimConfig { Seed = int.MaxValue };
        var unsupported = new SimConfig { Seed = (uint)int.MaxValue + 1u };
        var negativeTurnRate = new SimConfig { MaxTurnRateDegPerSecond = -1f };

        Assert.DoesNotContain(supported.Validate(), error => error.StartsWith("Seed ", StringComparison.Ordinal));
        Assert.Contains(unsupported.Validate(), error => error.StartsWith("Seed ", StringComparison.Ordinal));
        Assert.Contains(
            negativeTurnRate.Validate(),
            error => error.StartsWith("MaxTurnRateDegPerSecond ", StringComparison.Ordinal));
    }
}
