using AleksejCupic.FinancialMath.Derivatives;

namespace AleksejCupic.FinancialMath.Tests;

public class BlackModelTests
{
    [Fact]
    public void CapletPrice_IsPositive()
    {
        double price = BlackModel.CapletPrice(100, 0.04, 0.04, 1.0, 0.03, 0.20, accrualFraction: 0.5);
        Assert.True(price > 0);
    }

    [Fact]
    public void CapletPrice_IncreasesWithVolatility()
    {
        double low = BlackModel.CapletPrice(100, 0.04, 0.04, 1.0, 0.03, 0.10, accrualFraction: 0.5);
        double high = BlackModel.CapletPrice(100, 0.04, 0.04, 1.0, 0.03, 0.30, accrualFraction: 0.5);
        Assert.True(high > low);
    }

    [Fact]
    public void CapletPrice_AtExpiryReturnsDiscountedIntrinsic()
    {
        // t <= 0 -> notional * accrual * max(F - K, 0) for caplet
        double price = BlackModel.CapletPrice(100, 0.05, 0.04, 0.0, 0.03, 0.20, accrualFraction: 0.5);
        Assert.Equal(100 * 0.5 * (0.05 - 0.04), price, 12);
    }

    [Fact]
    public void FloorletPrice_AtExpiryReturnsIntrinsic()
    {
        double price = BlackModel.CapletPrice(100, 0.03, 0.04, 0.0, 0.03, 0.20, accrualFraction: 0.5, isFloor: true);
        Assert.Equal(100 * 0.5 * (0.04 - 0.03), price, 12);
    }

    [Fact]
    public void CapPrice_EqualsSumOfCaplets()
    {
        double[] times = { 0.5, 1.0, 1.5 };
        double[] zeros = { 0.03, 0.032, 0.034 };
        double[] fwds = { 0.035, 0.036, 0.037 };
        double[] accr = { 0.5, 0.5, 0.5 };

        double cap = BlackModel.CapPrice(100, 0.04, 0.20, times, zeros, fwds, accr);

        double sum = 0;
        for (int i = 0; i < times.Length; i++)
            sum += BlackModel.CapletPrice(100, fwds[i], 0.04, times[i], zeros[i], 0.20, accr[i], isFloor: false);

        Assert.Equal(sum, cap, 12);
    }

    [Fact]
    public void FloorPrice_EqualsSumOfFloorlets()
    {
        double[] times = { 0.5, 1.0, 1.5 };
        double[] zeros = { 0.03, 0.032, 0.034 };
        double[] fwds = { 0.035, 0.036, 0.037 };
        double[] accr = { 0.5, 0.5, 0.5 };

        double floor = BlackModel.FloorPrice(100, 0.04, 0.20, times, zeros, fwds, accr);

        double sum = 0;
        for (int i = 0; i < times.Length; i++)
            sum += BlackModel.CapletPrice(100, fwds[i], 0.04, times[i], zeros[i], 0.20, accr[i], isFloor: true);

        Assert.Equal(sum, floor, 12);
    }

    [Fact]
    public void CapFloorParity_HoldsViaCapletFloorletDifference()
    {
        // For each period: caplet - floorlet = N*delta*disc*(F - K) (forward-style parity).
        double n = 100, f = 0.045, k = 0.04, t = 1.0, r = 0.03, sigma = 0.20, delta = 0.5;
        double caplet = BlackModel.CapletPrice(n, f, k, t, r, sigma, delta, isFloor: false);
        double floorlet = BlackModel.CapletPrice(n, f, k, t, r, sigma, delta, isFloor: true);
        double expected = n * delta * Math.Exp(-r * t) * (f - k);
        Assert.Equal(expected, caplet - floorlet, 9);
    }

    [Fact]
    public void CapPrice_ThrowsOnLengthMismatch()
    {
        double[] times = { 0.5, 1.0 };
        double[] zeros = { 0.03 };
        double[] fwds = { 0.035, 0.036 };
        double[] accr = { 0.5, 0.5 };
        Assert.Throws<ArgumentException>(() => BlackModel.CapPrice(100, 0.04, 0.20, times, zeros, fwds, accr));
    }

    [Fact]
    public void ForwardSwapRate_MatchesClosedForm()
    {
        double[] times = { 1.0, 1.5, 2.0 };
        double[] zeros = { 0.03, 0.032, 0.034 };
        double[] accr = { 0.5, 0.5, 0.5 };

        double r = BlackModel.ForwardSwapRate(times, zeros, accr);

        double p0 = Math.Exp(-zeros[0] * times[0]);
        double pn = Math.Exp(-zeros[^1] * times[^1]);
        double annuity = 0;
        for (int i = 0; i < times.Length; i++)
            annuity += accr[i] * Math.Exp(-zeros[i] * times[i]);
        Assert.Equal((p0 - pn) / annuity, r, 12);
        Assert.True(r > 0);
    }

    [Fact]
    public void ForwardSwapRate_ThrowsWithFewerThanTwoTimes()
    {
        double[] times = { 1.0 };
        double[] zeros = { 0.03 };
        double[] accr = { 0.5 };
        Assert.Throws<ArgumentException>(() => BlackModel.ForwardSwapRate(times, zeros, accr));
    }

    [Fact]
    public void SwaptionPrice_PayerIsPositiveAndIncreasesWithVol()
    {
        double[] times = { 1.0, 1.5, 2.0 };
        double[] zeros = { 0.03, 0.032, 0.034 };
        double[] accr = { 0.5, 0.5, 0.5 };

        double low = BlackModel.SwaptionPrice(100, 0.035, 1.0, 0.10, times, zeros, accr, isPayer: true);
        double high = BlackModel.SwaptionPrice(100, 0.035, 1.0, 0.30, times, zeros, accr, isPayer: true);
        Assert.True(low > 0);
        Assert.True(high > low);
    }

    [Fact]
    public void SwaptionPrice_AtTheMoneyPayerEqualsReceiver()
    {
        double[] times = { 1.0, 1.5, 2.0 };
        double[] zeros = { 0.03, 0.032, 0.034 };
        double[] accr = { 0.5, 0.5, 0.5 };
        double atm = BlackModel.ForwardSwapRate(times, zeros, accr);

        double payer = BlackModel.SwaptionPrice(100, atm, 1.0, 0.20, times, zeros, accr, isPayer: true);
        double receiver = BlackModel.SwaptionPrice(100, atm, 1.0, 0.20, times, zeros, accr, isPayer: false);
        Assert.Equal(payer, receiver, 9);
    }
}
