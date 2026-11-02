using SwarmSim.Core.Canonical;

namespace SwarmSim.Tests;

public sealed class CanonicalVectorClampTests
{
    public static IEnumerable<object[]> FiniteCases()
    {
        foreach ((int sourceExponent, int capExponent) in new[]
        {
            (0, -1), (80, 79), (80, 0), (80, -90), (80, 80), (80, 81)
        })
            foreach (int signX in new[] { -1, 1 })
                foreach (int signY in new[] { -1, 1 })
                    yield return [sourceExponent, capExponent, signX, signY];
    }

    [Theory]
    [MemberData(nameof(FiniteCases))]
    public void FiniteVectorPreservesDirectionAndRespectsMagnitudeCap(
        int sourceExponent, int capExponent, int signX, int signY)
    {
        float sourceScale = MathF.ScaleB(1f, sourceExponent);
        float capScale = MathF.ScaleB(1f, capExponent);
        var source = new Vec2(signX * 3f * sourceScale, signY * 4f * sourceScale);

        Vec2 actual = source.ClampMagnitude(5f * capScale);

        // A 3-4-5 direction with a power-of-two scale gives exact component oracles,
        // without computing the source norm or repeating the clamping algorithm.
        float expectedScale = MathF.ScaleB(1f, Math.Min(sourceExponent, capExponent));
        Assert.Equal(signX * 3f * expectedScale, actual.X);
        Assert.Equal(signY * 4f * expectedScale, actual.Y);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OverflowedAxisNormCanClampToTheSmallestPositiveFloat(bool vertical)
    {
        var source = vertical
            ? new Vec2(-0f, -float.MaxValue)
            : new Vec2(float.MaxValue, -0f);

        Vec2 actual = source.ClampMagnitude(float.Epsilon);

        EqualBits(vertical ? -0f : float.Epsilon, actual.X);
        EqualBits(vertical ? -float.Epsilon : -0f, actual.Y);
    }

    [Fact]
    public void OrdinaryClampingKeepsItsEstablishedFloatRounding()
    {
        Vec2 actual = new Vec2(1f, 3f).ClampMagnitude(1f);

        Assert.Equal(0x3EA1E89B, BitConverter.SingleToInt32Bits(actual.X));
        // Computing the norm and scale in double throughout changes this to 0x3F72DCE9.
        Assert.Equal(0x3F72DCE8, BitConverter.SingleToInt32Bits(actual.Y));
    }

    public static IEnumerable<object[]> CompatibilityCases()
    {
        yield return [float.PositiveInfinity, 1f, 1f, float.NaN, 0f];
        yield return [float.NaN, 1f, 1f, float.NaN, float.NaN];
        yield return [3f, 4f, float.NaN, float.NaN, float.NaN];
        yield return [float.MaxValue, 0f, float.NaN, float.NaN, float.NaN];
        yield return [3f, 4f, float.PositiveInfinity, 3f, 4f];
        yield return [float.PositiveInfinity, 1f, float.PositiveInfinity, float.PositiveInfinity, 1f];
        yield return [3f, 4f, 0f, 0f, 0f];
        yield return [3f, 4f, -1f, 0f, 0f];
        yield return [3f, 4f, float.NegativeInfinity, 0f, 0f];
    }

    [Theory]
    [MemberData(nameof(CompatibilityCases))]
    public void NonfiniteInputsAndNonpositiveCapsKeepTheirExistingBehavior(
        float x, float y, float cap, float expectedX, float expectedY)
    {
        Vec2 actual = new Vec2(x, y).ClampMagnitude(cap);

        EqualBits(expectedX, actual.X);
        EqualBits(expectedY, actual.Y);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SteeringResolversContinueRejectingAnOverflowedWeightedSquaredMagnitude(bool independent)
    {
        ISteeringResolver resolver = independent
            ? new IndependentClampSumResolver() : new PriorityForceBudgetResolver();
        var writer = new IntentWriter(new PolicyMetadata("large-force"), new SteeringIntent[1]);
        writer.AddForce(new Vec2(MathF.ScaleB(3f, 80), MathF.ScaleB(4f, 80)));
        SteeringIntent intent = writer.Written[0];
        Vec2 total = new(1f, 2f);
        float remaining = 5f;

        var error = Assert.Throws<ArgumentException>(() =>
            resolver.Accumulate(intent, 0f, 5f, ref total, ref remaining));

        Assert.Equal("intent", error.ParamName);
        Assert.Equal(1f, total.X);
        Assert.Equal(2f, total.Y);
        Assert.Equal(5f, remaining);
    }

    private static void EqualBits(float expected, float actual)
    {
        if (float.IsNaN(expected)) Assert.True(float.IsNaN(actual));
        else Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(actual));
    }
}
