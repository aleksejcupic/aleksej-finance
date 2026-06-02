using System;

namespace AleksejCupic.FinancialMath.Options;

/// <summary>
/// Black-Scholes (1973) analytical pricing and Greeks for European options.
/// Assumes constant volatility, log-normal asset prices, and no dividends.
/// </summary>
public static class BlackScholes
{
    // ── Pricing ───────────────────────────────────────────────────────────────

    /// <summary>European put price.</summary>
    /// <param name="s">Current asset price</param>
    /// <param name="k">Strike price</param>
    /// <param name="t">Time to expiry in years</param>
    /// <param name="r">Continuous risk-free rate</param>
    /// <param name="sigma">Annualised volatility</param>
    public static double Put(double s, double k, double t, double r, double sigma)
    {
        if (t <= 0) return Math.Max(k - s, 0);
        var (d1, d2) = D1D2(s, k, t, r, sigma);
        return k * Math.Exp(-r * t) * N(-d2) - s * N(-d1);
    }

    /// <summary>European call price.</summary>
    public static double Call(double s, double k, double t, double r, double sigma)
    {
        if (t <= 0) return Math.Max(s - k, 0);
        var (d1, d2) = D1D2(s, k, t, r, sigma);
        return s * N(d1) - k * Math.Exp(-r * t) * N(d2);
    }

    // ── Greeks ────────────────────────────────────────────────────────────────

    /// <summary>Delta — sensitivity of option price to asset price (dV/dS).</summary>
    public static double Delta(double s, double k, double t, double r, double sigma, bool isPut)
    {
        if (t <= 0) return 0;
        var (d1, _) = D1D2(s, k, t, r, sigma);
        return isPut ? N(d1) - 1 : N(d1);
    }

    /// <summary>Gamma — rate of change of delta (d²V/dS²). Same for puts and calls.</summary>
    public static double Gamma(double s, double k, double t, double r, double sigma)
    {
        if (t <= 0) return 0;
        var (d1, _) = D1D2(s, k, t, r, sigma);
        return Npdf(d1) / (s * sigma * Math.Sqrt(t));
    }

    /// <summary>Vega — sensitivity to volatility per 1% move in sigma (dV/dσ / 100).</summary>
    public static double Vega(double s, double k, double t, double r, double sigma)
    {
        if (t <= 0) return 0;
        var (d1, _) = D1D2(s, k, t, r, sigma);
        return s * Npdf(d1) * Math.Sqrt(t) / 100;
    }

    /// <summary>Theta — daily time decay (dV/dT / 365).</summary>
    public static double Theta(double s, double k, double t, double r, double sigma, bool isPut)
    {
        if (t <= 0) return 0;
        var (d1, d2) = D1D2(s, k, t, r, sigma);
        double decay = -s * Npdf(d1) * sigma / (2 * Math.Sqrt(t));
        if (isPut)
            return (decay + r * k * Math.Exp(-r * t) * N(-d2)) / 365;
        return (decay - r * k * Math.Exp(-r * t) * N(d2)) / 365;
    }

    /// <summary>Rho — sensitivity to risk-free rate per 1% move in r (dV/dr / 100).</summary>
    public static double Rho(double s, double k, double t, double r, double sigma, bool isPut)
    {
        if (t <= 0) return 0;
        var (_, d2) = D1D2(s, k, t, r, sigma);
        if (isPut)
            return -k * t * Math.Exp(-r * t) * N(-d2) / 100;
        return k * t * Math.Exp(-r * t) * N(d2) / 100;
    }

    // ── Higher-Order Greeks ───────────────────────────────────────────────────

    /// <summary>
    /// Vanna — second-order sensitivity of option value to both spot and volatility (∂²V/∂S∂σ).
    /// Equivalently: the rate of change of delta with respect to volatility.
    /// Same value for puts and calls.
    /// </summary>
    public static double Vanna(double s, double k, double t, double r, double sigma)
    {
        if (t <= 0) return 0;
        var (d1, d2) = D1D2(s, k, t, r, sigma);
        return -Npdf(d1) * d2 / sigma;
    }

    /// <summary>
    /// Charm — rate of change of delta with respect to calendar time (∂Delta/∂t), i.e. daily delta bleed.
    /// Positive for ITM options (delta drifts toward ±1), negative for OTM (drifts toward 0).
    /// Same formula for puts and calls. Scaled per calendar day.
    /// </summary>
    public static double Charm(double s, double k, double t, double r, double sigma)
    {
        if (t <= 0) return 0;
        var (d1, d2) = D1D2(s, k, t, r, sigma);
        double sqrtT = Math.Sqrt(t);
        return -Npdf(d1) * (2 * r * t - d2 * sigma * sqrtT)
                         / (2 * t * sigma * sqrtT) / 365;
    }

    /// <summary>
    /// Volga (Vomma) — second-order sensitivity of option value to volatility (∂²V/∂σ²)
    /// per 1% move in volatility. Same for puts and calls.
    /// </summary>
    public static double Volga(double s, double k, double t, double r, double sigma)
    {
        if (t <= 0) return 0;
        var (d1, d2) = D1D2(s, k, t, r, sigma);
        return s * Npdf(d1) * Math.Sqrt(t) * d1 * d2 / sigma / 100;
    }

    /// <summary>
    /// Speed — rate of change of gamma with respect to asset price (∂Γ/∂S).
    /// </summary>
    public static double Speed(double s, double k, double t, double r, double sigma)
    {
        if (t <= 0) return 0;
        var (d1, _) = D1D2(s, k, t, r, sigma);
        return -Gamma(s, k, t, r, sigma) * (d1 / (sigma * Math.Sqrt(t)) + 1) / s;
    }

    /// <summary>
    /// Zomma — rate of change of gamma with respect to volatility (∂Γ/∂σ).
    /// </summary>
    public static double Zomma(double s, double k, double t, double r, double sigma)
    {
        if (t <= 0) return 0;
        var (d1, d2) = D1D2(s, k, t, r, sigma);
        return Gamma(s, k, t, r, sigma) * (d1 * d2 - 1) / sigma;
    }

    // ── Implied volatility ────────────────────────────────────────────────────

    /// <summary>
    /// Implied volatility via Brent's method. Returns NaN if no solution found.
    /// </summary>
    public static double ImpliedVolatility(
        double marketPrice, double s, double k, double t, double r, bool isPut,
        double tol = 1e-6, int maxIter = 200)
    {
        Func<double, double> f = vol =>
            (isPut ? Put(s, k, t, r, vol) : Call(s, k, t, r, vol)) - marketPrice;

        double lo = 1e-6, hi = 10.0;
        if (Math.Sign(f(lo)) == Math.Sign(f(hi))) return double.NaN;

        for (int i = 0; i < maxIter; i++)
        {
            double mid = (lo + hi) / 2;
            if (hi - lo < tol) return mid;
            if (Math.Sign(f(mid)) == Math.Sign(f(lo))) lo = mid; else hi = mid;
        }
        return (lo + hi) / 2;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static (double d1, double d2) D1D2(double s, double k, double t, double r, double sigma)
    {
        double d1 = (Math.Log(s / k) + (r + 0.5 * sigma * sigma) * t) / (sigma * Math.Sqrt(t));
        return (d1, d1 - sigma * Math.Sqrt(t));
    }

    // Standard normal CDF via Abramowitz & Stegun approximation
    private static double N(double x)
    {
        const double a1 =  0.254829592, a2 = -0.284496736, a3 =  1.421413741;
        const double a4 = -1.453152027, a5 =  1.061405429, p  =  0.3275911;
        double sign = x < 0 ? -1 : 1;
        // N(x) = 0.5 * (1 + erf(x / sqrt(2))); scale x so the A&S erf approximation gets x/sqrt(2).
        x = Math.Abs(x) / Math.Sqrt(2.0);
        double t = 1.0 / (1.0 + p * x);
        double poly = t * (a1 + t * (a2 + t * (a3 + t * (a4 + t * a5))));
        return 0.5 * (1.0 + sign * (1.0 - poly * Math.Exp(-x * x)));
    }

    private static double Npdf(double x) =>
        Math.Exp(-0.5 * x * x) / Math.Sqrt(2 * Math.PI);
}
