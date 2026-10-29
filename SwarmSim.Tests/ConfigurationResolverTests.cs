using System.Text.Json;
using SwarmSim.Core;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Configuration;

namespace SwarmSim.Tests;

public sealed class ConfigurationResolverTests
{
    [Fact]
    public void SimResolution_AppliesDefaultsThenScenarioThenFileThenOverrides()
    {
        string path = Path.Combine(Path.GetTempPath(), $"swarm_layers_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"maxspeed":13,"AlignmentWeight":4,"AggressionMatrix":[[0,-1],[1,0]]}""");
            var defaults = new SimConfig { WorldWidth = 600f, MaxSpeed = 3f, AlignmentWeight = 0.25f };
            var overrides = new Dictionary<string, JsonElement>
            {
                ["MAXSPEED"] = JsonSerializer.SerializeToElement(17f),
                ["Seed"] = JsonSerializer.SerializeToElement(91u)
            };

            SimConfig config = ConfigurationResolver.ResolveSimConfig("balanced", path, overrides, defaults);

            Assert.Equal(600f, config.WorldWidth); // untouched default
            Assert.Equal(7.5f, config.SeparationWeight); // named scenario
            Assert.Equal(4f, config.AlignmentWeight); // file
            Assert.Equal(17f, config.MaxSpeed); // explicit override
            Assert.Equal(91u, config.Seed);
            Assert.Equal(-1f, config.AggressionMatrix[0, 1]);
            Assert.Equal(3f, defaults.MaxSpeed);
            Assert.Equal(0.25f, defaults.AlignmentWeight);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CanonicalResolution_UsesSameOrderAndMapsScenarioSpeed()
    {
        string path = Path.Combine(Path.GetTempPath(), $"swarm_layers_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"TargetSpeed":13,"AlignmentWeight":4}""");
            var defaults = new CanonicalWorldSettings { WorldWidth = 600f, TargetSpeed = 3f };
            var overrides = new Dictionary<string, JsonElement> { ["targetspeed"] = JsonSerializer.SerializeToElement(17f) };

            CanonicalWorldSettings settings = ConfigurationResolver.ResolveCanonicalSettings("balanced", path, overrides, defaults);

            Assert.Equal(600f, settings.WorldWidth);
            Assert.Equal(7.5f, settings.SeparationWeight);
            Assert.Equal(4f, settings.AlignmentWeight);
            Assert.Equal(17f, settings.TargetSpeed);
            Assert.Equal(3f, defaults.TargetSpeed);
            Assert.Equal(10f, ConfigurationResolver.ResolveCanonicalSettings("balanced").TargetSpeed);
            Assert.Equal(new CanonicalWorldSettings().Seed, settings.Seed);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Balanced_StaticFactoryNamedScenarioAndFile_ResolveIdentically()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "configs", "balanced.json");
        SimConfig fromFactory = SimConfig.Balanced();
        SimConfig fromName = ConfigurationResolver.ResolveSimConfig("BALANCED");
        SimConfig fromFile = ConfigurationResolver.ResolveSimConfig(filePath: path);

        Assert.Equal(fromFactory.ToJson(), fromName.ToJson());
        Assert.Equal(fromFactory.ToJson(), fromFile.ToJson());
    }

    [Theory]
    [InlineData("peaceful-flocks")]
    [InlineData("warbands")]
    [InlineData("rapid-evolution")]
    public void ExistingStaticScenarioFactories_KeepTheirValues(string scenario)
    {
        SimConfig fromFactory = scenario switch
        {
            "peaceful-flocks" => SimConfig.PeacefulFlocks(),
            "warbands" => SimConfig.Warbands(),
            "rapid-evolution" => SimConfig.RapidEvolution(),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        Assert.Equal(fromFactory.ToJson(), ConfigurationResolver.ResolveSimConfig(scenario).ToJson());
    }

    [Fact]
    public void Resolution_ValidatesAfterApplyingOverrides()
    {
        string path = Path.Combine(Path.GetTempPath(), $"swarm_layers_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"MaxSpeed":-1}""");
            var validOverride = new Dictionary<string, JsonElement> { ["MaxSpeed"] = JsonSerializer.SerializeToElement(12f) };

            Assert.Throws<ArgumentException>(() => ConfigurationResolver.ResolveSimConfig(filePath: path));
            Assert.Equal(12f, ConfigurationResolver.ResolveSimConfig(filePath: path, overrides: validOverride).MaxSpeed);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Resolution_RejectsUnknownScenarioAndOverrideNames()
    {
        Assert.Throws<ArgumentException>(() => ConfigurationResolver.ResolveSimConfig("unknown"));
        Assert.Throws<ArgumentException>(() => ConfigurationResolver.ResolveCanonicalSettings("unknown"));
        var overrides = new Dictionary<string, JsonElement> { ["MaxSpeeed"] = JsonSerializer.SerializeToElement(1f) };
        Assert.Throws<ArgumentException>(() => ConfigurationResolver.ResolveSimConfig(overrides: overrides));
        Assert.Throws<ArgumentException>(() => ConfigurationResolver.ResolveCanonicalSettings(overrides: overrides));
    }

    [Fact]
    public void Resolution_RejectsUnknownFileFieldsAndWrongEngineVocabulary()
    {
        string path = Path.Combine(Path.GetTempPath(), $"swarm_layers_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"MaxSpeeed":12}""");
            Assert.Throws<ArgumentException>(() => ConfigurationResolver.ResolveSimConfig(filePath: path));
            Assert.Throws<ArgumentException>(() => ConfigurationResolver.ResolveCanonicalSettings(filePath: path));

            File.WriteAllText(path, """{"MaxSpeed":12}""");
            Assert.Equal(12f, ConfigurationResolver.ResolveSimConfig(filePath: path).MaxSpeed);
            var error = Assert.Throws<ArgumentException>(() => ConfigurationResolver.ResolveCanonicalSettings(filePath: path));
            Assert.Contains("MaxSpeed", error.Message, StringComparison.Ordinal);

            File.WriteAllText(path, """{"TargetSpeed":12}""");
            Assert.Equal(12f, ConfigurationResolver.ResolveCanonicalSettings(filePath: path).TargetSpeed);
            Assert.Throws<ArgumentException>(() => ConfigurationResolver.ResolveSimConfig(filePath: path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Resolution_DoesNotShareMutableAggressionMatrices()
    {
        SimConfig first = ConfigurationResolver.ResolveSimConfig("warbands");
        SimConfig second = ConfigurationResolver.ResolveSimConfig("warbands");
        first.AggressionMatrix[0, 1] = -1f;

        Assert.Equal(1f, second.AggressionMatrix[0, 1]);
        Assert.Equal(1f, ConfigurationResolver.ResolveSimConfig("warbands").AggressionMatrix[0, 1]);
    }

    [Fact]
    public void CanonicalBridge_PreservesRendererMappingAndExplicitCapacity()
    {
        var config = new SimConfig
        {
            Seed = 91u,
            InitialCapacity = 3,
            MaxSpeed = 0.005f,
            MaxForce = 0.005f,
            SenseRadius = 0.5f,
            SeparationRadius = 0f,
            SeparationWeight = -1f,
            AlignmentWeight = -2f,
            CohesionWeight = -3f,
            MaxNeighbors = 0,
            WorldWidth = 500f,
            WorldHeight = 300f,
            FixedDeltaTime = 0.02f,
            WanderStrength = 0.25f,
            WanderRate = 2.25f
        };

        CanonicalWorldSettings settings = CanonicalWorldSettings.FromSimConfig(config, initialCapacity: 11);

        Assert.Equal(11, settings.InitialCapacity);
        Assert.Equal(3, CanonicalWorldSettings.FromSimConfig(config).InitialCapacity);
        Assert.Equal(91u, settings.Seed);
        Assert.Equal(0.01f, settings.TargetSpeed);
        Assert.Equal(0.01f, settings.MaxForce);
        Assert.Equal(1f, settings.SenseRadius);
        Assert.Equal(1f, settings.SeparationRadius);
        Assert.Equal(0f, settings.SeparationWeight);
        Assert.Equal(0f, settings.AlignmentWeight);
        Assert.Equal(0f, settings.CohesionWeight);
        Assert.Equal(0, settings.MaxNeighbors);
        Assert.Equal(500f, settings.WorldWidth);
        Assert.Equal(300f, settings.WorldHeight);
        Assert.Equal(0.02f, settings.FixedDeltaTime);
        Assert.Equal(0.25f, settings.WanderStrength);
        Assert.Equal(2.25f, settings.WanderRate);
    }
}
