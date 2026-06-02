using System;
using System.Linq;
using Aleksej.Finance.Risk;

namespace Aleksej.Finance.Tests;

public class RiskMetricsMoreTests
{
    // ── Annualised return / volatility ────────────────────────────────────────

    [Fact]
    public void AnnualisedReturn_EqualsMeanTimesTradingDays()
    {
        // Mean of {0.01, 0.02, 0.03} = 0.02; with 10 trading days => 0.20.
        double[] returns = { 0.01, 0.02, 0.03 };
        double ann = RiskMetrics.AnnualisedReturn(returns, tradingDays: 10);
        Assert.Equal(0.20, ann, 12);
    }

    [Fact]
    public void AnnualisedVolatility_IsZero_ForConstantSeries()
    {
        double[] returns = { 0.01, 0.01, 0.01, 0.01 };
        double vol = RiskMetrics.AnnualisedVolatility(returns);
        Assert.Equal(0.0, vol, 12);
    }

    [Fact]
    public void AnnualisedVolatility_IsPositive_ForVaryingSeries()
    {
        double[] returns = { 0.01, -0.02, 0.03, -0.01, 0.02 };
        double vol = RiskMetrics.AnnualisedVolatility(returns);
        Assert.True(vol > 0);
    }

    // ── Value at Risk ─────────────────────────────────────────────────────────

    [Fact]
    public void HistoricalVaR_IsAPositiveTailLoss()
    {
        // The exact order-statistic index is fp-sensitive at boundaries, so assert robust
        // properties: VaR is positive, equals the magnitude of one of the observed returns,
        // and does not decrease as confidence rises.
        double[] returns = { 0.04, -0.03, 0.10, 0.02, -0.05, 0.08, -0.01, 0.06, 0.12, 0.14 };
        double var95 = RiskMetrics.HistoricalVaR(returns, confidence: 0.95);
        double var90 = RiskMetrics.HistoricalVaR(returns, confidence: 0.90);
        Assert.True(var95 > 0);
        Assert.Contains(returns, x => Math.Abs(-var95 - x) < 1e-12);
        Assert.True(var95 >= var90 - 1e-12);
    }

    [Fact]
    public void ConditionalVaR_IsAtLeastHistoricalVaR_SameConfidence()
    {
        double[] returns = { 0.04, -0.03, 0.10, 0.02, -0.05, 0.08, -0.01, 0.06, 0.12, 0.14 };
        double cvar = RiskMetrics.ConditionalVaR(returns, confidence: 0.90);
        double var = RiskMetrics.HistoricalVaR(returns, confidence: 0.90);
        Assert.True(cvar >= var);
    }

    [Fact]
    public void ConditionalVaR_AveragesWorstTail_HandComputed()
    {
        // Two equal worst returns (-0.05), so the tail average is -0.05 regardless of whether
        // the cutoff count rounds to 1 or 2 (avoids fp sensitivity in the cutoff index).
        double[] returns = { -0.05, -0.05, 0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07, 0.08 };
        double cvar = RiskMetrics.ConditionalVaR(returns, confidence: 0.80);
        Assert.Equal(0.05, cvar, 12);
    }

    [Fact]
    public void ParametricVaR_IsPositiveAndFinite_ForRiskySeries()
    {
        double[] returns = { 0.01, -0.02, 0.03, -0.04, 0.02, -0.03, 0.01, -0.05 };
        double pvar = RiskMetrics.ParametricVaR(returns, confidence: 0.95);
        Assert.True(double.IsFinite(pvar));
        Assert.True(pvar > 0);
    }

    // ── Performance ratios ────────────────────────────────────────────────────

    [Fact]
    public void SortinoRatio_IsPositive_WhenReturnsBeatRiskFree()
    {
        double[] returns = { 0.002, 0.003, -0.001, 0.004, 0.001, -0.002, 0.003 };
        double sortino = RiskMetrics.SortinoRatio(returns, riskFreeRateAnnual: 0.0);
        Assert.True(double.IsFinite(sortino));
        Assert.True(sortino > 0);
    }

    [Fact]
    public void SortinoRatio_IsPositiveInfinity_WhenNoDownsideBelowMar()
    {
        // All returns >= MAR (= rf/tradingDays = 0 here), so no downside observations.
        double[] returns = { 0.01, 0.02, 0.03, 0.0 };
        double sortino = RiskMetrics.SortinoRatio(returns, riskFreeRateAnnual: 0.0);
        Assert.True(double.IsPositiveInfinity(sortino));
    }

    [Fact]
    public void CalmarRatio_IsPositive_WhenReturnPositiveAndDrawdownExists()
    {
        double[] returns = { 0.05, -0.02, 0.04, -0.01, 0.03 };
        double calmar = RiskMetrics.CalmarRatio(returns);
        Assert.True(double.IsFinite(calmar));
        Assert.True(calmar > 0);
    }

    [Fact]
    public void CalmarRatio_IsNaN_WhenNoDrawdown()
    {
        double[] returns = { 0.01, 0.02, 0.03 };
        double calmar = RiskMetrics.CalmarRatio(returns);
        Assert.True(double.IsNaN(calmar));
    }

    // ── Beta ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Beta_OfSeriesAgainstItself_IsOne()
    {
        double[] x = { 0.01, -0.02, 0.03, -0.01, 0.02 };
        double beta = RiskMetrics.Beta(x, x);
        Assert.Equal(1.0, beta, 12);
    }

    [Fact]
    public void Beta_OfDoubledSeriesAgainstBase_IsTwo()
    {
        // Cov(2x, x) / Var(x) = 2 * Var(x) / Var(x) = 2.
        double[] x = { 0.01, -0.02, 0.03, -0.01, 0.02 };
        double[] twoX = x.Select(v => 2 * v).ToArray();
        double beta = RiskMetrics.Beta(twoX, x);
        Assert.Equal(2.0, beta, 12);
    }

    [Fact]
    public void Beta_OfBaseAgainstDoubledBenchmark_IsHalf()
    {
        // Cov(x, 2x) / Var(2x) = 2 * Var(x) / (4 * Var(x)) = 0.5.
        double[] x = { 0.01, -0.02, 0.03, -0.01, 0.02 };
        double[] twoX = x.Select(v => 2 * v).ToArray();
        double beta = RiskMetrics.Beta(x, twoX);
        Assert.Equal(0.5, beta, 12);
    }

    [Fact]
    public void Beta_ThrowsOnMismatchedLengths()
    {
        double[] a = { 0.01, 0.02, 0.03 };
        double[] b = { 0.01, 0.02 };
        Assert.Throws<ArgumentException>(() => RiskMetrics.Beta(a, b));
    }

    // ── Jensen's Alpha ──────────────────────────────────────────────────────────

    [Fact]
    public void JensensAlpha_IsZero_WhenPortfolioEqualsBenchmark()
    {
        // Portfolio == benchmark => beta = 1, retP == retM.
        // alpha = retP - (rf + 1 * (retM - rf)) = retP - retM = 0.
        double[] x = { 0.01, -0.02, 0.03, -0.01, 0.02 };
        double alpha = RiskMetrics.JensensAlpha(x, x, riskFreeRateAnnual: 0.04);
        Assert.Equal(0.0, alpha, 12);
    }

    [Fact]
    public void JensensAlpha_IsPositive_WhenPortfolioOutperformsCapm()
    {
        // Portfolio = benchmark with a constant positive shift each day.
        // beta = 1, retP = retM + shift*tradingDays => alpha = shift*tradingDays > 0.
        double[] bench = { 0.01, -0.02, 0.03, -0.01, 0.02 };
        double[] port = bench.Select(v => v + 0.005).ToArray();
        double alpha = RiskMetrics.JensensAlpha(port, bench, riskFreeRateAnnual: 0.03, tradingDays: 10);
        // shift 0.005 * 10 trading days = 0.05.
        Assert.Equal(0.05, alpha, 10);
    }

    // ── Treynor ───────────────────────────────────────────────────────────────

    [Fact]
    public void TreynorRatio_IsFinite_ForNonZeroBeta()
    {
        double[] bench = { 0.01, -0.02, 0.03, -0.01, 0.02 };
        double[] port = bench.Select(v => 1.2 * v + 0.001).ToArray();
        double treynor = RiskMetrics.TreynorRatio(port, bench, riskFreeRateAnnual: 0.0);
        Assert.True(double.IsFinite(treynor));
    }

    [Fact]
    public void TreynorRatio_IsNaN_WhenBetaIsZero()
    {
        // Benchmark uncorrelated/orthogonal-ish: use a constant benchmark => beta NaN,
        // instead use a benchmark giving zero covariance.
        // Portfolio symmetric, benchmark whose deviations are orthogonal => cov = 0 => beta = 0.
        double[] port  = { 0.02, -0.02, 0.02, -0.02 };
        double[] bench = { 0.01,  0.01, -0.01, -0.01 };
        // Cov = (0.02)(0.01)+(-0.02)(0.01)+(0.02)(-0.01)+(-0.02)(-0.01) = 0 => beta 0.
        double treynor = RiskMetrics.TreynorRatio(port, bench, riskFreeRateAnnual: 0.0);
        Assert.True(double.IsNaN(treynor));
    }

    // ── Tracking error & information ratio ────────────────────────────────────

    [Fact]
    public void TrackingError_IsZero_AgainstItself()
    {
        double[] x = { 0.01, -0.02, 0.03, -0.01, 0.02 };
        double te = RiskMetrics.TrackingError(x, x);
        Assert.Equal(0.0, te, 12);
    }

    [Fact]
    public void TrackingError_IsPositive_AgainstDifferentBenchmark()
    {
        double[] port  = { 0.01, -0.02, 0.03, -0.01, 0.02 };
        double[] bench = { 0.02, -0.01, 0.01,  0.00, 0.03 };
        double te = RiskMetrics.TrackingError(port, bench);
        Assert.True(te > 0);
    }

    [Fact]
    public void TrackingError_ThrowsOnMismatchedLengths()
    {
        double[] a = { 0.01, 0.02, 0.03 };
        double[] b = { 0.01, 0.02 };
        Assert.Throws<ArgumentException>(() => RiskMetrics.TrackingError(a, b));
    }

    [Fact]
    public void InformationRatio_IsNaN_WhenTrackingErrorIsZero()
    {
        double[] x = { 0.01, -0.02, 0.03, -0.01, 0.02 };
        double ir = RiskMetrics.InformationRatio(x, x);
        Assert.True(double.IsNaN(ir));
    }

    [Fact]
    public void InformationRatio_IsPositive_WhenPortfolioOutperforms()
    {
        // Portfolio = benchmark + constant daily shift => positive active mean,
        // but active return varies enough? Active = constant => TE = 0 => NaN.
        // So use a benchmark that differs in both level and variation.
        double[] bench = { 0.01, -0.02, 0.03, -0.01, 0.02 };
        double[] port  = { 0.02, -0.01, 0.05,  0.00, 0.03 };
        double ir = RiskMetrics.InformationRatio(port, bench);
        Assert.True(double.IsFinite(ir));
        Assert.True(ir > 0);
    }
}
