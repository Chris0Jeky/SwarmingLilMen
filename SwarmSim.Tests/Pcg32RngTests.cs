using SwarmSim.Core.Utils;

namespace SwarmSim.Tests;

public sealed class Pcg32RngTests
{
    // The first six words are the published PCG C demo, initialized with (42, 54).
    // The remaining values/adapters were independently calculated with integer Python arithmetic.
    private static readonly uint[] Words =
    {
        0xA15C02B7u, 0x7B47F409u, 0xBA1D3330u, 0x83D2F293u,
        0xBFA4784Bu, 0xCBED606Eu, 0xBFC6A3ADu, 0x812FFF6Du,
        0xE61F305Au, 0xF9384B90u, 0x32DB86FEu, 0x1DC035F9u,
        0xED786826u, 0x3822441Du, 0x2BA113D7u, 0x1C5B818Bu
    };

    [Fact]
    public void IntegerSequenceMatchesThePublishedPcgReference()
    {
        var rng = new Pcg32Rng(42, 54);
        foreach (uint expected in Words)
            Assert.Equal(expected, rng.NextUInt32());
    }

    [Fact]
    public void FloatAdapterHasCommittedExactBits()
    {
        uint[] expected =
        {
            0x3F215C02u, 0x3EF68FE8u, 0x3F3A1D33u, 0x3F03D2F2u,
            0x3F3FA478u, 0x3F4BED60u, 0x3F3FC6A3u, 0x3F012FFFu,
            0x3F661F30u, 0x3F79384Bu, 0x3E4B6E18u, 0x3DEE01A8u,
            0x3F6D7868u, 0x3E608910u, 0x3E2E844Cu, 0x3DE2DC08u
        };
        var rng = new Pcg32Rng(42, 54);
        foreach (uint bits in expected)
            Assert.Equal(bits, BitConverter.SingleToUInt32Bits(rng.NextFloat()));
    }

    [Fact]
    public void DoubleAdapterHasCommittedExactBits()
    {
        ulong[] expected =
        {
            0x3FE42B8055ED1FD0ul, 0x3FE743A6660F4BCAul, 0x3FE7F48F0B2FB581ul, 0x3FE7F8D47604BFFDul,
            0x3FECC3E60BE4E12Eul, 0x3FC96DC371DC035Cul, 0x3FEDAF0D04E08910ul, 0x3FC5D089E1C5B818ul,
            0x3FE44672AE136997ul, 0x3FE9DACE52CB03F8ul, 0x3FE2302E2557FA24ul, 0x3FD1FA4822435794ul,
            0x3FE63D10570987A1ul, 0x3FBA9B90EF2C9D10ul, 0x3FD7CDB5E8F71028ul, 0x3FE0A33EB8A4F539ul
        };
        var rng = new Pcg32Rng(42, 54);
        foreach (ulong bits in expected)
            Assert.Equal(bits, BitConverter.DoubleToUInt64Bits(rng.NextDouble()));
    }

    [Fact]
    public void BoundedDrawUsesRejectionInsteadOfBiasedModulo()
    {
        uint[] expected =
        {
            559678134u, 974992175u, 64156306u, 1067743306u,
            1273847917u, 1069982636u, 19922796u, 1713320025u,
            2033732495u, 1836607525u, 573805929u, 81421794u,
            1322676881u, 851508741u, 293695791u, 837321402u
        };
        var rng = new Pcg32Rng(42, 54);
        foreach (uint value in expected)
            Assert.Equal(value, rng.NextUInt32(2_147_483_649u));
    }

    [Fact]
    public void ZeroBoundIsRejectedBeforeConsumingADraw()
    {
        var rng = new Pcg32Rng(42, 54);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextUInt32(0));
        Assert.Equal(Words[0], rng.NextUInt32());
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    [InlineData(2_147_483_649u)]
    [InlineData(uint.MaxValue)]
    public void BoundedDrawsStayInsideTheHalfOpenRange(uint bound)
    {
        var rng = new Pcg32Rng(ulong.MaxValue, ulong.MaxValue);
        for (int i = 0; i < 10_000; i++)
            Assert.True(rng.NextUInt32(bound) < bound);
    }

    [Fact]
    public void FloatingPointDrawsStayInsideTheHalfOpenUnitInterval()
    {
        var rng = new Pcg32Rng(0, 0);
        for (int i = 0; i < 10_000; i++)
        {
            float single = rng.NextFloat();
            double value = rng.NextDouble();
            Assert.True(single >= 0f && single < 1f);
            Assert.True(value >= 0d && value < 1d);
        }
    }

    [Fact]
    public void SequenceSelectorUsesTheSpecifiedLow63Bits()
    {
        var first = new Pcg32Rng(ulong.MaxValue, 54);
        var second = new Pcg32Rng(ulong.MaxValue, 54 | (1ul << 63));
        for (int i = 0; i < 128; i++)
            Assert.Equal(first.NextUInt32(), second.NextUInt32());
    }

    [Fact]
    public void RetainedGeneratorDrawsAllocateNoManagedMemory()
    {
        var rng = new RandomStreams(42).CreateStream("allocation", 0);
        for (int i = 0; i < 1000; i++)
        {
            rng.NextUInt32(2_147_483_649u);
            rng.NextFloat();
            rng.NextDouble();
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        double sum = 0d;
        for (int i = 0; i < 100_000; i++)
            sum += rng.NextUInt32(2_147_483_649u) + rng.NextFloat() + rng.NextDouble();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(sum > 0d);
        Assert.Equal(0L, allocated);
    }
}
