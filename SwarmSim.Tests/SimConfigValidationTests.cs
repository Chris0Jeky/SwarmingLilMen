using System.Globalization;
using SwarmSim.Core;

namespace SwarmSim.Tests;

public sealed class SimConfigValidationTests
{
    public static IEnumerable<object[]> InvalidActiveValues()
    {
        foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            yield return new object[] { nameof(SimConfig.SeparationRadius), value };
            yield return new object[] { nameof(SimConfig.InitialEnergy), value };
            yield return new object[] { nameof(SimConfig.InitialHealth), value };
            yield return new object[] { nameof(SimConfig.WanderRate), value };
            yield return new object[] { nameof(SimConfig.MaxTurnRateDegPerSecond), value };
            yield return new object[] { nameof(SimConfig.WhiskerTimeHorizon), value };
            yield return new object[] { nameof(SimConfig.WhiskerWeight), value };
            yield return new object[] { nameof(SimConfig.SeparationPriorityRadiusFactor), value };
            yield return new object[] { nameof(SimConfig.SeparationPriorityExitFactor), value };
            yield return new object[] { nameof(SimConfig.SeparationPriorityBoost), value };
            yield return new object[] { nameof(SimConfig.SeparationPriorityHoldTime), value };
            yield return new object[] { nameof(SimConfig.SeparationPriorityRampInTime), value };
            yield return new object[] { nameof(SimConfig.SeparationPriorityRampOutTime), value };
            yield return new object[] { nameof(SimConfig.SeparationSpeedDroop), value };
        }

        yield return new object[] { nameof(SimConfig.SeparationRadius), -1f };
        yield return new object[] { nameof(SimConfig.WanderRate), -1f };
        yield return new object[] { nameof(SimConfig.MaxTurnRateDegPerSecond), -1f };
        yield return new object[] { nameof(SimConfig.WhiskerWeight), -1f };
        yield return new object[] { nameof(SimConfig.SeparationPriorityBoost), -1f };
        yield return new object[] { nameof(SimConfig.SeparationSpeedDroop), -1f };
        yield return new object[] { nameof(SimConfig.SeparationSpeedDroop), 1.1f };
    }

    [Theory]
    [MemberData(nameof(InvalidActiveValues))]
    public void InvalidActiveValue_IsRejectedBeforeWorldAllocation(string property, float value)
    {
        var config = new SimConfig { InitialCapacity = 2 };
        typeof(SimConfig).GetProperty(property)!.SetValue(config, value);

        Assert.Contains(config.Validate(), error => error.StartsWith(property + " ", StringComparison.Ordinal));
        var error = Assert.Throws<ArgumentException>(() => new World(config));
        Assert.Contains(property, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NegativeCrowdingThreshold_IsRejected(int threshold)
    {
        var config = new SimConfig { InitialCapacity = 2, SeparationCrowdingThreshold = threshold };

        Assert.Contains(config.Validate(), error => error.StartsWith("SeparationCrowdingThreshold ", StringComparison.Ordinal));
        Assert.Throws<ArgumentException>(() => new World(config));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void DisabledRulesAndUnlimitedNeighbors_RemainUsable(int maxNeighbors)
    {
        var config = new SimConfig
        {
            InitialCapacity = 2,
            MaxNeighbors = maxNeighbors,
            SeparationRadius = 0f,
            CollisionAvoidanceRadius = 0f,
            SeparationCrowdingThreshold = 0,
            SeparationWeight = -1f,
            AlignmentWeight = 0f,
            CohesionWeight = 0f,
            InitialEnergy = 0f,
            InitialHealth = 0f
        };

        Assert.Empty(config.Validate());
        var world = new World(config);
        world.AddAgent(10f, 10f);
        world.AddAgent(12f, 10f);
        world.Tick();

        Assert.All(world.Vx.Take(world.Count), value => Assert.True(float.IsFinite(value)));
        Assert.All(world.Vy.Take(world.Count), value => Assert.True(float.IsFinite(value)));
    }

    [Theory]
    [InlineData("SeparationRadius", "-1")]
    [InlineData("SeparationRadius", "1e100")]
    [InlineData("InitialEnergy", "1e100")]
    [InlineData("InitialHealth", "-1e100")]
    [InlineData("SeparationCrowdingThreshold", "-1")]
    public void JsonLoading_RejectsInvalidActiveValues(string property, string literal)
    {
        string path = Path.Combine(Path.GetTempPath(), $"swarm_config_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, $"{{\"{property}\":{literal},\"InitialCapacity\":2}}");

            var error = Assert.Throws<ArgumentException>(() => SimConfig.LoadFromJson(path));
            Assert.Contains(property, error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void JsonLoading_PreservesDisabledRulesAndUnlimitedNeighbors(int maxNeighbors)
    {
        string path = Path.Combine(Path.GetTempPath(), $"swarm_config_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, $$"""
                {"MaxNeighbors": {{maxNeighbors.ToString(CultureInfo.InvariantCulture)}}, "SeparationRadius": 0,
                 "CollisionAvoidanceRadius": 0, "SeparationCrowdingThreshold": 0, "InitialCapacity": 2}
                """);

            SimConfig config = SimConfig.LoadFromJson(path);

            Assert.Equal(maxNeighbors, config.MaxNeighbors);
            Assert.Equal(0f, config.SeparationRadius);
            Assert.Equal(0f, config.CollisionAvoidanceRadius);
            Assert.NotNull(new World(config));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReservedFields_DoNotAcquireSpeculativeRanges()
    {
        var config = new SimConfig
        {
            InitialCapacity = 2,
            AttackRadius = -1f,
            AttackCooldown = -1f,
            AttackDamage = -1f,
            BaseDrain = -1f,
            MaxEnergy = -1f,
            MaxHealth = -1f,
            MutationStdDev = -1f,
            InitialEnergy = -1f,
            InitialHealth = -1f,
            CollisionAvoidanceRadius = -1f,
            CollisionAvoidanceBoost = -1f,
            WhiskerTimeHorizon = -1f,
            SeparationPriorityRadiusFactor = -1f,
            SeparationPriorityExitFactor = -1f,
            SeparationPriorityHoldTime = -1f,
            SeparationPriorityRampInTime = -1f,
            SeparationPriorityRampOutTime = -1f
        };

        Assert.Empty(config.Validate());
        Assert.NotNull(new World(config));
    }

    [Theory]
    [InlineData(nameof(SimConfig.BoundaryMode))]
    [InlineData(nameof(SimConfig.SpeedModel))]
    public void UndefinedEnumValue_IsRejected(string property)
    {
        var config = property == nameof(SimConfig.BoundaryMode)
            ? new SimConfig { InitialCapacity = 2, BoundaryMode = (BoundaryMode)99 }
            : new SimConfig { InitialCapacity = 2, SpeedModel = (SpeedModel)99 };

        Assert.Contains(config.Validate(), error => error.StartsWith(property + " ", StringComparison.Ordinal));
        Assert.Throws<ArgumentException>(() => new World(config));
    }
}
