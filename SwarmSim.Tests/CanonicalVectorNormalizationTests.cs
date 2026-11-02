using SwarmSim.Core.Canonical;

namespace SwarmSim.Tests;

public sealed class CanonicalVectorNormalizationTests
{
    public static IEnumerable<object[]> DirectionCases()
    {
        foreach (int exponent in new[] { 0, 80 })
            foreach (int signX in new[] { -1, 1 })
                foreach (int signY in new[] { -1, 1 })
                    foreach (bool withLength in new[] { false, true })
                        yield return [exponent, signX, signY, withLength];
    }

    [Theory]
    [MemberData(nameof(DirectionCases))]
    public void FiniteDirectionSurvivesSquaredOverflow(int exponent, int signX, int signY, bool withLength)
    {
        float scale = MathF.ScaleB(1f, exponent);
        var source = new Vec2(signX * 3f * scale, signY * 4f * scale);

        Vec2 actual = withLength ? source.WithLength(5f) : source.Normalized;

        Assert.Equal(signX * (withLength ? 3f : 0.6f), actual.X);
        Assert.Equal(signY * (withLength ? 4f : 0.8f), actual.Y);
    }

    [Theory]
    [InlineData(-90, 1)]
    [InlineData(80, 1)]
    [InlineData(0, -1)]
    public void WithLengthRetainsTheFiniteRequestedScaleAndSign(int targetExponent, int sign)
    {
        var source = new Vec2(MathF.ScaleB(3f, 80), MathF.ScaleB(4f, 80));
        float targetScale = MathF.ScaleB(1f, targetExponent);

        Vec2 actual = source.WithLength(sign * 5f * targetScale);

        Assert.Equal(sign * 3f * targetScale, actual.X);
        Assert.Equal(sign * 4f * targetScale, actual.Y);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LargestFiniteAxisSupportsNormalizationAndTheSmallestOutput(bool vertical)
    {
        var source = vertical ? new Vec2(-0f, -float.MaxValue) : new Vec2(float.MaxValue, -0f);

        Vec2 normalized = source.Normalized;
        Vec2 tiny = source.WithLength(float.Epsilon);

        EqualBits(vertical ? -0f : 1f, normalized.X);
        EqualBits(vertical ? -1f : -0f, normalized.Y);
        EqualBits(vertical ? -0f : float.Epsilon, tiny.X);
        EqualBits(vertical ? -float.Epsilon : -0f, tiny.Y);
    }

    [Fact]
    public void ALengthBeyondFloatRangeStillHasARepresentableUnitDirection()
    {
        var source = new Vec2(float.MaxValue, -float.MaxValue);
        const float inverseSqrtTwo = 0.7071067811865475244f;

        foreach (Vec2 actual in new[] { source.Normalized, source.WithLength(1f) })
        {
            Assert.Equal(inverseSqrtTwo, actual.X);
            Assert.Equal(-inverseSqrtTwo, actual.Y);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryNormalizationKeepsItsFloatRounding(bool withLength)
    {
        var source = new Vec2(1f, 3f);
        Vec2 actual = withLength ? source.WithLength(1f) : source.Normalized;

        Assert.Equal(0x3EA1E89B, BitConverter.SingleToInt32Bits(actual.X));
        Assert.Equal(0x3F72DCE8, BitConverter.SingleToInt32Bits(actual.Y));
    }

    public static IEnumerable<object[]> CutoffCases()
    {
        foreach ((float x, int direction) in new[]
        {
            (0f, 0), (-0f, 0), (float.Epsilon, 0), (MathF.BitDecrement(1e-6f), 0),
            (1e-6f, 0), (-1e-6f, 0), (MathF.BitIncrement(1e-6f), 1), (-MathF.BitIncrement(1e-6f), -1)
        })
            foreach (bool withLength in new[] { false, true })
                yield return [x, direction, withLength];
    }

    [Theory]
    [MemberData(nameof(CutoffCases))]
    public void ExistingNearlyZeroBoundariesArePreserved(float x, int direction, bool withLength)
    {
        var source = new Vec2(x, 0f);
        Vec2 actual = withLength ? source.WithLength(5f) : source.Normalized;

        EqualBits(direction * (withLength ? 5f : 1f), actual.X);
        EqualBits(0f, actual.Y);
    }

    public static IEnumerable<object[]> NonfiniteCases()
    {
        foreach (float[] values in new[]
        {
            new[] { float.PositiveInfinity, 1f, float.NaN, 0f },
            new[] { 1f, float.PositiveInfinity, 0f, float.NaN },
            new[] { float.NaN, 1f, float.NaN, float.NaN },
            new[] { float.PositiveInfinity, float.NegativeInfinity, float.NaN, float.NaN }
        })
            foreach (bool withLength in new[] { false, true })
                yield return [values[0], values[1], values[2], values[3], withLength];
    }

    [Theory]
    [MemberData(nameof(NonfiniteCases))]
    public void NonfiniteSourceBehaviorIsUnchanged(float x, float y, float expectedX, float expectedY, bool withLength)
    {
        var source = new Vec2(x, y);
        Vec2 actual = withLength ? source.WithLength(5f) : source.Normalized;

        EqualBits(expectedX, actual.X);
        EqualBits(expectedY, actual.Y);
    }

    [Theory]
    [InlineData(float.MaxValue, 0f, float.PositiveInfinity, float.NaN, float.NaN)]
    [InlineData(float.MaxValue, 0f, float.NegativeInfinity, float.NaN, float.NaN)]
    [InlineData(float.MaxValue, 0f, float.NaN, float.NaN, float.NaN)]
    [InlineData(1f, 3f, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity)]
    [InlineData(1f, 3f, float.NaN, float.NaN, float.NaN)]
    [InlineData(0f, 0f, float.PositiveInfinity, 0f, 0f)]
    [InlineData(0f, 0f, float.NaN, 0f, 0f)]
    public void NonfiniteTargetLengthBehaviorIsUnchanged(float x, float y, float target, float expectedX, float expectedY)
    {
        Vec2 actual = new Vec2(x, y).WithLength(target);

        EqualBits(expectedX, actual.X);
        EqualBits(expectedY, actual.Y);
    }

    [Theory]
    [InlineData(0, false, 1)]
    [InlineData(0, true, 1)]
    [InlineData(0, false, -1)]
    [InlineData(0, true, -1)]
    [InlineData(80, false, 1)]
    [InlineData(80, true, 1)]
    [InlineData(80, false, -1)]
    [InlineData(80, true, -1)]
    public void WorldIngestionRetainsLargeFiniteInputDirections(int exponent, bool setVelocity, int sign)
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = 1, WorldWidth = 64f, WorldHeight = 64f, TargetSpeed = 5f
        };
        var world = new CanonicalWorld(settings, new NaiveSpatialIndex(64f, 64f), CanonicalPipeline.Empty());
        var source = new Vec2(sign * MathF.ScaleB(3f, exponent), sign * MathF.ScaleB(4f, exponent));
        Assert.True(world.TryAddBoid(new Vec2(16f, 16f), setVelocity ? new Vec2(1f, 0f) : source));
        if (setVelocity) world.SetVelocity(0, source);

        Assert.Equal(sign * 3f, world.Boids[0].Velocity.X);
        Assert.Equal(sign * 4f, world.Boids[0].Velocity.Y);
        world.Step(1f);
        Assert.Equal(16f + sign * 3f, world.Boids[0].Position.X);
        Assert.Equal(16f + sign * 4f, world.Boids[0].Position.Y);
    }

    private static void EqualBits(float expected, float actual)
    {
        if (float.IsNaN(expected)) Assert.True(float.IsNaN(actual));
        else Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(actual));
    }
}
