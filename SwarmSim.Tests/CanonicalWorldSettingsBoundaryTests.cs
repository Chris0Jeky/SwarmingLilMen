using SwarmSim.Core.Canonical;
using SwarmSim.Core.Utils;

namespace SwarmSim.Tests;

public sealed class CanonicalWorldSettingsBoundaryTests
{
    private const float KnownCellSize = 10f;
    private const float KnownWidth = 1920f;
    private const float KnownHeight = 1080f;

    [Fact]
    public void Ctor_DefaultSettings_Constructs()
    {
        var settings = new CanonicalWorldSettings();

        AssertAccepted(settings);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Ctor_RejectsTargetSpeed(float value)
    {
        AssertRejected(new CanonicalWorldSettings { TargetSpeed = value });
    }

    [Theory]
    [InlineData(float.Epsilon)]
    public void Ctor_AcceptsTargetSpeedBoundary(float value)
    {
        AssertAccepted(new CanonicalWorldSettings { TargetSpeed = value });
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Ctor_RejectsSenseRadius(float value)
    {
        AssertRejected(new CanonicalWorldSettings { SenseRadius = value });
    }

    [Theory]
    [InlineData(float.Epsilon)]
    public void Ctor_AcceptsSenseRadiusBoundary(float value)
    {
        AssertAccepted(new CanonicalWorldSettings { SenseRadius = value });
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Ctor_RejectsMaxForce(float value)
    {
        AssertRejected(new CanonicalWorldSettings { MaxForce = value });
    }

    [Theory]
    [InlineData(float.Epsilon)]
    public void Ctor_AcceptsMaxForceBoundary(float value)
    {
        AssertAccepted(new CanonicalWorldSettings { MaxForce = value });
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(360.001f)]
    public void Ctor_RejectsFieldOfView(float value)
    {
        AssertRejected(new CanonicalWorldSettings { FieldOfView = value });
    }

    [Theory]
    [InlineData(float.Epsilon)]
    [InlineData(360f)]
    public void Ctor_AcceptsFieldOfViewBoundary(float value)
    {
        AssertAccepted(new CanonicalWorldSettings { FieldOfView = value });
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Ctor_RejectsWorldWidth(float value)
    {
        AssertRejected(new CanonicalWorldSettings { WorldWidth = value });
    }

    [Theory]
    [InlineData(float.Epsilon)]
    public void Ctor_AcceptsWorldWidthBoundary(float value)
    {
        AssertAccepted(new CanonicalWorldSettings { WorldWidth = value });
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Ctor_RejectsWorldHeight(float value)
    {
        AssertRejected(new CanonicalWorldSettings { WorldHeight = value });
    }

    [Theory]
    [InlineData(float.Epsilon)]
    public void Ctor_AcceptsWorldHeightBoundary(float value)
    {
        AssertAccepted(new CanonicalWorldSettings { WorldHeight = value });
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-1f)]
    public void Ctor_RejectsMaxTurnRateDegPerSecond(float value)
    {
        AssertRejected(new CanonicalWorldSettings { MaxTurnRateDegPerSecond = value });
    }

    [Theory]
    [InlineData(0f)]
    public void Ctor_AcceptsMaxTurnRateDegPerSecondBoundary(float value)
    {
        AssertAccepted(new CanonicalWorldSettings { MaxTurnRateDegPerSecond = value });
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-1f)]
    public void Ctor_RejectsWanderStrength(float value)
    {
        AssertRejected(new CanonicalWorldSettings { WanderStrength = value });
    }

    [Theory]
    [InlineData(0f)]
    public void Ctor_AcceptsWanderStrengthBoundary(float value)
    {
        AssertAccepted(new CanonicalWorldSettings { WanderStrength = value });
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-1f)]
    public void Ctor_RejectsWanderRate(float value)
    {
        AssertRejected(new CanonicalWorldSettings { WanderRate = value });
    }

    [Theory]
    [InlineData(0f)]
    public void Ctor_AcceptsWanderRateBoundary(float value)
    {
        AssertAccepted(new CanonicalWorldSettings { WanderRate = value });
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Ctor_RejectsWhiskerTimeHorizon(float value)
    {
        AssertRejected(new CanonicalWorldSettings { WhiskerTimeHorizon = value });
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Ctor_AcceptsWhiskerTimeHorizonBoundary(float value)
    {
        AssertAccepted(new CanonicalWorldSettings { WhiskerTimeHorizon = value });
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-1f)]
    public void Ctor_RejectsWhiskerWeight(float value)
    {
        AssertRejected(new CanonicalWorldSettings { WhiskerWeight = value });
    }

    [Theory]
    [InlineData(0f)]
    public void Ctor_AcceptsWhiskerWeightBoundary(float value)
    {
        AssertAccepted(new CanonicalWorldSettings { WhiskerWeight = value });
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Ctor_RejectsSeparationPriorityRadiusFactor(float value)
    {
        AssertRejected(new CanonicalWorldSettings { SeparationPriorityRadiusFactor = value });
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Ctor_AcceptsSeparationPriorityRadiusFactorBoundary(float value)
    {
        AssertAccepted(new CanonicalWorldSettings { SeparationPriorityRadiusFactor = value });
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Ctor_RejectsSeparationPriorityExitFactor(float value)
    {
        AssertRejected(new CanonicalWorldSettings { SeparationPriorityExitFactor = value });
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Ctor_AcceptsSeparationPriorityExitFactorBoundary(float value)
    {
        AssertAccepted(new CanonicalWorldSettings { SeparationPriorityExitFactor = value });
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-1f)]
    public void Ctor_RejectsSeparationPriorityBoost(float value)
    {
        AssertRejected(new CanonicalWorldSettings { SeparationPriorityBoost = value });
    }

    [Theory]
    [InlineData(0f)]
    public void Ctor_AcceptsSeparationPriorityBoostBoundary(float value)
    {
        AssertAccepted(new CanonicalWorldSettings { SeparationPriorityBoost = value });
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-1f)]
    [InlineData(1.001f)]
    public void Ctor_RejectsSeparationSpeedDroop(float value)
    {
        AssertRejected(new CanonicalWorldSettings { SeparationSpeedDroop = value });
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    public void Ctor_AcceptsSeparationSpeedDroopBoundary(float value)
    {
        AssertAccepted(new CanonicalWorldSettings { SeparationSpeedDroop = value });
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Ctor_RejectsSeparationPriorityHoldTime(float value)
    {
        AssertRejected(new CanonicalWorldSettings { SeparationPriorityHoldTime = value });
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Ctor_AcceptsSeparationPriorityHoldTimeBoundary(float value)
    {
        AssertAccepted(new CanonicalWorldSettings { SeparationPriorityHoldTime = value });
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Ctor_RejectsSeparationPriorityRampInTime(float value)
    {
        AssertRejected(new CanonicalWorldSettings { SeparationPriorityRampInTime = value });
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Ctor_AcceptsSeparationPriorityRampInTimeBoundary(float value)
    {
        AssertAccepted(new CanonicalWorldSettings { SeparationPriorityRampInTime = value });
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Ctor_RejectsSeparationPriorityRampOutTime(float value)
    {
        AssertRejected(new CanonicalWorldSettings { SeparationPriorityRampOutTime = value });
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Ctor_AcceptsSeparationPriorityRampOutTimeBoundary(float value)
    {
        AssertAccepted(new CanonicalWorldSettings { SeparationPriorityRampOutTime = value });
    }

    [Theory]
    [InlineData(Rng.MaxSupportedSeed + 1u)]
    [InlineData(uint.MaxValue)]
    public void Ctor_RejectsSeed(uint seed)
    {
        AssertRejected(new CanonicalWorldSettings { Seed = seed });
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(Rng.MaxSupportedSeed)]
    public void Ctor_AcceptsSeedBoundary(uint seed)
    {
        AssertAccepted(new CanonicalWorldSettings { Seed = seed });
    }

    private static void AssertRejected(CanonicalWorldSettings settings)
    {
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => Create(settings));
        Assert.Equal("settings", ex.ParamName);
    }

    private static void AssertAccepted(CanonicalWorldSettings settings)
    {
        CanonicalWorld world = Create(settings);
        Assert.Same(settings, world.Settings);
    }

    // A bad SenseRadius or world extent must fail in CanonicalWorld. The index is built from a
    // known-good cell size, and its extents follow the settings only while those extents are valid.
    private static CanonicalWorld Create(CanonicalWorldSettings settings)
    {
        float width = float.IsFinite(settings.WorldWidth) && settings.WorldWidth > 0f
            ? settings.WorldWidth
            : KnownWidth;
        float height = float.IsFinite(settings.WorldHeight) && settings.WorldHeight > 0f
            ? settings.WorldHeight
            : KnownHeight;
        return new CanonicalWorld(settings, new GridSpatialIndex(KnownCellSize, width, height));
    }
}
