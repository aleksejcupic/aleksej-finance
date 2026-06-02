using Aleksej.Finance.Credit;

namespace Aleksej.Finance.Tests;

public class CreditDerivativesTests
{
    // ── Merton structural model ───────────────────────────────────────────────

    [Fact]
    public void MertonEquityPlusDebt_EqualsAssetValue()
    {
        double v = 100, d = 80, t = 1, r = 0.05, sigma = 0.20;
        double equity = CreditDerivatives.MertonEquityValue(v, d, t, r, sigma);
        double debt = CreditDerivatives.MertonDebtValue(v, d, t, r, sigma);
        Assert.Equal(v, equity + debt, 8);
    }

    [Fact]
    public void MertonEquityValue_NonNegative()
    {
        double equity = CreditDerivatives.MertonEquityValue(100, 80, 1, 0.05, 0.20);
        Assert.True(equity >= 0);
    }

    [Fact]
    public void MertonEquityValue_AtMaturityIsIntrinsic()
    {
        // t <= 0 returns max(v - d, 0)
        Assert.Equal(20.0, CreditDerivatives.MertonEquityValue(100, 80, 0, 0.05, 0.20), 8);
        Assert.Equal(0.0, CreditDerivatives.MertonEquityValue(60, 80, 0, 0.05, 0.20), 8);
    }

    [Fact]
    public void MertonDefaultProbability_InUnitInterval()
    {
        double pd = CreditDerivatives.MertonDefaultProbability(100, 80, 1, 0.05, 0.20);
        Assert.True(pd >= 0 && pd <= 1);
    }

    [Fact]
    public void MertonDefaultProbability_HigherLeverageIncreasesPd()
    {
        double pdLow = CreditDerivatives.MertonDefaultProbability(100, 50, 1, 0.05, 0.20);
        double pdHigh = CreditDerivatives.MertonDefaultProbability(100, 90, 1, 0.05, 0.20);
        Assert.True(pdHigh > pdLow);
    }

    [Fact]
    public void DistanceToDefault_HigherForSafarFirm()
    {
        // Lower leverage (smaller debt) => higher distance to default
        double ddSafe = CreditDerivatives.DistanceToDefault(100, 50, 1, 0.05, 0.20);
        double ddRisky = CreditDerivatives.DistanceToDefault(100, 90, 1, 0.05, 0.20);
        Assert.True(ddSafe > ddRisky);
    }

    [Fact]
    public void DistanceToDefault_ConsistentWithDefaultProbability()
    {
        // PD = N(-d2) and DD = d2, so higher DD => lower PD
        double dd = CreditDerivatives.DistanceToDefault(100, 80, 1, 0.05, 0.20);
        double pd = CreditDerivatives.MertonDefaultProbability(100, 80, 1, 0.05, 0.20);
        double ddHigher = CreditDerivatives.DistanceToDefault(100, 60, 1, 0.05, 0.20);
        double pdHigher = CreditDerivatives.MertonDefaultProbability(100, 60, 1, 0.05, 0.20);
        Assert.True(ddHigher > dd);
        Assert.True(pdHigher < pd);
    }

    [Fact]
    public void MertonCreditSpread_NonNegative()
    {
        double spread = CreditDerivatives.MertonCreditSpread(100, 80, 1, 0.05, 0.20);
        Assert.True(spread >= 0);
    }

    [Fact]
    public void MertonCreditSpread_ZeroAtMaturity()
    {
        Assert.Equal(0.0, CreditDerivatives.MertonCreditSpread(100, 80, 0, 0.05, 0.20), 8);
    }

    // ── Hazard rate / reduced-form CDS model ─────────────────────────────────

    [Fact]
    public void SurvivalProbability_KnownValue()
    {
        // Q(t) = exp(-lambda * t)
        Assert.Equal(Math.Exp(-0.02 * 5), CreditDerivatives.SurvivalProbability(0.02, 5), 10);
    }

    [Fact]
    public void SurvivalProbability_AtTimeZeroIsOne()
    {
        Assert.Equal(1.0, CreditDerivatives.SurvivalProbability(0.02, 0), 10);
    }

    [Fact]
    public void SurvivalProbability_DecreasingInTime()
    {
        double q1 = CreditDerivatives.SurvivalProbability(0.02, 1);
        double q5 = CreditDerivatives.SurvivalProbability(0.02, 5);
        double q10 = CreditDerivatives.SurvivalProbability(0.02, 10);
        Assert.True(q1 > q5);
        Assert.True(q5 > q10);
    }

    [Fact]
    public void SurvivalPlusDefaultProbability_EqualsOne()
    {
        double q = CreditDerivatives.SurvivalProbability(0.03, 4);
        double pd = CreditDerivatives.DefaultProbability(0.03, 4);
        Assert.Equal(1.0, q + pd, 10);
    }

    [Fact]
    public void DefaultProbability_InUnitInterval()
    {
        double pd = CreditDerivatives.DefaultProbability(0.05, 7);
        Assert.True(pd >= 0 && pd <= 1);
    }

    [Fact]
    public void HazardRateFromSpread_KnownApproximation()
    {
        // lambda = s / (1 - R) = 0.012 / 0.6 = 0.02
        Assert.Equal(0.02, CreditDerivatives.HazardRateFromSpread(0.012, 0.40), 10);
    }

    [Fact]
    public void HazardRateFromSpread_DefaultRecoveryRate()
    {
        // default R = 0.40
        Assert.Equal(0.012 / 0.6, CreditDerivatives.HazardRateFromSpread(0.012), 10);
    }

    [Fact]
    public void CdsFairSpread_Positive()
    {
        double spread = CreditDerivatives.CdsFairSpread(0.02, 0.03, 5);
        Assert.True(spread > 0);
    }

    [Fact]
    public void CdsFairSpread_IncreasesWithHazardRate()
    {
        double low = CreditDerivatives.CdsFairSpread(0.01, 0.03, 5);
        double high = CreditDerivatives.CdsFairSpread(0.05, 0.03, 5);
        Assert.True(high > low);
    }

    [Fact]
    public void CdsFairSpread_ApproximatesSpreadHazardIdentity()
    {
        // For small hazard and rates, s ~ lambda * (1 - R)
        double hazard = 0.01;
        double recovery = 0.40;
        double spread = CreditDerivatives.CdsFairSpread(hazard, 0.0, 5, recovery);
        double approx = hazard * (1 - recovery);
        // Loose tolerance: discretisation and timing make this approximate
        Assert.Equal(approx, spread, 3);
    }

    [Fact]
    public void CdsMtm_ZeroWhenContractedEqualsFair()
    {
        double hazard = 0.02, r = 0.03, maturity = 5;
        double fair = CreditDerivatives.CdsFairSpread(hazard, r, maturity);
        double mtm = CreditDerivatives.CdsMtm(fair, hazard, r, 10_000_000, maturity);
        Assert.Equal(0.0, mtm, 4);
    }

    [Fact]
    public void CdsMtm_PositiveForProtectionBuyerWhenCreditDeteriorates()
    {
        // Bought protection at a low contracted spread; credit worsens (fair > contracted)
        double mtm = CreditDerivatives.CdsMtm(0.005, 0.03, 0.03, 10_000_000, 5);
        Assert.True(mtm > 0);
    }
}
