using SwarmSim.Core.Utils;

namespace SwarmSim.Tests;

public class MathUtilsImageDeltaTests
{
    [Fact]
    public void MinimumImageDelta_SeamWrapsToNegativeHalfExtent()
    {
        Assert.Equal(-2.5f, MathUtils.MinimumImageDelta(7.5f, 10f), 1e-4f);
        Assert.Equal(5f, MathUtils.MinimumImageDelta(5f, 10f), 1e-4f);
        Assert.Equal(-5f, MathUtils.MinimumImageDelta(-5f, 10f), 1e-4f);
    }

    [Theory]
    [InlineData(-145)]
    [InlineData(-126)]
    [InlineData(-20)]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(100)]
    [InlineData(120)]
    public void MinimumImageDelta_PreservesExactImagesAcrossBinaryScales(int exponent)
    {
        // Integer positions on an eight-unit circle have analytical signed images.
        // Power-of-two scaling keeps every input/output exact, including subnormal floats.
        float[] deltas = [-20, -16, -9, -8, -7, -5, -4, -3, -1, -0f, 0, 1, 3, 4, 5, 7, 8, 9, 16, 20];
        float[] images = [-4, -0f, -1, -0f, 1, 3, -4, -3, -1, -0f, 0, 1, 3, 4, -3, -1, 0, 1, 0, 4];
        float extent = MathF.ScaleB(8f, exponent);
        for (int i = 0; i < deltas.Length; i++)
        {
            float actual = MathUtils.MinimumImageDelta(MathF.ScaleB(deltas[i], exponent), extent);
            Assert.Equal(BitConverter.SingleToInt32Bits(MathF.ScaleB(images[i], exponent)),
                BitConverter.SingleToInt32Bits(actual));
        }
    }

    [Theory]
    [InlineData(0x40800001, unchecked((int)0xC07FFFFE))] // just above +half
    [InlineData(unchecked((int)0xC0800001), 0x407FFFFE)] // just below -half
    [InlineData(0x40FFFFFF, unchecked((int)0xB5000000))] // just below +extent
    [InlineData(0x41000001, 0x35800000)]                 // just above +extent
    [InlineData(unchecked((int)0xC0FFFFFF), 0x35000000)] // just above -extent
    [InlineData(unchecked((int)0xC1000001), unchecked((int)0xB5800000))]
    public void MinimumImageDelta_RetainsBoundaryRoundingBits(int deltaBits, int imageBits)
    {
        float actual = MathUtils.MinimumImageDelta(BitConverter.Int32BitsToSingle(deltaBits), 8f);
        Assert.Equal(imageBits, BitConverter.SingleToInt32Bits(actual));
    }

    [Fact]
    public void MinimumImageDelta_RetainsUnboundedAndNonfiniteBehavior()
    {
        foreach (float delta in new[] { -float.MaxValue, -1f, -0f, 0f, 1f, float.MaxValue })
            Assert.Equal(BitConverter.SingleToInt32Bits(delta),
                BitConverter.SingleToInt32Bits(MathUtils.MinimumImageDelta(delta, float.PositiveInfinity)));
        foreach (float delta in new[] { float.NegativeInfinity, float.PositiveInfinity, float.NaN })
            Assert.True(float.IsNaN(MathUtils.MinimumImageDelta(delta, 8f)));
        Assert.True(float.IsNaN(MathUtils.MinimumImageDelta(1f, float.NaN)));
        Assert.True(float.IsNaN(MathUtils.MinimumImageDelta(1f, 0f)));
    }
}
