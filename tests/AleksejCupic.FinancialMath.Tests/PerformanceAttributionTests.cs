using AleksejCupic.FinancialMath.Portfolio;

namespace AleksejCupic.FinancialMath.Tests;

public class PerformanceAttributionTests
{
    [Fact]
    public void TimeWeightedReturn_ChainLinksReturns()
    {
        // (1.10)(1.05)(0.95) - 1
        double[] r = { 0.10, 0.05, -0.05 };
        double expected = 1.10 * 1.05 * 0.95 - 1;
        Assert.Equal(expected, PerformanceAttribution.TimeWeightedReturn(r), 10);
    }

    [Fact]
    public void TimeWeightedReturn_EmptyIsZero()
    {
        Assert.Equal(0.0, PerformanceAttribution.TimeWeightedReturn(Array.Empty<double>()), 10);
    }

    [Fact]
    public void ModifiedDietz_NoCashFlowsReducesToSimpleReturn()
    {
        // (EV - BV) / BV when no cash flows
        double mdietz = PerformanceAttribution.ModifiedDietz(
            1000, 1100, Array.Empty<double>(), Array.Empty<double>(), 365);
        Assert.Equal(0.10, mdietz, 10);
    }

    [Fact]
    public void ModifiedDietz_KnownValueWithMidPeriodFlow()
    {
        // BV=1000, EV=1200, one contribution of 100 at day 182.5 of 365 (half period)
        // weight = (365 - 182.5)/365 = 0.5
        // numerator = 1200 - 1000 - 100 = 100
        // denominator = 1000 + 100*0.5 = 1050
        double expected = 100.0 / 1050.0;
        double mdietz = PerformanceAttribution.ModifiedDietz(
            1000, 1200, new double[] { 100 }, new double[] { 182.5 }, 365);
        Assert.Equal(expected, mdietz, 10);
    }

    [Fact]
    public void ModifiedDietz_ThrowsOnLengthMismatch()
    {
        Assert.Throws<ArgumentException>(() =>
            PerformanceAttribution.ModifiedDietz(
                1000, 1100, new double[] { 100, 50 }, new double[] { 10 }, 365));
    }

    [Fact]
    public void NetPresentValue_KnownDiscounting()
    {
        // -1000 at t=0, 1100 at t=1, r=0.05 continuous
        // npv = -1000 + 1100 * exp(-0.05)
        double[] cf = { -1000, 1100 };
        double[] t = { 0, 1 };
        double expected = -1000 + 1100 * Math.Exp(-0.05);
        Assert.Equal(expected, PerformanceAttribution.NetPresentValue(cf, t, 0.05), 8);
    }

    [Fact]
    public void NetPresentValue_ThrowsOnLengthMismatch()
    {
        Assert.Throws<ArgumentException>(() =>
            PerformanceAttribution.NetPresentValue(new double[] { 1, 2 }, new double[] { 1 }, 0.05));
    }

    [Fact]
    public void InternalRateOfReturn_NpvAtIrrIsZero()
    {
        double[] cf = { -1000, 300, 400, 500 };
        double[] t = { 0, 1, 2, 3 };
        double irr = PerformanceAttribution.InternalRateOfReturn(cf, t);
        double npvAtIrr = PerformanceAttribution.NetPresentValue(cf, t, irr);
        Assert.Equal(0.0, npvAtIrr, 6);
    }

    [Fact]
    public void InternalRateOfReturn_MatchesAnalyticTwoFlow()
    {
        // -1000 at t=0, 1100 at t=1; continuous IRR = ln(1.1)
        double[] cf = { -1000, 1100 };
        double[] t = { 0, 1 };
        double irr = PerformanceAttribution.InternalRateOfReturn(cf, t);
        Assert.Equal(Math.Log(1.1), irr, 6);
    }

    [Fact]
    public void AllocationEffect_KnownFormula()
    {
        // (0.5 - 0.4) * (0.08 - 0.05) = 0.1 * 0.03 = 0.003
        Assert.Equal(0.003, PerformanceAttribution.AllocationEffect(0.5, 0.4, 0.08, 0.05), 10);
    }

    [Fact]
    public void SelectionEffect_KnownFormula()
    {
        // 0.4 * (0.10 - 0.08) = 0.008
        Assert.Equal(0.008, PerformanceAttribution.SelectionEffect(0.4, 0.10, 0.08), 10);
    }

    [Fact]
    public void InteractionEffect_KnownFormula()
    {
        // (0.5 - 0.4) * (0.10 - 0.08) = 0.1 * 0.02 = 0.002
        Assert.Equal(0.002, PerformanceAttribution.InteractionEffect(0.5, 0.4, 0.10, 0.08), 10);
    }

    [Fact]
    public void ActiveReturn_IsDifference()
    {
        Assert.Equal(0.02, PerformanceAttribution.ActiveReturn(0.10, 0.08), 10);
    }

    [Fact]
    public void BhbAttribution_EffectsSumToActiveReturn()
    {
        double[] pw = { 0.5, 0.5 };
        double[] bw = { 0.4, 0.6 };
        double[] pr = { 0.10, 0.04 };
        double[] br = { 0.08, 0.03 };

        var (alloc, select, interact, active) =
            PerformanceAttribution.BhbAttribution(pw, bw, pr, br);

        Assert.Equal(active, alloc + select + interact, 10);
    }

    [Fact]
    public void BhbAttribution_ActiveReturnMatchesManualTotals()
    {
        double[] pw = { 0.5, 0.5 };
        double[] bw = { 0.4, 0.6 };
        double[] pr = { 0.10, 0.04 };
        double[] br = { 0.08, 0.03 };

        double totalPortfolio = 0.5 * 0.10 + 0.5 * 0.04;
        double totalBenchmark = 0.4 * 0.08 + 0.6 * 0.03;

        var result = PerformanceAttribution.BhbAttribution(pw, bw, pr, br);
        Assert.Equal(totalPortfolio - totalBenchmark, result.activeReturn, 10);
    }

    [Fact]
    public void BhbAttribution_ThrowsOnLengthMismatch()
    {
        Assert.Throws<ArgumentException>(() =>
            PerformanceAttribution.BhbAttribution(
                new double[] { 0.5, 0.5 },
                new double[] { 0.4 },
                new double[] { 0.10, 0.04 },
                new double[] { 0.08, 0.03 }));
    }
}
