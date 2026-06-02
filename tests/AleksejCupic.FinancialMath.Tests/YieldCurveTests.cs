using AleksejCupic.FinancialMath.Bonds;

namespace AleksejCupic.FinancialMath.Tests;

public class YieldCurveTests
{
    // ── Discount factors ──────────────────────────────────────────────────────

    [Fact]
    public void DiscountFactor_PositiveRate_InZeroOneInterval()
    {
        double df = YieldCurve.DiscountFactor(zeroRate: 0.05, t: 3);
        Assert.True(df > 0 && df <= 1);
        Assert.Equal(Math.Exp(-0.05 * 3), df, 12);
    }

    [Fact]
    public void DiscountFactor_AtTimeZero_IsOne()
    {
        Assert.Equal(1.0, YieldCurve.DiscountFactor(0.05, 0), 12);
    }

    [Fact]
    public void DiscountFactor_DecreasesWithMaturity()
    {
        double near = YieldCurve.DiscountFactor(0.04, 1);
        double far = YieldCurve.DiscountFactor(0.04, 10);
        Assert.True(far < near);
    }

    [Fact]
    public void DiscountFactorSimple_MatchesFormula()
    {
        double df = YieldCurve.DiscountFactorSimple(simpleRate: 0.05, delta: 0.5);
        Assert.Equal(1.0 / (1 + 0.05 * 0.5), df, 12);
        Assert.True(df > 0 && df <= 1);
    }

    [Fact]
    public void DiscountFactors_MatchScalarVersion()
    {
        double[] rates = { 0.03, 0.04, 0.05 };
        double[] maturities = { 1, 2, 3 };
        double[] dfs = YieldCurve.DiscountFactors(rates, maturities);

        Assert.Equal(3, dfs.Length);
        for (int i = 0; i < dfs.Length; i++)
            Assert.Equal(YieldCurve.DiscountFactor(rates[i], maturities[i]), dfs[i], 12);
    }

    [Fact]
    public void DiscountFactors_MismatchedLengths_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            YieldCurve.DiscountFactors(new[] { 0.03, 0.04 }, new[] { 1.0 }));
    }

    // ── Rate conversions (inverses) ────────────────────────────────────────────

    [Fact]
    public void ToContinuous_FromContinuous_RoundTrips()
    {
        double simple = 0.06;
        int freq = 2;
        double cont = YieldCurve.ToContinuous(simple, freq);
        double recovered = YieldCurve.FromContinuous(cont, freq);
        Assert.Equal(simple, recovered, 12);
    }

    [Fact]
    public void FromContinuous_ToContinuous_RoundTrips()
    {
        double cont = 0.05;
        int freq = 4;
        double simple = YieldCurve.FromContinuous(cont, freq);
        double recovered = YieldCurve.ToContinuous(simple, freq);
        Assert.Equal(cont, recovered, 12);
    }

    [Fact]
    public void ToContinuous_IsBelowSimpleRate()
    {
        // Continuous compounding requires a lower rate to reach the same growth.
        double cont = YieldCurve.ToContinuous(0.06, 2);
        Assert.True(cont < 0.06);
    }

    [Fact]
    public void DefaultFrequency_IsSemiAnnual()
    {
        Assert.Equal(YieldCurve.ToContinuous(0.06, 2), YieldCurve.ToContinuous(0.06), 12);
        Assert.Equal(YieldCurve.FromContinuous(0.06, 2), YieldCurve.FromContinuous(0.06), 12);
    }

    // ── Forward rates ──────────────────────────────────────────────────────────

    [Fact]
    public void ForwardRate_FlatCurve_EqualsZeroRate()
    {
        // Flat zero curve => forward rate equals that flat rate.
        double fwd = YieldCurve.ForwardRate(r1: 0.05, t1: 1, r2: 0.05, t2: 2);
        Assert.Equal(0.05, fwd, 12);
    }

    [Fact]
    public void ForwardRate_MatchesFormula()
    {
        double r1 = 0.04, t1 = 1, r2 = 0.05, t2 = 2;
        double fwd = YieldCurve.ForwardRate(r1, t1, r2, t2);
        Assert.Equal((r2 * t2 - r1 * t1) / (t2 - t1), fwd, 12);
    }

    [Fact]
    public void ForwardRate_InvalidPeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => YieldCurve.ForwardRate(0.04, 2, 0.05, 1));
        Assert.Throws<ArgumentException>(() => YieldCurve.ForwardRate(0.04, 1, 0.05, 1));
    }

    [Fact]
    public void ForwardRateCurve_FirstElementIsFirstZeroRate()
    {
        double[] maturities = { 1, 2, 3 };
        double[] zeros = { 0.03, 0.04, 0.05 };
        double[] fwd = YieldCurve.ForwardRateCurve(maturities, zeros);

        Assert.Equal(3, fwd.Length);
        Assert.Equal(zeros[0], fwd[0], 12);
        // Subsequent elements match the pairwise forward rate.
        Assert.Equal(YieldCurve.ForwardRate(zeros[0], maturities[0], zeros[1], maturities[1]), fwd[1], 12);
        Assert.Equal(YieldCurve.ForwardRate(zeros[1], maturities[1], zeros[2], maturities[2]), fwd[2], 12);
    }

    [Fact]
    public void ForwardRateCurve_FlatCurve_IsFlat()
    {
        double[] maturities = { 1, 2, 3 };
        double[] zeros = { 0.05, 0.05, 0.05 };
        double[] fwd = YieldCurve.ForwardRateCurve(maturities, zeros);
        foreach (double f in fwd)
            Assert.Equal(0.05, f, 12);
    }

    [Fact]
    public void InstantaneousForwardRate_FlatCurve_EqualsZeroRate()
    {
        double[] maturities = { 1, 2, 3, 5 };
        double[] zeros = { 0.05, 0.05, 0.05, 0.05 };
        // dr/dt = 0 => f(t) = r(t) = flat rate.
        double f = YieldCurve.InstantaneousForwardRate(maturities, zeros, 2.5);
        Assert.Equal(0.05, f, 6);
    }

    // ── Interpolation ──────────────────────────────────────────────────────────

    [Fact]
    public void InterpolateZeroRate_AtNode_ReturnsNodeValue()
    {
        double[] maturities = { 1, 2, 3 };
        double[] zeros = { 0.03, 0.04, 0.05 };
        Assert.Equal(0.04, YieldCurve.InterpolateZeroRate(maturities, zeros, 2), 12);
    }

    [Fact]
    public void InterpolateZeroRate_Midpoint_IsLinearAverage()
    {
        double[] maturities = { 1, 3 };
        double[] zeros = { 0.03, 0.05 };
        // Midpoint t=2 => average of the two rates.
        Assert.Equal(0.04, YieldCurve.InterpolateZeroRate(maturities, zeros, 2), 12);
    }

    [Fact]
    public void InterpolateZeroRate_FlatExtrapolationAtEnds()
    {
        double[] maturities = { 1, 2, 3 };
        double[] zeros = { 0.03, 0.04, 0.05 };
        Assert.Equal(0.03, YieldCurve.InterpolateZeroRate(maturities, zeros, 0.5), 12);
        Assert.Equal(0.05, YieldCurve.InterpolateZeroRate(maturities, zeros, 10), 12);
    }

    // ── Bootstrapping ──────────────────────────────────────────────────────────

    [Fact]
    public void Bootstrap_FlatParCurve_RepricesParBondsAtFace()
    {
        // Bootstrap zeros from par rates, then a par bond priced off those zeros
        // must equal face value. We verify each maturity reprices to 1.0.
        double[] maturities = { 0.5, 1.0, 1.5, 2.0 };
        double[] parRates = { 0.05, 0.05, 0.05, 0.05 };
        int freq = 2;
        double delta = 1.0 / freq;

        double[] zeros = YieldCurve.Bootstrap(maturities, parRates, freq);
        Assert.Equal(maturities.Length, zeros.Length);

        // Reprice the par bond at the longest maturity.
        double c = parRates[^1];
        double price = 0;
        for (int i = 0; i < maturities.Length; i++)
            price += c * delta * YieldCurve.DiscountFactor(zeros[i], maturities[i]);
        price += YieldCurve.DiscountFactor(zeros[^1], maturities[^1]); // redemption
        Assert.Equal(1.0, price, 10);
    }

    [Fact]
    public void Bootstrap_ProducesPositiveRates()
    {
        double[] maturities = { 0.5, 1.0, 1.5, 2.0 };
        double[] parRates = { 0.03, 0.035, 0.04, 0.045 };
        double[] zeros = YieldCurve.Bootstrap(maturities, parRates);
        foreach (double z in zeros)
            Assert.True(z > 0);
    }

    [Fact]
    public void BootstrapFromPrices_RepricesEachBondToObservedPrice()
    {
        double[] maturities = { 0.5, 1.0, 1.5, 2.0 };
        double[] couponRates = { 0.04, 0.05, 0.06, 0.05 };
        double[] prices = { 1.00, 1.01, 0.99, 1.02 };
        int freq = 2;
        double delta = 1.0 / freq;

        double[] zeros = YieldCurve.BootstrapFromPrices(maturities, couponRates, prices, freq);

        // Reprice each bond using the bootstrapped zeros; must match input price.
        for (int b = 0; b < maturities.Length; b++)
        {
            double c = couponRates[b];
            double price = 0;
            for (int i = 0; i <= b; i++)
                price += c * delta * YieldCurve.DiscountFactor(zeros[i], maturities[i]);
            price += YieldCurve.DiscountFactor(zeros[b], maturities[b]); // redemption
            Assert.Equal(prices[b], price, 10);
        }
    }

    // ── Par yield ──────────────────────────────────────────────────────────────

    [Fact]
    public void ParYield_RoundTripsWithBootstrap()
    {
        // Bootstrapping from a flat par curve, then recomputing the par yield,
        // should recover the original par rate.
        double[] maturities = { 0.5, 1.0, 1.5, 2.0 };
        double[] parRates = { 0.05, 0.05, 0.05, 0.05 };
        int freq = 2;

        double[] zeros = YieldCurve.Bootstrap(maturities, parRates, freq);
        double parYield = YieldCurve.ParYield(maturities, zeros, targetMaturity: 2.0, frequency: freq);
        Assert.Equal(0.05, parYield, 8);
    }

    [Fact]
    public void ParYield_IsPositiveForUpwardCurve()
    {
        double[] maturities = { 0.5, 1.0, 1.5, 2.0 };
        double[] zeros = { 0.03, 0.035, 0.04, 0.045 };
        double y = YieldCurve.ParYield(maturities, zeros, 2.0);
        Assert.True(y > 0);
    }
}
