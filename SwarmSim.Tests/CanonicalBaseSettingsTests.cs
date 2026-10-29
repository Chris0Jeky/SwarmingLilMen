using SwarmSim.Core;
using SwarmSim.Core.Canonical;
using RenderProgram = SwarmSim.Render.Program;

namespace SwarmSim.Tests;

public sealed class CanonicalBaseSettingsTests
{
    public static IEnumerable<object[]> InvalidBaseInputs()
    {
        foreach (string property in new[]
        {
            nameof(CanonicalWorldSettings.FixedDeltaTime),
            nameof(CanonicalWorldSettings.SeparationRadius),
            nameof(CanonicalWorldSettings.SeparationWeight),
            nameof(CanonicalWorldSettings.AlignmentWeight),
            nameof(CanonicalWorldSettings.CohesionWeight)
        })
        {
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                yield return new object[] { property, value };
        }

        yield return new object[] { nameof(CanonicalWorldSettings.FixedDeltaTime), 0f };
        yield return new object[] { nameof(CanonicalWorldSettings.FixedDeltaTime), -1f };
    }

    [Theory]
    [MemberData(nameof(InvalidBaseInputs))]
    public void CtorRejectsInvalidBaseInputs(string property, float value)
    {
        var settings = SettingsWith(property, value);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Create(settings));

        Assert.Equal("settings", exception.ParamName);
        Assert.Contains(property, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(nameof(CanonicalWorldSettings.SeparationRadius), 0f)]
    [InlineData(nameof(CanonicalWorldSettings.SeparationRadius), -1f)]
    [InlineData(nameof(CanonicalWorldSettings.SeparationWeight), 0f)]
    [InlineData(nameof(CanonicalWorldSettings.SeparationWeight), -1f)]
    [InlineData(nameof(CanonicalWorldSettings.AlignmentWeight), 0f)]
    [InlineData(nameof(CanonicalWorldSettings.AlignmentWeight), -1f)]
    [InlineData(nameof(CanonicalWorldSettings.CohesionWeight), 0f)]
    [InlineData(nameof(CanonicalWorldSettings.CohesionWeight), -1f)]
    public void FiniteClampedRuleInputsRemainUsable(string property, float value)
    {
        var settings = SettingsWith(property, value);
        var world = Create(settings);
        Assert.True(world.TryAddBoid(new Vec2(10, 10), new Vec2(1, 0)));
        Assert.True(world.TryAddBoid(new Vec2(11, 10), new Vec2(-1, 0)));

        world.Step(settings.FixedDeltaTime);

        Assert.Equal(1ul, world.TickCount);
        foreach (var boid in world.Boids)
        {
            Assert.True(float.IsFinite(boid.Position.X));
            Assert.True(float.IsFinite(boid.Position.Y));
            Assert.True(float.IsFinite(boid.Velocity.X));
            Assert.True(float.IsFinite(boid.Velocity.Y));
        }
    }

    [Fact]
    public void SmallestPositiveConfiguredTimestepRemainsAccepted()
    {
        var settings = new CanonicalWorldSettings { InitialCapacity = 1, FixedDeltaTime = float.Epsilon };
        var world = Create(settings);
        Assert.True(world.TryAddBoid(new Vec2(10, 10), new Vec2(1, 0)));

        world.Step(settings.FixedDeltaTime);

        Assert.Equal(1ul, world.TickCount);
        Assert.True(float.IsFinite(world.Boids[0].Position.X));
        Assert.True(float.IsFinite(world.Boids[0].Velocity.X));
    }

    [Fact]
    public void CanonicalBoundaryRejectsNaNThatSurvivesRendererMapping()
    {
        var config = new SimConfig { SeparationWeight = float.NaN };
        var settings = RenderProgram.BuildCanonicalWorldSettings(config, 2);

        Assert.True(float.IsNaN(settings.SeparationWeight));
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Create(settings));
        Assert.Equal("settings", exception.ParamName);
        Assert.Contains(nameof(CanonicalWorldSettings.SeparationWeight), exception.Message, StringComparison.Ordinal);
    }

    private static CanonicalWorldSettings SettingsWith(string property, float value)
    {
        var settings = new CanonicalWorldSettings { InitialCapacity = 2 };
        typeof(CanonicalWorldSettings).GetProperty(property)!.SetValue(settings, value);
        return settings;
    }

    private static CanonicalWorld Create(CanonicalWorldSettings settings) => new(
        settings, new NaiveSpatialIndex(settings.WorldWidth, settings.WorldHeight));
}
