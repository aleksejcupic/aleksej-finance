using AleksejCupic.FinancialMath.Derivatives;

namespace AleksejCupic.FinancialMath.Tests;

public class ShortRateModelsTests
{
    // Representative positive parameter set.
    private const double R = 0.03, Tau = 5.0, Kappa = 0.3, Theta = 0.05, Sigma = 0.02;

    [Fact]
    public void VasicekBondPrice_IsInZeroToOneForPositiveRate()
    {
        double p = ShortRateModels.VasicekBondPrice(R, Tau, Kappa, Theta, Sigma);
        Assert.InRange(p, 0.0, 1.0);
    }

    [Fact]
    public void VasicekBondPrice_AtZeroMaturityApproachesOne()
    {
        double p = ShortRateModels.VasicekBondPrice(R, 1e-8, Kappa, Theta, Sigma);
        Assert.Equal(1.0, p, 5);
    }

    [Fact]
    public void VasicekYield_MatchesNegativeLogPriceIdentity()
    {
        double p = ShortRateModels.VasicekBondPrice(R, Tau, Kappa, Theta, Sigma);
        double y = ShortRateModels.VasicekYield(R, Tau, Kappa, Theta, Sigma);
        Assert.Equal(-Math.Log(p) / Tau, y, 12);
    }

    [Fact]
    public void VasicekLongRunYield_MatchesClosedForm()
    {
        double y = ShortRateModels.VasicekLongRunYield(Kappa, Theta, Sigma);
        Assert.Equal(Theta - Sigma * Sigma / (2 * Kappa * Kappa), y, 12);
    }

    [Fact]
    public void VasicekYield_ConvergesToLongRunYieldForLargeTau()
    {
        // Convergence to the long-run yield is asymptotic (O(1/tau)); at tau=500 the residual
        // is ~1e-4, so assert agreement to 3 decimal places (the closed form overflows for huge tau).
        double yieldLong = ShortRateModels.VasicekYield(R, 500.0, Kappa, Theta, Sigma);
        double longRun = ShortRateModels.VasicekLongRunYield(Kappa, Theta, Sigma);
        Assert.Equal(longRun, yieldLong, 3);
    }

    [Fact]
    public void VasicekBondOption_PutCallParityHolds()
    {
        // call - put = P(0,T_bond) - K * P(0,T_expiry)
        double tExpiry = 1.0, maturity = 5.0, k = 0.8;
        double call = ShortRateModels.VasicekBondOption(R, tExpiry, maturity, k, Kappa, Theta, Sigma, isPut: false);
        double put = ShortRateModels.VasicekBondOption(R, tExpiry, maturity, k, Kappa, Theta, Sigma, isPut: true);
        double pBond = ShortRateModels.VasicekBondPrice(R, maturity, Kappa, Theta, Sigma);
        double pExpiry = ShortRateModels.VasicekBondPrice(R, tExpiry, Kappa, Theta, Sigma);
        Assert.Equal(pBond - k * pExpiry, call - put, 9);
    }

    [Fact]
    public void VasicekBondOption_PricesAreNonNegative()
    {
        double call = ShortRateModels.VasicekBondOption(R, 1.0, 5.0, 0.8, Kappa, Theta, Sigma, isPut: false);
        double put = ShortRateModels.VasicekBondOption(R, 1.0, 5.0, 0.8, Kappa, Theta, Sigma, isPut: true);
        Assert.True(call >= 0);
        Assert.True(put >= 0);
    }

    [Fact]
    public void CirBondPrice_IsInZeroToOneForPositiveRate()
    {
        double p = ShortRateModels.CirBondPrice(R, Tau, Kappa, Theta, Sigma);
        Assert.InRange(p, 0.0, 1.0);
    }

    [Fact]
    public void CirYield_MatchesNegativeLogPriceIdentity()
    {
        double p = ShortRateModels.CirBondPrice(R, Tau, Kappa, Theta, Sigma);
        double y = ShortRateModels.CirYield(R, Tau, Kappa, Theta, Sigma);
        Assert.Equal(-Math.Log(p) / Tau, y, 12);
    }

    [Fact]
    public void CirLongRunYield_MatchesClosedForm()
    {
        double gamma = Math.Sqrt(Kappa * Kappa + 2 * Sigma * Sigma);
        double expected = 2 * Kappa * Theta / (Kappa + gamma);
        Assert.Equal(expected, ShortRateModels.CirLongRunYield(Kappa, Theta, Sigma), 12);
    }

    [Fact]
    public void CirYield_ConvergesToLongRunYieldForLargeTau()
    {
        // Convergence to the long-run yield is asymptotic (O(1/tau)); at tau=500 the residual
        // is ~1e-4, so assert agreement to 3 decimal places (the closed form overflows for huge tau).
        double yieldLong = ShortRateModels.CirYield(R, 500.0, Kappa, Theta, Sigma);
        double longRun = ShortRateModels.CirLongRunYield(Kappa, Theta, Sigma);
        Assert.Equal(longRun, yieldLong, 3);
    }

    [Fact]
    public void VasicekTermStructure_MatchesPerMaturityYields()
    {
        double[] maturities = { 1.0, 2.0, 5.0, 10.0 };
        double[] rates = ShortRateModels.VasicekTermStructure(R, maturities, Kappa, Theta, Sigma);
        Assert.Equal(maturities.Length, rates.Length);
        for (int i = 0; i < maturities.Length; i++)
            Assert.Equal(ShortRateModels.VasicekYield(R, maturities[i], Kappa, Theta, Sigma), rates[i], 12);
    }

    [Fact]
    public void CirTermStructure_MatchesPerMaturityYields()
    {
        double[] maturities = { 1.0, 2.0, 5.0, 10.0 };
        double[] rates = ShortRateModels.CirTermStructure(R, maturities, Kappa, Theta, Sigma);
        Assert.Equal(maturities.Length, rates.Length);
        for (int i = 0; i < maturities.Length; i++)
            Assert.Equal(ShortRateModels.CirYield(R, maturities[i], Kappa, Theta, Sigma), rates[i], 12);
    }
}
