using System;
using System.Linq;

namespace AleksejCupic.FinancialMath.Risk;

/// <summary>
/// Risk and return metrics for a portfolio or asset.
/// All return inputs are simple daily returns (e.g. 0.01 = +1%).
/// </summary>
public static class RiskMetrics
{
    // ── Sharpe Ratio ──────────────────────────────────────────────────────────

    /// <summary>
    /// Annualised Sharpe ratio: (AnnReturn − rf) / AnnVolatility.
    /// </summary>
    /// <param name="dailyReturns">Daily simple returns</param>
    /// <param name="riskFreeRateAnnual">Annual risk-free rate (e.g. 0.05)</param>
    /// <param name="tradingDays">Trading days per year (default 252)</param>
    public static double SharpeRatio(
        double[] dailyReturns, double riskFreeRateAnnual, int tradingDays = 252)
    {
        double annVol = AnnualisedVolatility(dailyReturns, tradingDays);
        return annVol > 0
            ? (AnnualisedReturn(dailyReturns, tradingDays) - riskFreeRateAnnual) / annVol
            : 0;
    }

    // ── Value at Risk ─────────────────────────────────────────────────────────

    /// <summary>
    /// Historical VaR — the loss not exceeded at the given confidence level.
    /// Returns a positive number (e.g. 0.03 = 3% potential loss).
    /// </summary>
    /// <param name="dailyReturns">Daily simple returns</param>
    /// <param name="confidence">Confidence level (default 0.95)</param>
    public static double HistoricalVaR(double[] dailyReturns, double confidence = 0.95)
    {
        var sorted = (double[])dailyReturns.Clone();
        Array.Sort(sorted);
        int idx = (int)Math.Floor((1 - confidence) * sorted.Length);
        idx = Math.Max(0, Math.Min(idx, sorted.Length - 1));
        return -sorted[idx];
    }

    /// <summary>
    /// Conditional VaR (CVaR / Expected Shortfall) — average loss beyond the VaR threshold.
    /// </summary>
    public static double ConditionalVaR(double[] dailyReturns, double confidence = 0.95)
    {
        var sorted = (double[])dailyReturns.Clone();
        Array.Sort(sorted);
        int cutoff = Math.Max(1, (int)Math.Floor((1 - confidence) * sorted.Length));
        double sum = 0;
        for (int i = 0; i < cutoff; i++) sum += sorted[i];
        return -sum / cutoff;
    }

    /// <summary>
    /// Parametric VaR — assumes normally distributed returns.
    /// Uses the series' historical mean and standard deviation.
    /// </summary>
    public static double ParametricVaR(double[] dailyReturns, double confidence = 0.95)
    {
        double mu  = Mean(dailyReturns);
        double std = StdDev(dailyReturns);
        return -(mu + NormalQuantile(1 - confidence) * std);
    }

    // ── Return & Volatility ───────────────────────────────────────────────────

    /// <summary>Annualised arithmetic mean return.</summary>
    public static double AnnualisedReturn(double[] dailyReturns, int tradingDays = 252)
        => Mean(dailyReturns) * tradingDays;

    /// <summary>Annualised volatility (sample standard deviation scaled by √tradingDays).</summary>
    public static double AnnualisedVolatility(double[] dailyReturns, int tradingDays = 252)
        => StdDev(dailyReturns) * Math.Sqrt(tradingDays);

    /// <summary>Maximum drawdown — largest peak-to-trough percentage decline.</summary>
    public static double MaxDrawdown(double[] dailyReturns)
    {
        double peak = 1, value = 1, maxDD = 0;
        foreach (double r in dailyReturns)
        {
            value *= 1 + r;
            if (value > peak) peak = value;
            double dd = (peak - value) / peak;
            if (dd > maxDD) maxDD = dd;
        }
        return maxDD;
    }

    // ── Performance ratios ────────────────────────────────────────────────────

    /// <summary>
    /// Sortino ratio — excess return per unit of downside deviation.
    /// Only returns below the minimum acceptable return (MAR = rf/252 daily) are penalised.
    /// </summary>
    public static double SortinoRatio(
        double[] dailyReturns, double riskFreeRateAnnual, int tradingDays = 252)
    {
        double ret = AnnualisedReturn(dailyReturns, tradingDays);
        double mar = riskFreeRateAnnual / tradingDays;

        double sumSq = 0; int count = 0;
        foreach (double r in dailyReturns)
            if (r < mar) { double d = r - mar; sumSq += d * d; count++; }

        if (count == 0) return double.PositiveInfinity;
        double downsideDev = Math.Sqrt(sumSq / count * tradingDays);
        return downsideDev < 1e-14 ? double.NaN : (ret - riskFreeRateAnnual) / downsideDev;
    }

    /// <summary>
    /// Calmar ratio — annualised return divided by maximum drawdown.
    /// </summary>
    public static double CalmarRatio(double[] dailyReturns, int tradingDays = 252)
    {
        double dd = MaxDrawdown(dailyReturns);
        return dd < 1e-14 ? double.NaN : AnnualisedReturn(dailyReturns, tradingDays) / dd;
    }

    /// <summary>
    /// Treynor ratio — excess return per unit of market beta.
    /// Treynor = (AnnReturn − rf) / β
    /// </summary>
    public static double TreynorRatio(
        double[] portfolioReturns, double[] benchmarkReturns,
        double riskFreeRateAnnual, int tradingDays = 252)
    {
        double b = Beta(portfolioReturns, benchmarkReturns);
        return Math.Abs(b) < 1e-14
            ? double.NaN
            : (AnnualisedReturn(portfolioReturns, tradingDays) - riskFreeRateAnnual) / b;
    }

    // ── CAPM risk attribution ─────────────────────────────────────────────────

    /// <summary>
    /// Beta — systematic risk relative to a benchmark.
    /// β = Cov(portfolio, benchmark) / Var(benchmark)
    /// </summary>
    public static double Beta(double[] portfolioReturns, double[] benchmarkReturns)
    {
        if (portfolioReturns.Length != benchmarkReturns.Length)
            throw new ArgumentException("Return series must be the same length.");
        if (portfolioReturns.Length < 2) return double.NaN;

        double pMean = Mean(portfolioReturns), bMean = Mean(benchmarkReturns);
        double cov = 0, bVar = 0;
        for (int i = 0; i < portfolioReturns.Length; i++)
        {
            cov  += (portfolioReturns[i] - pMean) * (benchmarkReturns[i] - bMean);
            bVar += (benchmarkReturns[i] - bMean) * (benchmarkReturns[i] - bMean);
        }
        return bVar < 1e-14 ? double.NaN : cov / bVar;
    }

    /// <summary>
    /// Jensen's Alpha — abnormal return above CAPM expectation.
    /// α = AnnReturn_p − [rf + β · (AnnReturn_m − rf)]
    /// </summary>
    public static double JensensAlpha(
        double[] portfolioReturns, double[] benchmarkReturns,
        double riskFreeRateAnnual, int tradingDays = 252)
    {
        double b    = Beta(portfolioReturns, benchmarkReturns);
        double retP = AnnualisedReturn(portfolioReturns, tradingDays);
        double retM = AnnualisedReturn(benchmarkReturns, tradingDays);
        return retP - (riskFreeRateAnnual + b * (retM - riskFreeRateAnnual));
    }

    // ── Active management metrics ─────────────────────────────────────────────

    /// <summary>
    /// Tracking error — annualised standard deviation of active returns
    /// (portfolio minus benchmark).
    /// </summary>
    public static double TrackingError(
        double[] portfolioReturns, double[] benchmarkReturns, int tradingDays = 252)
    {
        if (portfolioReturns.Length != benchmarkReturns.Length)
            throw new ArgumentException("Return series must be the same length.");
        var active = new double[portfolioReturns.Length];
        for (int i = 0; i < active.Length; i++)
            active[i] = portfolioReturns[i] - benchmarkReturns[i];
        return StdDev(active) * Math.Sqrt(tradingDays);
    }

    /// <summary>
    /// Information ratio — active return per unit of tracking error.
    /// IR = (AnnReturn_p − AnnReturn_m) / TrackingError
    /// </summary>
    public static double InformationRatio(
        double[] portfolioReturns, double[] benchmarkReturns, int tradingDays = 252)
    {
        double te = TrackingError(portfolioReturns, benchmarkReturns, tradingDays);
        if (te < 1e-14) return double.NaN;
        return (AnnualisedReturn(portfolioReturns, tradingDays)
              - AnnualisedReturn(benchmarkReturns, tradingDays)) / te;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static double Mean(double[] r)
    {
        double s = 0;
        foreach (double x in r) s += x;
        return s / r.Length;
    }

    private static double StdDev(double[] r)
    {
        if (r.Length < 2) return 0;
        double mu  = Mean(r);
        double sum = 0;
        foreach (double x in r) sum += (x - mu) * (x - mu);
        return Math.Sqrt(sum / (r.Length - 1));
    }

    private static double NormalQuantile(double p)
    {
        if (p <= 0) return double.NegativeInfinity;
        if (p >= 1) return double.PositiveInfinity;
        const double a0 = 2.515517, a1 = 0.802853, a2 = 0.010328;
        const double b1 = 1.432788, b2 = 0.189269, b3 = 0.001308;
        bool lower = p < 0.5;
        double t   = lower ? Math.Sqrt(-2 * Math.Log(p)) : Math.Sqrt(-2 * Math.Log(1 - p));
        double z   = t - (a0 + t * (a1 + t * a2)) / (1 + t * (b1 + t * (b2 + t * b3)));
        return lower ? -z : z;
    }
}
