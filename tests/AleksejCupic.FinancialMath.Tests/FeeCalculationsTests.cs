using AleksejCupic.FinancialMath.Portfolio;

namespace AleksejCupic.FinancialMath.Tests;

public class FeeCalculationsTests
{
    [Fact]
    public void ManagementFee_AccruesProRata()
    {
        // 100m AUM, 2% annual, 90 days of 365
        double expected = 100_000_000 * 0.02 * (90.0 / 365.0);
        Assert.Equal(expected, FeeCalculations.ManagementFee(100_000_000, 0.02, 90), 6);
    }

    [Fact]
    public void ManagementFee_FullYearMatchesAnnual()
    {
        double prorata = FeeCalculations.ManagementFee(50_000_000, 0.02, 365, 365);
        double annual = FeeCalculations.ManagementFeeAnnual(50_000_000, 0.02);
        Assert.Equal(annual, prorata, 6);
    }

    [Fact]
    public void ManagementFeeAnnual_IsAumTimesRate()
    {
        Assert.Equal(1_000_000.0, FeeCalculations.ManagementFeeAnnual(50_000_000, 0.02), 6);
    }

    [Fact]
    public void PerformanceFee_AboveHwmChargesCarryOnExcess()
    {
        // NAV 120, HWM 100, prevNav 100, carry 20%, no hurdle
        // excess = 20, fee = 4
        Assert.Equal(4.0, FeeCalculations.PerformanceFee(120, 100, 100, 0.20, 0.0), 6);
    }

    [Fact]
    public void PerformanceFee_BelowHwmIsZero()
    {
        // NAV 95 below HWM 110 -> no fee
        Assert.Equal(0.0, FeeCalculations.PerformanceFee(95, 110, 90, 0.20, 0.0), 8);
    }

    [Fact]
    public void PerformanceFee_HurdleRaisesEffectiveHwm()
    {
        // prevNav 100, hurdle 10% -> effective HWM = max(100, 110) = 110
        // NAV 105 is below 110 -> zero fee despite gain over prevNav
        Assert.Equal(0.0, FeeCalculations.PerformanceFee(105, 100, 100, 0.20, 0.10), 8);
    }

    [Fact]
    public void PerformanceFee_HurdleOnlyExcessAboveHurdleCharged()
    {
        // prevNav 100, hurdle 10% -> effective HWM 110, NAV 120
        // excess = 10, fee = 2
        Assert.Equal(2.0, FeeCalculations.PerformanceFee(120, 100, 100, 0.20, 0.10), 6);
    }

    [Fact]
    public void NetNavAfterPerformanceFee_SubtractsFee()
    {
        double fee = FeeCalculations.PerformanceFee(120, 100, 100, 0.20, 0.0);
        Assert.Equal(120 - fee, FeeCalculations.NetNavAfterPerformanceFee(120, 100, 100, 0.20, 0.0), 6);
    }

    [Fact]
    public void HighWaterMark_IsRunningMaximum()
    {
        double[] series = { 100, 110, 105, 130, 120 };
        Assert.Equal(130.0, FeeCalculations.HighWaterMark(series), 8);
    }

    [Fact]
    public void HighWaterMark_EmptySeriesIsZero()
    {
        Assert.Equal(0.0, FeeCalculations.HighWaterMark(Array.Empty<double>()), 8);
    }

    [Fact]
    public void CarriedInterest_AboveHurdleChargesCarry()
    {
        // invested 1000, preferred 8% for 1y -> 1080
        // distributions 1280 -> profit above hurdle 200, carry 20% = 40
        Assert.Equal(40.0, FeeCalculations.CarriedInterest(1280, 1000, 0.08, 1.0, 0.20), 6);
    }

    [Fact]
    public void CarriedInterest_BelowHurdleIsZero()
    {
        // distributions 1050 below 1080 hurdle -> zero carry
        Assert.Equal(0.0, FeeCalculations.CarriedInterest(1050, 1000, 0.08, 1.0, 0.20), 8);
    }

    [Fact]
    public void CatchUpAmount_CappedAtFullCarry()
    {
        // profit 1000, carry 20% -> full carry 200
        // catch-up pool = profit - preferred = 1000 - 100 = 900 at 100% = 900
        // capped at full carry 200
        Assert.Equal(200.0, FeeCalculations.CatchUpAmount(1000, 100, 0.20, 1.0), 6);
    }

    [Fact]
    public void CatchUpAmount_UsesPoolWhenBelowFullCarry()
    {
        // profit 1000, carry 20% -> full carry 200
        // pool = 1000 - 950 = 50 at 100% = 50 (below full carry) -> 50
        Assert.Equal(50.0, FeeCalculations.CatchUpAmount(1000, 950, 0.20, 1.0), 6);
    }

    [Fact]
    public void NetReturnAfterFees_ExactFormula()
    {
        // (1.10 / 1.01) - 1
        double expected = 1.10 / 1.01 - 1;
        Assert.Equal(expected, FeeCalculations.NetReturnAfterFees(0.10, 0.01), 10);
    }

    [Fact]
    public void NetReturnAfterFees_LessThanGross()
    {
        Assert.True(FeeCalculations.NetReturnAfterFees(0.10, 0.01) < 0.10);
    }

    [Fact]
    public void CumulativeFeeImpact_PositiveAndGrowsWithHorizon()
    {
        double impact5 = FeeCalculations.CumulativeFeeImpact(0.10, 0.01, 5);
        double impact10 = FeeCalculations.CumulativeFeeImpact(0.10, 0.01, 10);
        Assert.True(impact5 > 0);
        Assert.True(impact10 > impact5);
    }

    [Fact]
    public void TotalTransactionCost_CommissionPlusHalfSpread()
    {
        // 1,000,000 * 0.001 + 1,000,000 * 4 / 10000 / 2
        // = 1000 + 200 = 1200
        Assert.Equal(1200.0, FeeCalculations.TotalTransactionCost(1_000_000, 0.001, 4), 6);
    }

    [Fact]
    public void SlippageCost_KnownValue()
    {
        // 1,000,000 * 5 / 10000 = 500
        Assert.Equal(500.0, FeeCalculations.SlippageCost(1_000_000, 5), 6);
    }

    [Fact]
    public void BreakEvenReturn_IsCostOverValue()
    {
        Assert.Equal(0.0012, FeeCalculations.BreakEvenReturn(1200, 1_000_000), 8);
    }

    [Fact]
    public void BreakEvenReturn_ZeroValueIsNaN()
    {
        Assert.True(double.IsNaN(FeeCalculations.BreakEvenReturn(1200, 0)));
    }
}
