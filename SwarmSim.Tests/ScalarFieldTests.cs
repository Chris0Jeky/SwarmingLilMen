using SwarmSim.Core;

namespace SwarmSim.Tests;

public sealed class ScalarFieldTests
{
    [Fact]
    public void EvaporationAfterKUpdatesIsAGeometricDecay()
    {
        var field = new ScalarField(4, 4, 8f, 8f);
        field.Deposit(1f, 1f, 16f);

        for (int k = 0; k < 4; k++) field.Evaporate(0.5f);

        // Powers of two are exact in binary32: 16 * 0.5^4.
        Assert.Equal(1f, field.Sample(1f, 1f));
    }

    [Fact]
    public void EvaporationMatchesTheClosedFormAtAnInexactRate()
    {
        var field = new ScalarField(1, 1, 1f, 1f);
        field.Deposit(0f, 0f, 100f);

        for (int k = 0; k < 50; k++) field.Evaporate(0.1f);

        double expected = 100d * Math.Pow(1d - 0.1f, 50);
        Assert.Equal(expected, field.Sample(0f, 0f), 3);
    }

    [Fact]
    public void RateOneClearsAndRateZeroKeepsEveryBit()
    {
        var field = new ScalarField(2, 1, 2f, 1f);
        field.Deposit(0.5f, 0f, 0.1f);
        field.Deposit(1.5f, 0f, 3f);

        field.Evaporate(0f);
        Assert.Equal(BitConverter.SingleToUInt32Bits(0.1f), BitConverter.SingleToUInt32Bits(field.Sample(0.5f, 0f)));

        field.Evaporate(1f);
        Assert.Equal(0d, field.Total());
    }

    [Fact]
    public void ValuesBelowTheFlushThresholdBecomeExactlyZero()
    {
        var field = new ScalarField(1, 1, 1f, 1f);
        field.Deposit(0f, 0f, 1f);

        // Without the flush this stalls at the smallest subnormal instead of reaching zero.
        for (int k = 0; k < 2000; k++) field.Evaporate(0.1f);

        Assert.Equal(0f, field.Sample(0f, 0f));
    }

    [Fact]
    public void DepositsAreConservedAcrossCells()
    {
        var field = new ScalarField(10, 10, 100f, 100f);
        double expected = 0d;
        for (int i = 0; i < 1000; i++)
        {
            field.Deposit(i * 7.3f, i * 3.1f, 2f);
            expected += 2d;
        }

        Assert.Equal(expected, field.Total());
    }

    [Fact]
    public void ADepositLandsInExactlyOneCell()
    {
        var field = new ScalarField(4, 2, 40f, 20f);

        field.Deposit(25f, 15f, 3f);

        // Column 2, row 1 of a four-column row-major grid.
        float[] expected = [0f, 0f, 0f, 0f, 0f, 0f, 3f, 0f];
        Assert.Equal(expected, field.Cells.ToArray());
    }

    [Theory]
    [InlineData(-1f, 5f, 9.5f, 5f)]   // West of the seam is the last column.
    [InlineData(10f, 5f, 0.5f, 5f)]   // The extent itself is column zero.
    [InlineData(5f, -0.5f, 5f, 9.5f)] // North of the seam is the last row.
    [InlineData(5f, 10f, 5f, 0.5f)]
    [InlineData(21f, -21f, 1f, 9f)]   // Several wraps, and both axes together.
    public void SamplesWrapAcrossTheSeam(float x, float y, float sameX, float sameY)
    {
        var field = new ScalarField(10, 10, 10f, 10f);

        field.Deposit(sameX, sameY, 1f);

        Assert.Equal(1f, field.Sample(x, y));
        Assert.Equal(1d, field.Total());
    }

    [Fact]
    public void TheLargestCoordinateBelowAnExtentStaysInTheLastCell()
    {
        var field = new ScalarField(3, 3, 0.3f, 0.3f);
        float justBelow = MathF.BitDecrement(0.3f);

        field.Deposit(justBelow, justBelow, 1f);

        Assert.Equal(1f, field.Cells[8]);
    }

    [Fact]
    public void BoundsAreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScalarField(0, 1, 1f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScalarField(1, 0, 1f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScalarField(1, 1, 0f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScalarField(1, 1, 1f, float.NaN));

        var field = new ScalarField(1, 1, 1f, 1f);
        Assert.Throws<ArgumentOutOfRangeException>(() => field.Deposit(0f, 0f, -1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => field.Deposit(0f, 0f, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => field.Evaporate(-0.1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => field.Evaporate(1.1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => field.Evaporate(float.NaN));
        Assert.Throws<ArgumentException>(() => field.Sample(float.NaN, 0f));
        Assert.Throws<ArgumentException>(() => field.Deposit(float.PositiveInfinity, 0f, 1f));
    }

    [Fact]
    public void OperationsAllocateNothingAfterConstruction()
    {
        var field = new ScalarField(32, 32, 100f, 100f);
        void Work()
        {
            for (int i = 0; i < 1000; i++)
            {
                field.Deposit(i, i, 1f);
                _ = field.Sample(i, i);
                field.Evaporate(0.01f);
            }
            _ = field.Total();
        }
        Work();

        long before = GC.GetAllocatedBytesForCurrentThread();
        Work();

        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
