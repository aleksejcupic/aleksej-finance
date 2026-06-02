using System;
using System.Linq;

namespace AleksejCupic.FinancialMath.Risk;

/// <summary>
/// Time-varying volatility models used in risk management.
/// Includes EWMA (RiskMetrics) and GARCH(1,1) volatility estimation.
/// Based on Hull Chapter 23 and JP Morgan's RiskMetrics methodology.
/// </summary>
public static class VolatilityModels
{
    // ── EWMA (Exponentially Weighted Moving Average) ──────────────────────────
    //
    // RiskMetrics model: σ²_t = λ · σ²_{t-1} + (1 − λ) · r²_{t-1}
    // Recommended λ = 0.94 for daily data (JP Morgan RiskMetrics).

    /// <summary>
    /// Computes the EWMA (RiskMetrics) volatility estimate for a series of daily returns.
    /// The last element is the most recent estimate.
    /// σ²_t = λ · σ²_{t-1} + (1 − λ) · r²_{t-1}
    /// </summary>
    /// <param name="dailyReturns">Daily returns in chronological order (r_t = 0.01 means +1%)</param>
    /// <param name="lambda">Decay factor (default 0.94, the RiskMetrics standard for daily data)</param>
    /// <returns>Array of daily volatility estimates (annualised), same length as returns</returns>
    public static double[] EwmaVolatility(double[] dailyReturns, double lambda = 0.94)
    {
        if (dailyReturns.Length < 2)
            throw new ArgumentException("At least 2 returns are required.");

        int n      = dailyReturns.Length;
        var variances = new double[n];

        // Initialise with sample variance of first 30 (or all) observations
        int warmup = Math.Min(30, n);
        double mean = 0;
        for (int i = 0; i < warmup; i++) mean += dailyReturns[i];
        mean /= warmup;
        double v0 = 0;
        for (int i = 0; i < warmup; i++) v0 += (dailyReturns[i] - mean) * (dailyReturns[i] - mean);
        variances[0] = v0 / warmup;

        for (int t = 1; t < n; t++)
            variances[t] = lambda * variances[t - 1] + (1 - lambda) * dailyReturns[t - 1] * dailyReturns[t - 1];

        // Annualise and take square root
        var result = new double[n];
        for (int t = 0; t < n; t++)
            result[t] = Math.Sqrt(variances[t] * 252);
        return result;
    }

    /// <summary>
    /// Most recent EWMA volatility estimate (annualised) from a return series.
    /// </summary>
    public static double EwmaVolatilityLatest(double[] dailyReturns, double lambda = 0.94)
    {
        var series = EwmaVolatility(dailyReturns, lambda);
        return series[^1];
    }

    // ── GARCH(1,1) ────────────────────────────────────────────────────────────
    //
    // σ²_t = ω + α · ε²_{t-1} + β · σ²_{t-1}
    //
    // ω = constant term (related to long-run variance: V_L = ω/(1−α−β))
    // α = weight on last squared return (ARCH term)
    // β = weight on last variance (GARCH term)
    // Stationarity condition: α + β < 1

    /// <summary>
    /// Computes GARCH(1,1) conditional variance series given parameters.
    /// σ²_t = ω + α · r²_{t-1} + β · σ²_{t-1}
    /// </summary>
    /// <param name="dailyReturns">Daily returns in chronological order</param>
    /// <param name="omega">Constant term ω (&gt; 0)</param>
    /// <param name="alpha">ARCH coefficient α (α &gt; 0)</param>
    /// <param name="beta">GARCH coefficient β (β &gt; 0, α + β &lt; 1 for stationarity)</param>
    /// <returns>Array of annualised volatility estimates (same length as returns)</returns>
    public static double[] GarchVolatility(
        double[] dailyReturns, double omega, double alpha, double beta)
    {
        if (dailyReturns.Length < 2)
            throw new ArgumentException("At least 2 returns are required.");
        if (alpha + beta >= 1)
            throw new ArgumentException("alpha + beta must be less than 1 for GARCH stationarity.");

        int n          = dailyReturns.Length;
        var variances  = new double[n];
        double vLong   = omega / (1 - alpha - beta); // long-run variance
        variances[0]   = vLong;

        for (int t = 1; t < n; t++)
            variances[t] = omega
                         + alpha * dailyReturns[t - 1] * dailyReturns[t - 1]
                         + beta  * variances[t - 1];

        var result = new double[n];
        for (int t = 0; t < n; t++)
            result[t] = Math.Sqrt(variances[t] * 252);
        return result;
    }

    /// <summary>
    /// Long-run (unconditional) variance implied by GARCH(1,1) parameters.
    /// V_L = ω / (1 − α − β)
    /// </summary>
    public static double GarchLongRunVariance(double omega, double alpha, double beta)
    {
        if (alpha + beta >= 1) throw new ArgumentException("alpha + beta must be less than 1.");
        return omega / (1 - alpha - beta);
    }

    /// <summary>
    /// GARCH(1,1) n-day ahead variance forecast from current conditional variance.
    /// E[σ²_{t+n}] = V_L + (α+β)^n · (σ²_t − V_L)
    /// Mean-reverts to the long-run variance V_L at rate (α+β) per day.
    /// </summary>
    /// <param name="currentVariance">Today's conditional daily variance σ²_t</param>
    /// <param name="omega">ω parameter</param>
    /// <param name="alpha">α parameter</param>
    /// <param name="beta">β parameter</param>
    /// <param name="nDays">Forecast horizon in trading days</param>
    /// <returns>Forecasted annualised volatility n days ahead</returns>
    public static double GarchForecast(
        double currentVariance, double omega, double alpha, double beta, int nDays)
    {
        double vL          = GarchLongRunVariance(omega, alpha, beta);
        double persistence = alpha + beta;
        double forecast    = vL + Math.Pow(persistence, nDays) * (currentVariance - vL);
        return Math.Sqrt(Math.Max(forecast, 0) * 252);
    }

    // ── Rolling historical volatility ─────────────────────────────────────────

    /// <summary>
    /// Rolling historical volatility (annualised) using a fixed lookback window.
    /// Each element i is the annualised standard deviation of returns[i−window+1 .. i].
    /// Elements before the window is full are NaN.
    /// </summary>
    /// <param name="dailyReturns">Daily returns in chronological order</param>
    /// <param name="window">Lookback window in trading days (default 21 ≈ 1 month)</param>
    /// <param name="tradingDays">Trading days per year for annualisation (default 252)</param>
    public static double[] RollingVolatility(
        double[] dailyReturns, int window = 21, int tradingDays = 252)
    {
        if (window < 2) throw new ArgumentOutOfRangeException(nameof(window), "window must be >= 2");
        int n      = dailyReturns.Length;
        var result = new double[n];

        for (int i = 0; i < window - 1; i++) result[i] = double.NaN;

        for (int i = window - 1; i < n; i++)
        {
            double mean = 0;
            for (int j = i - window + 1; j <= i; j++) mean += dailyReturns[j];
            mean /= window;

            double var_ = 0;
            for (int j = i - window + 1; j <= i; j++)
                var_ += (dailyReturns[j] - mean) * (dailyReturns[j] - mean);
            result[i] = Math.Sqrt(var_ / (window - 1) * tradingDays);
        }
        return result;
    }

    // ── GARCH MLE parameter estimation ───────────────────────────────────────

    /// <summary>
    /// Estimates GARCH(1,1) parameters (ω, α, β) by maximum likelihood.
    /// Uses grid search over a coarse parameter space followed by gradient descent.
    /// This is a simplified estimator — for production use a dedicated optimiser.
    /// </summary>
    /// <param name="dailyReturns">Daily returns in chronological order (at least 100 recommended)</param>
    /// <param name="maxIter">Gradient ascent iterations (default 500)</param>
    /// <returns>(omega, alpha, beta) — the MLE GARCH(1,1) parameter estimates</returns>
    public static (double omega, double alpha, double beta) GarchMle(
        double[] dailyReturns, int maxIter = 500)
    {
        if (dailyReturns.Length < 30)
            throw new ArgumentException("At least 30 returns are required for GARCH estimation.");

        double sampleVar = SampleVariance(dailyReturns);

        // Initial guess: ω = V_L·γ, α = 0.05, β = 0.90 (γ=0.05, persistence=0.95)
        double alpha = 0.05, beta = 0.90;
        double omega = sampleVar * (1 - alpha - beta);

        double lr = 1e-6;
        double bestLl = GarchLogLikelihood(dailyReturns, omega, alpha, beta);

        for (int iter = 0; iter < maxIter; iter++)
        {
            // Numerical gradient (central difference)
            double eps  = 1e-7;
            double dOm  = (GarchLogLikelihood(dailyReturns, omega + eps, alpha, beta)
                         - GarchLogLikelihood(dailyReturns, omega - eps, alpha, beta)) / (2 * eps);
            double dAlp = (GarchLogLikelihood(dailyReturns, omega, alpha + eps, beta)
                         - GarchLogLikelihood(dailyReturns, omega, alpha - eps, beta)) / (2 * eps);
            double dBet = (GarchLogLikelihood(dailyReturns, omega, alpha, beta + eps)
                         - GarchLogLikelihood(dailyReturns, omega, alpha, beta - eps)) / (2 * eps);

            double oNew = omega + lr * dOm;
            double aNew = alpha + lr * dAlp;
            double bNew = beta  + lr * dBet;

            // Enforce constraints: ω > 0, α > 0, β > 0, α+β < 0.999
            if (oNew <= 0 || aNew <= 0 || bNew <= 0 || aNew + bNew >= 0.999) continue;

            double ll = GarchLogLikelihood(dailyReturns, oNew, aNew, bNew);
            if (ll > bestLl) { omega = oNew; alpha = aNew; beta = bNew; bestLl = ll; }
        }
        return (omega, alpha, beta);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    // GARCH(1,1) log-likelihood (Gaussian innovations)
    private static double GarchLogLikelihood(
        double[] r, double omega, double alpha, double beta)
    {
        if (omega <= 0 || alpha <= 0 || beta <= 0 || alpha + beta >= 1) return double.NegativeInfinity;

        int n         = r.Length;
        double vL     = omega / (1 - alpha - beta);
        double var_t  = vL;
        double ll     = 0;

        for (int t = 1; t < n; t++)
        {
            var_t  = omega + alpha * r[t - 1] * r[t - 1] + beta * var_t;
            if (var_t <= 0) return double.NegativeInfinity;
            ll    -= 0.5 * (Math.Log(var_t) + r[t] * r[t] / var_t);
        }
        return ll;
    }

    private static double SampleVariance(double[] r)
    {
        double mean = 0;
        foreach (double x in r) mean += x;
        mean /= r.Length;
        double v = 0;
        foreach (double x in r) v += (x - mean) * (x - mean);
        return v / (r.Length - 1);
    }
}
