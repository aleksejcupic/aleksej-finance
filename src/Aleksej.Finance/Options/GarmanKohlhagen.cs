using System;

namespace Aleksej.Finance.Options;

/// <summary>
/// Garman-Kohlhagen (1983) model for pricing European FX options.
/// Extends Black-Scholes by replacing the dividend yield with a foreign risk-free rate rf.
/// When rf = 0 this reduces exactly to Black-Scholes.
/// </summary>
public static class GarmanKohlhagen
{
    // ── Pricing ───────────────────────────────────────────────────────────────

    /// <summary>
    /// European FX call option price.
    /// C = S · exp(−rf · T) · N(d1) − K · exp(−r · T) · N(d2)
    /// </summary>
    /// <param name="s">Spot exchange rate (domestic per unit of foreign)</param>
    /// <param name="k">Strike exchange rate</param>
    /// <param name="t">Time to expiry in years</param>
    /// <param name="r">Domestic continuous risk-free rate</param>
    /// <param name="rf">Foreign continuous risk-free rate</param>
    /// <param name="sigma">Annualised volatility of the exchange rate</param>
    public static double Call(double s, double k, double t, double r, double rf, double sigma)
    {
        if (t <= 0) return Math.Max(s - k, 0);
        var (d1, d2) = D1D2(s, k, t, r, rf, sigma);
        return s * Math.Exp(-rf * t) * N(d1) - k * Math.Exp(-r * t) * N(d2);
    }

    /// <summary>
    /// European FX put option price.
    /// P = K · exp(−r · T) · N(−d2) − S · exp(−rf · T) · N(−d1)
    /// </summary>
    public static double Put(double s, double k, double t, double r, double rf, double sigma)
    {
        if (t <= 0) return Math.Max(k - s, 0);
        var (d1, d2) = D1D2(s, k, t, r, rf, sigma);
        return k * Math.Exp(-r * t) * N(-d2) - s * Math.Exp(-rf * t) * N(-d1);
    }

    // ── Greeks ────────────────────────────────────────────────────────────────

    /// <summary>Delta — dV/dS.</summary>
    public static double Delta(double s, double k, double t, double r, double rf, double sigma, bool isPut)
    {
        if (t <= 0) return 0;
        var (d1, _) = D1D2(s, k, t, r, rf, sigma);
        double factor = Math.Exp(-rf * t);
        return isPut ? factor * (N(d1) - 1) : factor * N(d1);
    }

    /// <summary>Gamma — d²V/dS². Same for puts and calls.</summary>
    public static double Gamma(double s, double k, double t, double r, double rf, double sigma)
    {
        if (t <= 0) return 0;
        var (d1, _) = D1D2(s, k, t, r, rf, sigma);
        return Math.Exp(-rf * t) * Npdf(d1) / (s * sigma * Math.Sqrt(t));
    }

    /// <summary>Vega — dV/dσ per 1% move in volatility. Same for puts and calls.</summary>
    public static double Vega(double s, double k, double t, double r, double rf, double sigma)
    {
        if (t <= 0) return 0;
        var (d1, _) = D1D2(s, k, t, r, rf, sigma);
        return s * Math.Exp(-rf * t) * Npdf(d1) * Math.Sqrt(t) / 100;
    }

    /// <summary>Theta — daily time decay (dV/dt per calendar day).</summary>
    public static double Theta(double s, double k, double t, double r, double rf, double sigma, bool isPut)
    {
        if (t <= 0) return 0;
        var (d1, d2) = D1D2(s, k, t, r, rf, sigma);
        double decay = -s * Math.Exp(-rf * t) * Npdf(d1) * sigma / (2 * Math.Sqrt(t));
        if (isPut)
            return (decay - rf * s * Math.Exp(-rf * t) * N(-d1) + r * k * Math.Exp(-r * t) * N(-d2)) / 365;
        return (decay + rf * s * Math.Exp(-rf * t) * N(d1) - r * k * Math.Exp(-r * t) * N(d2)) / 365;
    }

    /// <summary>Rho — sensitivity to domestic risk-free rate per 1% move in r (dV/dr / 100).</summary>
    public static double Rho(double s, double k, double t, double r, double rf, double sigma, bool isPut)
    {
        if (t <= 0) return 0;
        var (_, d2) = D1D2(s, k, t, r, rf, sigma);
        if (isPut)
            return -k * t * Math.Exp(-r * t) * N(-d2) / 100;
        return k * t * Math.Exp(-r * t) * N(d2) / 100;
    }

    /// <summary>Rho (foreign) — sensitivity to foreign risk-free rate per 1% move in rf (dV/drf / 100).</summary>
    public static double RhoForeign(double s, double k, double t, double r, double rf, double sigma, bool isPut)
    {
        if (t <= 0) return 0;
        var (d1, _) = D1D2(s, k, t, r, rf, sigma);
        if (isPut)
            return s * t * Math.Exp(-rf * t) * N(-d1) / 100;
        return -s * t * Math.Exp(-rf * t) * N(d1) / 100;
    }

    // ── Implied volatility ────────────────────────────────────────────────────

    /// <summary>
    /// Implied volatility via bisection. Returns NaN if no solution is found.
    /// </summary>
    public static double ImpliedVolatility(
        double marketPrice, double s, double k, double t, double r, double rf, bool isPut,
        double tol = 1e-6, int maxIter = 200)
    {
        Func<double, double> f = vol =>
            (isPut ? Put(s, k, t, r, rf, vol) : Call(s, k, t, r, rf, vol)) - marketPrice;

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

    private static (double d1, double d2) D1D2(
        double s, double k, double t, double r, double rf, double sigma)
    {
        double d1 = (Math.Log(s / k) + (r - rf + 0.5 * sigma * sigma) * t) / (sigma * Math.Sqrt(t));
        return (d1, d1 - sigma * Math.Sqrt(t));
    }

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
