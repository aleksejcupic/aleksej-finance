using System;

namespace Aleksej.Finance.Options;

/// <summary>
/// Options on futures contracts priced via Black's (1976) model.
/// The underlying is a futures (or forward) price F, not a spot price.
/// Since a futures contract costs nothing to enter, Black's model discounts the
/// full payoff: C = e^{−rT} · [F·N(d1) − K·N(d2)].
/// When the futures price equals the forward price this is also called the
/// "Black forward model" and is the standard for fixed-income options.
/// Based on Hull Chapter 18.
/// </summary>
public static class OptionsOnFutures
{
    // ── Pricing ───────────────────────────────────────────────────────────────

    /// <summary>
    /// European call option on a futures contract (Black 1976).
    /// C = e^{−rT} · [F·N(d1) − K·N(d2)]
    /// </summary>
    /// <param name="f">Current futures price</param>
    /// <param name="k">Strike price</param>
    /// <param name="t">Time to option expiry in years</param>
    /// <param name="r">Continuously compounded risk-free rate</param>
    /// <param name="sigma">Annualised volatility of the futures price</param>
    public static double Call(double f, double k, double t, double r, double sigma)
    {
        if (t <= 0) return Math.Max(f - k, 0);
        var (d1, d2) = D1D2(f, k, t, sigma);
        return Math.Exp(-r * t) * (f * N(d1) - k * N(d2));
    }

    /// <summary>
    /// European put option on a futures contract (Black 1976).
    /// P = e^{−rT} · [K·N(−d2) − F·N(−d1)]
    /// </summary>
    public static double Put(double f, double k, double t, double r, double sigma)
    {
        if (t <= 0) return Math.Max(k - f, 0);
        var (d1, d2) = D1D2(f, k, t, sigma);
        return Math.Exp(-r * t) * (k * N(-d2) - f * N(-d1));
    }

    // ── Put-call parity ───────────────────────────────────────────────────────

    /// <summary>
    /// Put-call parity for futures options.
    /// C − P = e^{−rT} · (F − K)
    /// Returns the call price derived from a known put price (or vice versa).
    /// </summary>
    /// <param name="putPrice">Known put price</param>
    /// <param name="f">Futures price</param>
    /// <param name="k">Strike</param>
    /// <param name="t">Time to expiry</param>
    /// <param name="r">Risk-free rate</param>
    public static double CallFromPut(double putPrice, double f, double k, double t, double r) =>
        putPrice + Math.Exp(-r * t) * (f - k);

    /// <summary>Put price derived from a known call price via put-call parity.</summary>
    public static double PutFromCall(double callPrice, double f, double k, double t, double r) =>
        callPrice - Math.Exp(-r * t) * (f - k);

    // ── Greeks ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Delta — dV/dF (change in option value per unit change in futures price).
    /// Call delta: e^{−rT} · N(d1)
    /// Put delta:  e^{−rT} · (N(d1) − 1)
    /// </summary>
    public static double Delta(double f, double k, double t, double r, double sigma, bool isPut)
    {
        if (t <= 0) return 0;
        var (d1, _) = D1D2(f, k, t, sigma);
        double disc = Math.Exp(-r * t);
        return isPut ? disc * (N(d1) - 1) : disc * N(d1);
    }

    /// <summary>
    /// Gamma — d²V/dF². Same for puts and calls.
    /// Γ = e^{−rT} · N'(d1) / (F · σ · √T)
    /// </summary>
    public static double Gamma(double f, double k, double t, double r, double sigma)
    {
        if (t <= 0) return 0;
        var (d1, _) = D1D2(f, k, t, sigma);
        return Math.Exp(-r * t) * Npdf(d1) / (f * sigma * Math.Sqrt(t));
    }

    /// <summary>
    /// Vega — dV/dσ per 1% move in volatility. Same for puts and calls.
    /// V = e^{−rT} · F · N'(d1) · √T / 100
    /// </summary>
    public static double Vega(double f, double k, double t, double r, double sigma)
    {
        if (t <= 0) return 0;
        var (d1, _) = D1D2(f, k, t, sigma);
        return Math.Exp(-r * t) * f * Npdf(d1) * Math.Sqrt(t) / 100;
    }

    /// <summary>
    /// Theta — daily time decay (dV/dt per calendar day).
    /// Includes both the time-decay of the option and the carry on the discount factor.
    /// </summary>
    public static double Theta(double f, double k, double t, double r, double sigma, bool isPut)
    {
        if (t <= 0) return 0;
        var (d1, d2) = D1D2(f, k, t, sigma);
        double disc  = Math.Exp(-r * t);
        double decay = -disc * f * Npdf(d1) * sigma / (2 * Math.Sqrt(t));
        if (isPut)
            return (decay + r * disc * (k * N(-d2) - f * N(-d1))) / 365;
        return (decay + r * disc * (f * N(d1) - k * N(d2))) / 365;
    }

    /// <summary>
    /// Rho — sensitivity to the risk-free rate per 1% move in r (dV/dr / 100).
    /// For futures options, rho is just the option price scaled by −T (discount effect only).
    /// Call rho: −T · C / 100
    /// Put rho:  −T · P / 100
    /// </summary>
    public static double Rho(double f, double k, double t, double r, double sigma, bool isPut)
    {
        if (t <= 0) return 0;
        double price = isPut ? Put(f, k, t, r, sigma) : Call(f, k, t, r, sigma);
        return -t * price / 100;
    }

    // ── Implied volatility ────────────────────────────────────────────────────

    /// <summary>
    /// Implied volatility of a futures option via bisection. Returns NaN if no solution found.
    /// </summary>
    public static double ImpliedVolatility(
        double marketPrice, double f, double k, double t, double r, bool isPut,
        double tol = 1e-6, int maxIter = 200)
    {
        Func<double, double> fn = vol =>
            (isPut ? Put(f, k, t, r, vol) : Call(f, k, t, r, vol)) - marketPrice;

        double lo = 1e-6, hi = 10.0;
        if (Math.Sign(fn(lo)) == Math.Sign(fn(hi))) return double.NaN;

        for (int i = 0; i < maxIter; i++)
        {
            double mid = (lo + hi) / 2;
            if (hi - lo < tol) return mid;
            if (Math.Sign(fn(mid)) == Math.Sign(fn(lo))) lo = mid; else hi = mid;
        }
        return (lo + hi) / 2;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    // Black 1976 d1/d2 — uses futures price F, no cost-of-carry term.
    private static (double d1, double d2) D1D2(double f, double k, double t, double sigma)
    {
        double d1 = (Math.Log(f / k) + 0.5 * sigma * sigma * t) / (sigma * Math.Sqrt(t));
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
