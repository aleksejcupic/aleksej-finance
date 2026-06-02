using System;

namespace Aleksej.Finance.Options;

/// <summary>
/// Closed-form and Monte Carlo prices for exotic options.
/// Includes binary (digital), European barrier, Asian (geometric), and lookback options.
/// Based on Hull Chapters 26–27 and Haug's "Complete Guide to Option Pricing Formulas."
/// </summary>
public static class ExoticOptions
{
    // ── Binary / Digital options ──────────────────────────────────────────────

    /// <summary>
    /// Cash-or-nothing binary option.
    /// Pays a fixed cash amount Q if the option expires in-the-money, otherwise zero.
    /// Call: Q · exp(−rT) · N(d2)
    /// Put:  Q · exp(−rT) · N(−d2)
    /// </summary>
    /// <param name="s">Current asset price</param>
    /// <param name="k">Strike price</param>
    /// <param name="t">Time to expiry in years</param>
    /// <param name="r">Continuous risk-free rate</param>
    /// <param name="sigma">Annualised volatility</param>
    /// <param name="cashPayoff">Fixed cash amount paid if in-the-money (default 1.0)</param>
    /// <param name="isPut">True for put (pays if S &lt; K), false for call (pays if S &gt; K)</param>
    public static double CashOrNothing(
        double s, double k, double t, double r, double sigma,
        double cashPayoff = 1.0, bool isPut = false)
    {
        if (t <= 0) return (isPut ? s < k : s > k) ? cashPayoff : 0;
        var (_, d2) = D1D2(s, k, t, r, sigma);
        return cashPayoff * Math.Exp(-r * t) * (isPut ? N(-d2) : N(d2));
    }

    /// <summary>
    /// Asset-or-nothing binary option.
    /// Pays the asset price S_T if the option expires in-the-money, otherwise zero.
    /// Call: S · N(d1)
    /// Put:  S · N(−d1)
    /// </summary>
    public static double AssetOrNothing(
        double s, double k, double t, double r, double sigma, bool isPut = false)
    {
        if (t <= 0) return (isPut ? s < k : s > k) ? s : 0;
        var (d1, _) = D1D2(s, k, t, r, sigma);
        return s * (isPut ? N(-d1) : N(d1));
    }

    // ── Barrier options ───────────────────────────────────────────────────────

    /// <summary>
    /// European barrier call option (closed-form).
    /// A knock-out option expires worthless if S touches the barrier H before expiry.
    /// A knock-in option only becomes active if S touches H.
    /// </summary>
    /// <param name="s">Current asset price</param>
    /// <param name="k">Strike price</param>
    /// <param name="h">Barrier level</param>
    /// <param name="t">Time to expiry in years</param>
    /// <param name="r">Continuous risk-free rate</param>
    /// <param name="sigma">Annualised volatility</param>
    /// <param name="knockIn">True = knock-in; False = knock-out</param>
    /// <param name="isUp">True = up-and-in/out (barrier above S); False = down-and-in/out</param>
    public static double BarrierCall(
        double s, double k, double h, double t, double r, double sigma,
        bool knockIn = false, bool isUp = false)
    {
        if (t <= 0) return Math.Max(s - k, 0);
        return BarrierPrice(s, k, h, t, r, sigma, phi: 1, eta: isUp ? -1 : 1, knockIn: knockIn);
    }

    /// <summary>
    /// European barrier put option (closed-form).
    /// </summary>
    public static double BarrierPut(
        double s, double k, double h, double t, double r, double sigma,
        bool knockIn = false, bool isUp = false)
    {
        if (t <= 0) return Math.Max(k - s, 0);
        return BarrierPrice(s, k, h, t, r, sigma, phi: -1, eta: isUp ? -1 : 1, knockIn: knockIn);
    }

    // Reiner-Rubinstein (1991) / Haug standard barrier option formulas (no rebate, cost of carry = r).
    // phi: +1 call / -1 put.  eta: +1 down barrier / -1 up barrier.
    // Knock-in and knock-out prices are computed independently; they sum to the vanilla price.
    private static double BarrierPrice(
        double s, double k, double h, double t, double r, double sigma, int phi, int eta, bool knockIn)
    {
        bool isUp = eta < 0;
        double vanilla = phi > 0 ? BlackScholes.Call(s, k, t, r, sigma) : BlackScholes.Put(s, k, t, r, sigma);

        // Barrier already breached at inception.
        if (!isUp && h >= s) return knockIn ? vanilla : 0.0; // down barrier at/above spot
        if (isUp  && h <= s) return knockIn ? vanilla : 0.0; // up barrier at/below spot

        double mu    = (r - 0.5 * sigma * sigma) / (sigma * sigma);
        double sqrtT = Math.Sqrt(t);
        double sig   = sigma * sqrtT;
        double x1    = Math.Log(s / k) / sig + (1 + mu) * sig;
        double x2    = Math.Log(s / h) / sig + (1 + mu) * sig;
        double y1    = Math.Log(h * h / (s * k)) / sig + (1 + mu) * sig;
        double y2    = Math.Log(h / s) / sig + (1 + mu) * sig;
        double disc  = Math.Exp(-r * t);
        double powP  = Math.Pow(h / s, 2 * (mu + 1));
        double powM  = Math.Pow(h / s, 2 * mu);

        double A = phi * s * N(phi * x1) - phi * k * disc * N(phi * x1 - phi * sig);
        double B = phi * s * N(phi * x2) - phi * k * disc * N(phi * x2 - phi * sig);
        double C = phi * s * powP * N(eta * y1) - phi * k * disc * powM * N(eta * y1 - eta * sig);
        double D = phi * s * powP * N(eta * y2) - phi * k * disc * powM * N(eta * y2 - eta * sig);

        double inPrice, outPrice;
        if (!isUp) // down barrier
        {
            if (phi > 0) // call
            {
                if (k > h) { inPrice = C;            outPrice = A - C; }
                else       { inPrice = A - B + D;    outPrice = B - D; }
            }
            else // put
            {
                if (k > h) { inPrice = B - C + D;    outPrice = A - B + C - D; }
                else       { inPrice = A;            outPrice = 0.0; }
            }
        }
        else // up barrier
        {
            if (phi > 0) // call
            {
                if (k > h) { inPrice = A;            outPrice = 0.0; }
                else       { inPrice = B - C + D;    outPrice = A - B + C - D; }
            }
            else // put
            {
                if (k > h) { inPrice = A - B + D;    outPrice = B - D; }
                else       { inPrice = C;            outPrice = A - C; }
            }
        }

        return knockIn ? inPrice : outPrice;
    }

    // ── Asian options (geometric) ─────────────────────────────────────────────

    /// <summary>
    /// European geometric Asian option (closed-form).
    /// The payoff is based on the geometric average of the asset price over the life of the option.
    /// The geometric average follows log-normal dynamics, so Black-Scholes applies with adjusted parameters.
    /// </summary>
    /// <param name="s">Current asset price</param>
    /// <param name="k">Strike price</param>
    /// <param name="t">Time to expiry in years</param>
    /// <param name="r">Continuous risk-free rate</param>
    /// <param name="sigma">Annualised volatility</param>
    /// <param name="isPut">True for put on geometric average</param>
    public static double GeometricAsian(
        double s, double k, double t, double r, double sigma, bool isPut = false)
    {
        if (t <= 0) return isPut ? Math.Max(k - s, 0) : Math.Max(s - k, 0);

        // Adjusted parameters for geometric average (Kemna-Vorst 1990)
        double sigmaAdj = sigma / Math.Sqrt(3);
        double rAdj     = 0.5 * (r - 0.5 * sigma * sigma) + 0.5 * sigmaAdj * sigmaAdj;
        double bAdj     = rAdj - r; // cost-of-carry adjustment

        // Use Black-Scholes with adjusted vol and carry
        var (d1, d2) = D1D2Adjusted(s, k, t, r, bAdj, sigmaAdj);
        double disc  = Math.Exp(-r * t);
        double carry = Math.Exp(bAdj * t);

        if (isPut)
            return disc * (k * N(-d2) - s * carry * N(-d1));
        return disc * (s * carry * N(d1) - k * N(d2));
    }

    /// <summary>
    /// Arithmetic Asian option price via Monte Carlo simulation.
    /// The payoff is based on the arithmetic average of monitored asset prices.
    /// </summary>
    /// <param name="s">Current asset price</param>
    /// <param name="k">Strike price</param>
    /// <param name="t">Time to expiry in years</param>
    /// <param name="r">Continuous risk-free rate</param>
    /// <param name="sigma">Annualised volatility</param>
    /// <param name="monitoringSteps">Number of price observations (e.g. 252 for daily)</param>
    /// <param name="paths">Number of Monte Carlo paths (default 10 000)</param>
    /// <param name="isPut">True for put on arithmetic average</param>
    /// <param name="seed">Random seed (default 42)</param>
    public static double ArithmeticAsian(
        double s, double k, double t, double r, double sigma,
        int monitoringSteps = 252, int paths = 10_000,
        bool isPut = false, int seed = 42)
    {
        if (t <= 0) return isPut ? Math.Max(k - s, 0) : Math.Max(s - k, 0);

        double dt    = t / monitoringSteps;
        double drift = (r - 0.5 * sigma * sigma) * dt;
        double diff  = sigma * Math.Sqrt(dt);
        double disc  = Math.Exp(-r * t);
        var    rng   = new Random(seed);
        double sum   = 0;

        for (int p = 0; p < paths; p++)
        {
            double st  = s;
            double avg = 0;
            for (int i = 0; i < monitoringSteps; i++)
            {
                st  *= Math.Exp(drift + diff * BoxMuller(rng));
                avg += st;
            }
            avg /= monitoringSteps;
            sum += isPut ? Math.Max(k - avg, 0) : Math.Max(avg - k, 0);
        }
        return disc * sum / paths;
    }

    // ── Lookback options ──────────────────────────────────────────────────────

    /// <summary>
    /// European floating-strike lookback call option (closed-form).
    /// Payoff = S_T − min(S), i.e. the right to buy at the lowest price over the period.
    /// </summary>
    /// <param name="s">Current asset price (assumed to be the current minimum so far, i.e. at inception)</param>
    /// <param name="sMin">Minimum asset price observed so far (= s at inception)</param>
    /// <param name="t">Remaining time to expiry in years</param>
    /// <param name="r">Continuous risk-free rate</param>
    /// <param name="sigma">Annualised volatility</param>
    public static double LookbackCall(
        double s, double sMin, double t, double r, double sigma)
    {
        if (t <= 0) return Math.Max(s - sMin, 0);
        double sqrtT = Math.Sqrt(t);
        double a1    = (Math.Log(s / sMin) + (r + 0.5 * sigma * sigma) * t) / (sigma * sqrtT);
        double a2    = a1 - sigma * sqrtT;
        double a3    = (Math.Log(s / sMin) + (-r + 0.5 * sigma * sigma) * t) / (sigma * sqrtT);

        return s * N(a1)
             - sMin * Math.Exp(-r * t) * N(a2)
             - s * sigma * sigma / (2 * r) * (N(-a1) - Math.Exp(-r * t) * Math.Pow(s / sMin, -2 * r / (sigma * sigma)) * N(-a3));
    }

    /// <summary>
    /// European floating-strike lookback put option (closed-form).
    /// Payoff = max(S) − S_T, i.e. the right to sell at the highest price over the period.
    /// </summary>
    /// <param name="s">Current asset price</param>
    /// <param name="sMax">Maximum asset price observed so far (= s at inception)</param>
    /// <param name="t">Remaining time to expiry in years</param>
    /// <param name="r">Continuous risk-free rate</param>
    /// <param name="sigma">Annualised volatility</param>
    public static double LookbackPut(
        double s, double sMax, double t, double r, double sigma)
    {
        if (t <= 0) return Math.Max(sMax - s, 0);
        double sqrtT = Math.Sqrt(t);
        // Mirror of LookbackCall (Conze-Viswanathan 1991), with sMin -> sMax.
        double b1    = (Math.Log(s / sMax) + (r + 0.5 * sigma * sigma) * t) / (sigma * sqrtT);
        double b2    = b1 - sigma * sqrtT;
        double b3    = (Math.Log(s / sMax) + (-r + 0.5 * sigma * sigma) * t) / (sigma * sqrtT);

        return sMax * Math.Exp(-r * t) * N(-b2)
             - s * N(-b1)
             + s * sigma * sigma / (2 * r) * (N(b1) - Math.Exp(-r * t) * Math.Pow(s / sMax, -2 * r / (sigma * sigma)) * N(b3));
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static (double d1, double d2) D1D2(double s, double k, double t, double r, double sigma)
    {
        double d1 = (Math.Log(s / k) + (r + 0.5 * sigma * sigma) * t) / (sigma * Math.Sqrt(t));
        return (d1, d1 - sigma * Math.Sqrt(t));
    }

    // d1/d2 with separate cost-of-carry b (generalised Black-Scholes)
    private static (double d1, double d2) D1D2Adjusted(
        double s, double k, double t, double r, double b, double sigma)
    {
        double d1 = (Math.Log(s / k) + (b + 0.5 * sigma * sigma) * t) / (sigma * Math.Sqrt(t));
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

    private static double BoxMuller(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
