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
    public void Validate_RejectsUnsupportedSeed()
    {
        var supported = new SimConfig { Seed = int.MaxValue };
        var unsupported = new SimConfig { Seed = (uint)int.MaxValue + 1u };

        Assert.DoesNotContain(supported.Validate(), error => error.StartsWith("Seed ", StringComparison.Ordinal));
        Assert.Contains(unsupported.Validate(), error => error.StartsWith("Seed ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(nameof(SimConfig.SeparationWeight), float.NaN)]
    [InlineData(nameof(SimConfig.SeparationWeight), float.PositiveInfinity)]
    [InlineData(nameof(SimConfig.AlignmentWeight), float.NaN)]
    [InlineData(nameof(SimConfig.AlignmentWeight), float.NegativeInfinity)]
    [InlineData(nameof(SimConfig.CohesionWeight), float.NaN)]
    [InlineData(nameof(SimConfig.CohesionWeight), float.PositiveInfinity)]
    [InlineData(nameof(SimConfig.CollisionAvoidanceBoost), float.NaN)]
    [InlineData(nameof(SimConfig.CollisionAvoidanceBoost), float.PositiveInfinity)]
    [InlineData(nameof(SimConfig.CollisionAvoidanceRadius), float.NaN)]
    [InlineData(nameof(SimConfig.CollisionAvoidanceRadius), float.NegativeInfinity)]
    [InlineData(nameof(SimConfig.SeparationCrowdingBoost), float.NaN)]
    [InlineData(nameof(SimConfig.SeparationCrowdingBoost), float.PositiveInfinity)]
    public void NonFiniteLegacySteeringParameter_IsRejectedByName(string parameter, float value)
    {
        var config = parameter switch
        {
            nameof(SimConfig.SeparationWeight) => new SimConfig { SeparationWeight = value },
            nameof(SimConfig.AlignmentWeight) => new SimConfig { AlignmentWeight = value },
            nameof(SimConfig.CohesionWeight) => new SimConfig { CohesionWeight = value },
            nameof(SimConfig.CollisionAvoidanceBoost) => new SimConfig { CollisionAvoidanceBoost = value },
            nameof(SimConfig.CollisionAvoidanceRadius) => new SimConfig { CollisionAvoidanceRadius = value },
            nameof(SimConfig.SeparationCrowdingBoost) => new SimConfig { SeparationCrowdingBoost = value },
            _ => throw new ArgumentOutOfRangeException(nameof(parameter))
        };

        Assert.Contains(config.Validate(), e => e.StartsWith(parameter + " ", StringComparison.Ordinal));
        var ex = Assert.Throws<ArgumentException>(() => new World(config, seed: 42u));
        Assert.Contains(parameter, ex.Message);
    }

    [Fact]
    public void FiniteLegacySteeringParameters_IncludingNegatives_StayValid()
    {
        var config = new SimConfig
        {
            SeparationWeight = -1f,
            AlignmentWeight = 0f,
            CohesionWeight = -0.5f,
            CollisionAvoidanceBoost = 0f,
            SeparationCrowdingBoost = 0f
        };

        Assert.Empty(config.Validate());
        Assert.Empty(SimConfig.PeacefulFlocks().Validate());
        Assert.Empty(SimConfig.Warbands().Validate());
    }

    [Fact]
    public void ValidLegacySteeringConfig_KeepsNearbyBoidsFiniteAfterTick()
    {
        var world = new World(new SimConfig(), seed: 42u);
        for (int i = 0; i < 8; i++)
            world.AddAgent(100f + i * 3f, 100f + (i % 3) * 3f);

        world.Tick();

        for (int i = 0; i < world.Count; i++)
        {
            Assert.True(float.IsFinite(world.X[i]) && float.IsFinite(world.Y[i]));
            Assert.True(float.IsFinite(world.Vx[i]) && float.IsFinite(world.Vy[i]));
        }
    }
}
