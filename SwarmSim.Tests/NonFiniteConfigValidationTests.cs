using SwarmSim.Core;
using SwarmSim.Core.Canonical;

namespace SwarmSim.Tests;

public sealed class NonFiniteConfigValidationTests
{
    [Fact]
    public void Validate_DefaultConfig_Passes()
    {
        Assert.Empty(new SimConfig().Validate());
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Validate_RejectsNonFiniteWorldWidth(float value)
    {
        var errors = new SimConfig { WorldWidth = value }.Validate();
        Assert.Contains(errors, e => e.StartsWith("WorldWidth ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Validate_RejectsNonFiniteWorldHeight(float value)
    {
        var errors = new SimConfig { WorldHeight = value }.Validate();
        Assert.Contains(errors, e => e.StartsWith("WorldHeight ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Validate_RejectsNonFiniteFixedDeltaTime(float value)
    {
        var errors = new SimConfig { FixedDeltaTime = value }.Validate();
        Assert.Contains(errors, e => e.StartsWith("FixedDeltaTime ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Validate_RejectsNonFiniteMaxSpeed(float value)
    {
        var errors = new SimConfig { MaxSpeed = value }.Validate();
        Assert.Contains(errors, e => e.StartsWith("MaxSpeed ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Validate_RejectsNonFiniteMaxForce(float value)
    {
        var errors = new SimConfig { MaxForce = value }.Validate();
        Assert.Contains(errors, e => e.StartsWith("MaxForce ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Validate_RejectsNonFiniteGridCellSize(float value)
    {
        var errors = new SimConfig { GridCellSize = value }.Validate();
        Assert.Contains(errors, e => e.StartsWith("GridCellSize ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Validate_RejectsNonFiniteSenseRadius(float value)
    {
        var errors = new SimConfig { SenseRadius = value }.Validate();
        Assert.Contains(errors, e => e.StartsWith("SenseRadius ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Validate_RejectsNonFiniteFieldOfView(float value)
    {
        var errors = new SimConfig { FieldOfView = value }.Validate();
        Assert.Contains(errors, e => e.StartsWith("FieldOfView ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Validate_RejectsNonFiniteFriction(float value)
    {
        var errors = new SimConfig { Friction = value }.Validate();
        Assert.Contains(errors, e => e.StartsWith("Friction ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Validate_RejectsNonFiniteMutationRate(float value)
    {
        var errors = new SimConfig { MutationRate = value }.Validate();
        Assert.Contains(errors, e => e.StartsWith("MutationRate ", StringComparison.Ordinal));
    }

    [Fact]
    public void CanonicalWorld_DefaultSettings_Constructs()
    {
        var world = CreateWorld(new CanonicalWorldSettings());
        Assert.NotNull(world);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void CanonicalWorld_RejectsNonFiniteTargetSpeed(float value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateWorld(new CanonicalWorldSettings { TargetSpeed = value }));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void CanonicalWorld_RejectsNonFiniteSenseRadius(float value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateWorld(new CanonicalWorldSettings { SenseRadius = value }));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void CanonicalWorld_RejectsNonFiniteMaxForce(float value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateWorld(new CanonicalWorldSettings { MaxForce = value }));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void CanonicalWorld_RejectsNonFiniteFieldOfView(float value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateWorld(new CanonicalWorldSettings { FieldOfView = value }));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void CanonicalWorld_RejectsNonFiniteWorldWidth(float value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateWorld(new CanonicalWorldSettings { WorldWidth = value }));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void CanonicalWorld_RejectsNonFiniteWorldHeight(float value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateWorld(new CanonicalWorldSettings { WorldHeight = value }));
    }

    private static CanonicalWorld CreateWorld(CanonicalWorldSettings settings) => new(
        settings,
        new GridSpatialIndex(10f, 1920f, 1080f));
}
