using AleksejCupic.FinancialMath.Derivatives;

namespace AleksejCupic.FinancialMath.Tests;

public class InterestRateSwapTests
{
    [Fact]
    public void FloatingLegPV_MatchesNextResetApproximation()
    {
        double pv = InterestRateSwap.FloatingLegPV(100, 0.026, 0.25, 0.03);
        Assert.Equal(100 * (1 + 0.026) * Math.Exp(-0.03 * 0.25), pv, 9);
    }

    [Fact]
    public void FloatingLegPV_AtResetIsUndiscountedParPlusCoupon()
    {
        double pv = InterestRateSwap.FloatingLegPV(100, 0.026, 0.0, 0.03);
        Assert.Equal(100 * (1 + 0.026), pv, 9);
    }

    [Fact]
    public void FixedLegPV_CouponOnlyMatchesClosedForm()
    {
        double[] times = { 0.5, 1.0 };
        double[] rates = { 0.03, 0.032 };
        double coupon = 100 * 0.05 / 2;
        double expected = coupon * Math.Exp(-0.03 * 0.5) + coupon * Math.Exp(-0.032 * 1.0);
        double pv = InterestRateSwap.FixedLegPV(100, 0.05, times, rates, frequency: 2, includePrincipal: false);
        Assert.Equal(expected, pv, 9);
    }

    [Fact]
    public void FixedLegPV_WithPrincipalAddsDiscountedNotional()
    {
        double[] times = { 0.5, 1.0 };
        double[] rates = { 0.03, 0.032 };
        double couponOnly = InterestRateSwap.FixedLegPV(100, 0.05, times, rates, frequency: 2, includePrincipal: false);
        double withPrincipal = InterestRateSwap.FixedLegPV(100, 0.05, times, rates, frequency: 2, includePrincipal: true);
        Assert.Equal(couponOnly + 100 * Math.Exp(-0.032 * 1.0), withPrincipal, 9);
    }

    [Fact]
    public void FixedLegPV_ThrowsOnLengthMismatch()
    {
        double[] times = { 0.5, 1.0 };
        double[] rates = { 0.03 };
        Assert.Throws<ArgumentException>(() => InterestRateSwap.FixedLegPV(100, 0.05, times, rates));
    }

    [Fact]
    public void ParSwapRate_MakesParAnnuityIdentityHold()
    {
        double[] times = { 0.5, 1.0, 1.5, 2.0 };
        double[] rates = { 0.03, 0.032, 0.034, 0.035 };
        double k = InterestRateSwap.ParSwapRate(times, rates, frequency: 2);

        // Reconstruct the identity K = (1 - P(Tn)) / (delta * sum P(Ti)).
        double delta = 1.0 / 2;
        double annuity = 0;
        for (int i = 0; i < times.Length; i++)
            annuity += delta * Math.Exp(-rates[i] * times[i]);
        double pTn = Math.Exp(-rates[^1] * times[^1]);
        Assert.Equal((1 - pTn) / annuity, k, 9);
        Assert.True(k > 0);
    }

    [Fact]
    public void ParSwapRate_ThrowsOnLengthMismatch()
    {
        double[] times = { 0.5, 1.0 };
        double[] rates = { 0.03 };
        Assert.Throws<ArgumentException>(() => InterestRateSwap.ParSwapRate(times, rates));
    }

    [Fact]
    public void ParSwapRate_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() => InterestRateSwap.ParSwapRate(Array.Empty<double>(), Array.Empty<double>()));
    }

    [Fact]
    public void SwapValue_PayerIsNegationOfReceiver()
    {
        double[] times = { 0.5, 1.0, 1.5, 2.0 };
        double[] rates = { 0.03, 0.032, 0.034, 0.035 };
        double payer = InterestRateSwap.SwapValue(100, 0.05, times, rates, 0.026, 0.25, 0.029, isPayFixed: true);
        double receiver = InterestRateSwap.SwapValue(100, 0.05, times, rates, 0.026, 0.25, 0.029, isPayFixed: false);
        Assert.Equal(-payer, receiver, 9);
    }

    [Fact]
    public void SwapValue_EqualsDifferenceOfLegs()
    {
        double[] times = { 0.5, 1.0, 1.5, 2.0 };
        double[] rates = { 0.03, 0.032, 0.034, 0.035 };
        double bFixed = InterestRateSwap.FixedLegPV(100, 0.05, times, rates, includePrincipal: true);
        double bFloat = InterestRateSwap.FloatingLegPV(100, 0.026, 0.25, 0.029);
        double payer = InterestRateSwap.SwapValue(100, 0.05, times, rates, 0.026, 0.25, 0.029, isPayFixed: true);
        Assert.Equal(bFloat - bFixed, payer, 9);
    }

    [Fact]
    public void DV01_PayerAndReceiverAreOppositeAndNonZero()
    {
        double[] times = { 0.5, 1.0, 1.5, 2.0 };
        double[] rates = { 0.03, 0.032, 0.034, 0.035 };
        double payerDv01 = InterestRateSwap.DV01(100, 0.05, times, rates, 0.026, 0.25, 0.029, isPayFixed: true);
        double receiverDv01 = InterestRateSwap.DV01(100, 0.05, times, rates, 0.026, 0.25, 0.029, isPayFixed: false);
        // DV01 sign convention varies by desk, so assert the convention-agnostic invariants:
        // payer and receiver are exact opposites, and the sensitivity is non-zero.
        Assert.True(payerDv01 * receiverDv01 < 0); // opposite signs
        Assert.True(Math.Abs(payerDv01) > 0);
        Assert.Equal(-payerDv01, receiverDv01, 12);
    }
}
