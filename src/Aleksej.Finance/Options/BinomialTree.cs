using System;

namespace Aleksej.Finance.Options;

/// <summary>
/// Cox-Ross-Rubinstein (CRR) binomial tree for pricing European and American options.
/// Converges to Black-Scholes as steps → ∞ for European options.
/// Supports early exercise (American options) via backward induction.
/// Based on Hull Chapter 21.
/// </summary>
public static class BinomialTree
{
    // ── Pricing ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Option price via the CRR binomial tree.
    /// </summary>
    /// <param name="s">Current asset price</param>
    /// <param name="k">Strike price</param>
    /// <param name="t">Time to expiry in years</param>
    /// <param name="r">Continuous risk-free rate</param>
    /// <param name="sigma">Annualised volatility</param>
    /// <param name="steps">Number of time steps (higher = more accurate, default 200)</param>
    /// <param name="isPut">True for put, false for call</param>
    /// <param name="isAmerican">True for American (early exercise), false for European</param>
    public static double Price(
        double s, double k, double t, double r, double sigma,
        int steps = 200, bool isPut = false, bool isAmerican = false)
    {
        if (t <= 0) return isPut ? Math.Max(k - s, 0) : Math.Max(s - k, 0);
        if (steps < 1) throw new ArgumentOutOfRangeException(nameof(steps), "steps must be >= 1");

        double dt   = t / steps;
        double u    = Math.Exp(sigma * Math.Sqrt(dt));
        double d    = 1.0 / u;
        double disc = Math.Exp(-r * dt);
        double p    = (Math.Exp(r * dt) - d) / (u - d);
        double q    = 1.0 - p;

        // Terminal asset prices and option values (single space-optimised array)
        var v = new double[steps + 1];
        for (int j = 0; j <= steps; j++)
        {
            double spot = s * Math.Pow(u, steps - 2 * j);
            v[j] = isPut ? Math.Max(k - spot, 0) : Math.Max(spot - k, 0);
        }

        // Backward induction
        for (int i = steps - 1; i >= 0; i--)
        {
            for (int j = 0; j <= i; j++)
            {
                double cont = disc * (p * v[j] + q * v[j + 1]);
                if (isAmerican)
                {
                    double spot = s * Math.Pow(u, i - 2 * j);
                    double intrinsic = isPut ? Math.Max(k - spot, 0) : Math.Max(spot - k, 0);
                    v[j] = Math.Max(intrinsic, cont);
                }
                else
                {
                    v[j] = cont;
                }
            }
        }
        return v[0];
    }

    // ── Greeks ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Delta — first-order price sensitivity to the underlying asset (dV/dS).
    /// Computed from the two level-1 nodes of the tree.
    /// </summary>
    public static double Delta(
        double s, double k, double t, double r, double sigma,
        int steps = 200, bool isPut = false, bool isAmerican = false)
    {
        if (t <= 0) return 0;
        var (vu, vd, su, sd) = Level1Nodes(s, k, t, r, sigma, steps, isPut, isAmerican);
        return (vu - vd) / (su - sd);
    }

    /// <summary>
    /// Gamma — second-order price sensitivity (d²V/dS²).
    /// Computed from the three level-2 tree nodes (all at the same time slice), per Hull.
    /// </summary>
    public static double Gamma(
        double s, double k, double t, double r, double sigma,
        int steps = 200, bool isPut = false, bool isAmerican = false)
    {
        if (t <= 0) return 0;
        if (steps < 2) throw new ArgumentOutOfRangeException(nameof(steps), "steps must be >= 2 for gamma");

        double dt   = t / steps;
        double u    = Math.Exp(sigma * Math.Sqrt(dt));
        double d    = 1.0 / u;
        double tRem = t - 2 * dt;
        int    nRem = steps - 2;

        // Three nodes at time 2*dt: S*u^2, S (= S*u*d), S*d^2.
        double suu = s * u * u, sud = s, sdd = s * d * d;
        double vuu = Price(suu, k, tRem, r, sigma, nRem, isPut, isAmerican);
        double vud = Price(sud, k, tRem, r, sigma, nRem, isPut, isAmerican);
        double vdd = Price(sdd, k, tRem, r, sigma, nRem, isPut, isAmerican);

        double gammaUp   = (vuu - vud) / (suu - sud);
        double gammaDown = (vud - vdd) / (sud - sdd);
        return (gammaUp - gammaDown) / (0.5 * (suu - sdd));
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    // Returns the option values and asset prices at the two level-1 tree nodes.
    private static (double vu, double vd, double su, double sd) Level1Nodes(
        double s, double k, double t, double r, double sigma,
        int steps, bool isPut, bool isAmerican)
    {
        double dt = t / steps;
        double u  = Math.Exp(sigma * Math.Sqrt(dt));
        double d  = 1.0 / u;
        return (
            Price(s * u, k, t - dt, r, sigma, steps - 1, isPut, isAmerican),
            Price(s * d, k, t - dt, r, sigma, steps - 1, isPut, isAmerican),
            s * u,
            s * d
        );
    }
}
