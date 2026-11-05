using SwarmSim.Core;

namespace SwarmSim.Tests;

public sealed class ScalarFieldOverflowTests
{
    [Fact]
    public void FiniteDepositsCannotSilentlyTurnACellIntoInfinity()
    {
        var field = new ScalarField(1, 1, 1f, 1f);
        field.Deposit(0f, 0f, float.MaxValue);

        Assert.Throws<OverflowException>(() => field.Deposit(0f, 0f, float.MaxValue));

        Assert.Equal(float.MaxValue, field.Sample(0f, 0f));
        Assert.Equal((double)float.MaxValue, field.Total());
        // The failed operation left a valid finite field that can still be cleared.
        field.Evaporate(1f);
        Assert.Equal(0f, field.Sample(0f, 0f));
    }

    [Fact]
    public void RejectedDepositPreservesEveryCellBit()
    {
        var field = new ScalarField(2, 1, 2f, 1f);
        field.Deposit(0f, 0f, float.MaxValue);
        field.Deposit(1f, 0f, 0.1f);
        uint[] before = field.Cells.ToArray().Select(BitConverter.SingleToUInt32Bits).ToArray();

        Assert.Throws<OverflowException>(() => field.Deposit(-2f, 0f, float.MaxValue));

        Assert.Equal(before, field.Cells.ToArray().Select(BitConverter.SingleToUInt32Bits));
    }

    [Theory]
    [InlineData(0.1f, 0.2f)]
    [InlineData(1e30f, 2e30f)]
    [InlineData(float.MaxValue, 0f)]
    [InlineData(float.Epsilon, float.Epsilon)]
    public void RepresentableDepositsKeepTheOriginalFloatAddition(float first, float second)
    {
        var field = new ScalarField(1, 1, 1f, 1f);
        field.Deposit(0f, 0f, first);
        field.Deposit(0f, 0f, second);

        Assert.Equal(BitConverter.SingleToUInt32Bits(first + second), BitConverter.SingleToUInt32Bits(field.Sample(0f, 0f)));
    }
}
