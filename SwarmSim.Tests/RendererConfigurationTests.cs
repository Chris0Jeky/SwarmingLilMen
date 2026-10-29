using System.Reflection;
using SwarmSim.Core;
using SwarmSim.Core.Canonical;
using RenderProgram = SwarmSim.Render.Program;

namespace SwarmSim.Tests;

public sealed class RendererConfigurationTests
{
    [Fact]
    public void StartupWithoutAuthoredSourcesPreservesRendererDefaults()
    {
        SimConfig result = RenderProgram.ResolveStartupConfiguration(null, null);

        Assert.Equal(RenderProgram.CreateDefaultBaseConfig().ToJson(), result.ToJson());
        Assert.Equal(0f, result.WanderStrength);
        Assert.Equal(10f, result.MaxSpeed);
        Assert.Equal(1f / 60f, result.FixedDeltaTime);
        Assert.Equal(0f, result.AggressionMatrix[0, 1]);
    }

    [Theory]
    [InlineData("balanced", "Balanced (Recommended)")]
    [InlineData("strong-separation", "Strong Separation")]
    [InlineData("tight-flocking", "Tight Flocking")]
    [InlineData("fast-loose", "Fast & Loose")]
    [InlineData("slow-cohesive", "Slow & Cohesive")]
    public void StartupPresetIdsAndDisplayNamesMatchEveryFKeyConfiguration(string id, string displayName)
    {
        SimConfig registered = RegisteredPresetConfig(id);
        SimConfig fromId = RenderProgram.ResolveStartupConfiguration(id, null);
        SimConfig fromDisplayName = RenderProgram.ResolveStartupConfiguration(displayName.ToUpperInvariant(), null);

        Assert.Equal(registered.ToJson(), fromId.ToJson());
        Assert.Equal(registered.ToJson(), fromDisplayName.ToJson());
        Assert.Equal(registered.ToJson(), RenderProgram.CopyConfig(fromId).ToJson());
    }

    [Fact]
    public void BalancedRendererPresetFileAndStaticFactoryMatchEveryProperty()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "configs", "balanced.json");
        string expected = SimConfig.Balanced().ToJson();

        Assert.Equal(expected, RegisteredPresetConfig("balanced").ToJson());
        Assert.Equal(expected, RenderProgram.ResolveStartupConfiguration("balanced", null).ToJson());
        Assert.Equal(expected, RenderProgram.ResolveStartupConfiguration(null, path).ToJson());
    }

    [Fact]
    public void FileWithoutPresetKeepsExistingSimConfigDefaults()
    {
        WithConfig("""{"MaxSpeed":9,"SeparationWeight":11,"CollisionAvoidanceBoost":8.5}""", path =>
        {
            SimConfig oldFileBehavior = SimConfig.LoadFromJson(path);
            SimConfig result = RenderProgram.ResolveStartupConfiguration(null, path);

            Assert.Equal(oldFileBehavior.ToJson(), result.ToJson());
            Assert.Equal(new SimConfig().SenseRadius, result.SenseRadius);
            Assert.Equal(new SimConfig().FixedDeltaTime, result.FixedDeltaTime);
        });
    }

    [Fact]
    public void FileOverridesSelectedPresetWhileOmittedValuesKeepThatPreset()
    {
        WithConfig("""{"MaxSpeed":12,"Seed":9,"CollisionAvoidanceBoost":8.5}""", path =>
        {
            SimConfig result = RenderProgram.ResolveStartupConfiguration("balanced", path);

            Assert.Equal(12f, result.MaxSpeed);
            Assert.Equal(9u, result.Seed);
            Assert.Equal(8.5f, result.CollisionAvoidanceBoost);
            Assert.Equal(7.5f, result.SeparationWeight);
            Assert.Equal(2.2f, result.AlignmentWeight);
            Assert.Equal(0.45f, result.WanderStrength);
            Assert.Equal(110f, result.SenseRadius);
        });
    }

    [Fact]
    public void InvalidPresetIsNotSilentlyIgnoredWhenAFileIsAlsoSupplied()
    {
        WithConfig("{}", path =>
        {
            var error = Assert.Throws<ArgumentException>(() =>
                RenderProgram.ResolveStartupConfiguration("not-a-preset", path));

            Assert.Equal("presetName", error.ParamName);
            Assert.Contains("Unknown preset", error.Message, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("""{"MaxSpeeed":12}""")]
    [InlineData("""{"TargetSpeed":12}""")]
    public void RendererFilesRejectTyposAndNativeCanonicalFieldNames(string json)
    {
        WithConfig(json, path => Assert.Throws<ArgumentException>(() =>
            RenderProgram.ResolveStartupConfiguration(null, path)));
    }

    [Theory]
    [InlineData(0, 1024)]
    [InlineData(400, 1024)]
    [InlineData(600, 1200)]
    public void SharedCanonicalBridgeKeepsRendererCapacityAndClamps(int agentCount, int expectedCapacity)
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
            FixedDeltaTime = 0.02f
        };

        CanonicalWorldSettings result = RenderProgram.BuildCanonicalWorldSettings(config, agentCount);

        Assert.Equal(expectedCapacity, result.InitialCapacity);
        Assert.Equal(0.01f, result.TargetSpeed);
        Assert.Equal(0.01f, result.MaxForce);
        Assert.Equal(1f, result.SenseRadius);
        Assert.Equal(1f, result.SeparationRadius);
        Assert.Equal(0f, result.SeparationWeight);
        Assert.Equal(0f, result.AlignmentWeight);
        Assert.Equal(0f, result.CohesionWeight);
        Assert.Equal(CanonicalWorldSettings.FromSimConfig(config, expectedCapacity).ToJson(), result.ToJson());
    }

    [Fact]
    public void PresetListingRemainsTheExistingFiveEntryTranscript()
    {
        string expected = string.Join(Environment.NewLine,
            "Available presets:",
            "  balanced        - Balanced (Recommended) :: Canonical boids tuning with smooth flocking",
            "  strong-separation - Strong Separation :: Agents prioritize personal space, useful for dense scenes",
            "  tight-flocking  - Tight Flocking :: Emphasizes cohesion/alignment for ribbon-like formations",
            "  fast-loose      - Fast & Loose :: Higher speed ceiling with lighter cohesion",
            "  slow-cohesive   - Slow & Cohesive :: Lower speed with high cohesion for schooling behavior",
            "");

        Assert.Equal(expected, RenderProgram.FormatPresetList());
    }

    [Theory]
    [InlineData(nameof(SimConfig.SeparationCrowdingThreshold), true)]
    [InlineData(nameof(SimConfig.SeparationCrowdingBoost), true)]
    [InlineData(nameof(SimConfig.CollisionAvoidanceRadius), true)]
    [InlineData(nameof(SimConfig.CollisionAvoidanceBoost), true)]
    [InlineData(nameof(SimConfig.MaxNeighbors), true)]
    [InlineData(nameof(SimConfig.SeparationCrowdingThreshold), false)]
    [InlineData(nameof(SimConfig.SeparationCrowdingBoost), false)]
    [InlineData(nameof(SimConfig.CollisionAvoidanceRadius), false)]
    [InlineData(nameof(SimConfig.CollisionAvoidanceBoost), false)]
    [InlineData(nameof(SimConfig.MaxNeighbors), false)]
    public void RendererCopiesPreserveAuthoredNeighborAndAvoidanceFields(string propertyName, bool copyTemplate)
    {
        var source = new SimConfig
        {
            SeparationCrowdingThreshold = 27,
            SeparationCrowdingBoost = 6.25f,
            CollisionAvoidanceRadius = 22f,
            CollisionAvoidanceBoost = 9f,
            MaxNeighbors = 7
        };

        SimConfig result = copyTemplate
            ? RenderProgram.CopyConfig(source)
            : RenderProgram.BuildCurrentConfig(source);
        var property = typeof(SimConfig).GetProperty(propertyName)!;

        Assert.Equal(property.GetValue(source), property.GetValue(result));
    }

    [Fact]
    public void RenderedWorldKeepsItsExistingFixedWindowDimensions()
    {
        var source = new SimConfig { WorldWidth = 1234f, WorldHeight = 678f };
        SimConfig copied = RenderProgram.CopyConfig(source);
        SimConfig rendered = RenderProgram.BuildCurrentConfig(copied);

        Assert.Equal(1234f, copied.WorldWidth);
        Assert.Equal(678f, copied.WorldHeight);
        Assert.Equal(1920f, rendered.WorldWidth);
        Assert.Equal(1080f, rendered.WorldHeight);
    }

    private static SimConfig RegisteredPresetConfig(string id)
    {
        var presets = (Array)typeof(RenderProgram).GetField("Presets", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!;
        foreach (object preset in presets)
        {
            Type type = preset.GetType();
            if (Equals(type.GetProperty("Id")!.GetValue(preset), id))
                return Assert.IsType<SimConfig>(type.GetProperty("Config")!.GetValue(preset));
        }
        throw new InvalidOperationException($"Missing renderer preset {id}.");
    }

    private static void WithConfig(string json, Action<string> assertion)
    {
        string path = Path.Combine(Path.GetTempPath(), $"swarm-renderer-config-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, json);
            assertion(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
