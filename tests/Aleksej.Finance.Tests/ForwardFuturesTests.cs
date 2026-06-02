using Aleksej.Finance.Derivatives;

namespace Aleksej.Finance.Tests;

public class ForwardFuturesTests
{
    [Fact]
    public void ForwardPrice_MatchesCostOfCarry()
    {
        // F = S * exp(r*T) ; S=100, r=5%, T=1 -> 100 * e^0.05
        double f = ForwardFutures.ForwardPrice(100, 0.05, 1);
        Assert.Equal(100 * Math.Exp(0.05), f, 9);
        Assert.Equal(105.1271, f, 4);
    }

    [Fact]
    public void ForwardPriceWithYield_DiscountsByDividendYield()
    {
        double f = ForwardFutures.ForwardPriceWithYield(100, 0.05, 0.02, 1);
        Assert.Equal(100 * Math.Exp((0.05 - 0.02) * 1), f, 9);
    }

    [Fact]
    public void ForwardPriceWithYield_ReducesToPlainForwardWhenYieldZero()
    {
        double withYield = ForwardFutures.ForwardPriceWithYield(100, 0.05, 0.0, 1);
        double plain = ForwardFutures.ForwardPrice(100, 0.05, 1);
        Assert.Equal(plain, withYield, 9);
    }

    [Fact]
    public void ForwardPriceWithIncome_SubtractsIncomePvBeforeCompounding()
    {
        double f = ForwardFutures.ForwardPriceWithIncome(100, 5, 0.05, 1);
        Assert.Equal((100 - 5) * Math.Exp(0.05 * 1), f, 9);
    }

    [Fact]
    public void FxForwardPrice_MatchesCoveredInterestParity()
    {
        // F = S * exp((r - rf) * T)
        double f = ForwardFutures.FxForwardPrice(1.20, 0.04, 0.01, 0.5);
        Assert.Equal(1.20 * Math.Exp((0.04 - 0.01) * 0.5), f, 9);
    }

    [Fact]
    public void ForwardPriceCommodity_IncludesStorageAndConvenienceYield()
    {
        double f = ForwardFutures.ForwardPriceCommodity(50, 0.03, 0.02, 0.01, 1);
        Assert.Equal(50 * Math.Exp((0.03 + 0.02 - 0.01) * 1), f, 9);
    }

    [Fact]
    public void ForwardValue_IsZeroAtInceptionWhenStrikeEqualsForward()
    {
        double f = ForwardFutures.ForwardPrice(100, 0.05, 1);
        double value = ForwardFutures.ForwardValue(f, f, 0.05, 1);
        Assert.Equal(0.0, value, 9);
    }

    [Fact]
    public void ForwardValue_MatchesClosedForm()
    {
        double value = ForwardFutures.ForwardValue(110, 100, 0.05, 1);
        Assert.Equal((110 - 100) * Math.Exp(-0.05 * 1), value, 9);
    }

    [Fact]
    public void ForwardValueShort_IsNegationOfLong()
    {
        double longVal = ForwardFutures.ForwardValue(110, 100, 0.05, 1);
        double shortVal = ForwardFutures.ForwardValueShort(110, 100, 0.05, 1);
        Assert.Equal(-longVal, shortVal, 9);
    }

    [Fact]
    public void PresentValueOfIncome_DiscountsEachCashFlow()
    {
        double[] cf = { 2, 2, 2 };
        double[] times = { 0.25, 0.5, 0.75 };
        double expected = 2 * Math.Exp(-0.05 * 0.25)
                        + 2 * Math.Exp(-0.05 * 0.5)
                        + 2 * Math.Exp(-0.05 * 0.75);
        double pv = ForwardFutures.PresentValueOfIncome(cf, times, 0.05);
        Assert.Equal(expected, pv, 9);
    }

    [Fact]
    public void PresentValueOfIncome_ThrowsOnLengthMismatch()
    {
        double[] cf = { 1, 2 };
        double[] times = { 0.5 };
        Assert.Throws<ArgumentException>(() => ForwardFutures.PresentValueOfIncome(cf, times, 0.05));
    }
}
