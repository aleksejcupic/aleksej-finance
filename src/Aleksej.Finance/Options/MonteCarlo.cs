using System;

namespace Aleksej.Finance.Options;

/// <summary>
/// Monte Carlo option pricing via Geometric Brownian Motion (GBM).
/// Includes European option pricing and American option pricing via the
/// Longstaff-Schwartz (2001) Least-Squares Monte Carlo (LSM) algorithm.
/// Ported and extended from the Python options-pricing project.
/// </summary>
public static class MonteCarlo
{
    // ── Path simulation ───────────────────────────────────────────────────────

    /// <summary>
    /// Simulates GBM price paths.
    /// S(i) = S(i−1) · exp((r − ½σ²)·dt + σ·√dt·Z),  Z ~ N(0,1)
    /// </summary>
    /// <param name="s">Initial asset price</param>
    /// <param name="t">Time horizon in years</param>
    /// <param name="r">Continuous risk-free rate</param>
    /// <param name="sigma">Annualised volatility</param>
    /// <param name="paths">Number of paths to simulate</param>
    /// <param name="steps">Number of time steps</param>
    /// <param name="seed">Random seed for reproducibility (default 42)</param>
    /// <param name="antithetic">
    ///   When true, the second half of paths uses negated normals, halving variance.
    ///   paths must be even when antithetic is true.
    /// </param>
    /// <returns>2-D array [paths, steps+1] of asset prices</returns>
    public static double[,] SimulatePaths(
        double s, double t, double r, double sigma,
        int paths, int steps,
        int seed = 42, bool antithetic = false)
    {
        if (paths < 1)  throw new ArgumentOutOfRangeException(nameof(paths));
        if (steps < 1)  throw new ArgumentOutOfRangeException(nameof(steps));
        if (antithetic && paths % 2 != 0)
            throw new ArgumentException("paths must be even when antithetic = true.");

        double dt    = t / steps;
        double drift = (r - 0.5 * sigma * sigma) * dt;
        double diff  = sigma * Math.Sqrt(dt);

        var result = new double[paths, steps + 1];
        var rng    = new Random(seed);

        if (antithetic)
        {
            int half = paths / 2;
            for (int p = 0; p < half; p++)
            {
                result[p,        0] = s;
                result[p + half, 0] = s;
                for (int i = 1; i <= steps; i++)
                {
                    double z = BoxMuller(rng);
                    result[p,        i] = result[p,        i - 1] * Math.Exp(drift + diff *  z);
                    result[p + half, i] = result[p + half, i - 1] * Math.Exp(drift + diff * -z);
                }
            }
        }
        else
        {
            for (int p = 0; p < paths; p++)
            {
                result[p, 0] = s;
                for (int i = 1; i <= steps; i++)
                    result[p, i] = result[p, i - 1] * Math.Exp(drift + diff * BoxMuller(rng));
            }
        }

        return result;
    }

    // ── European option via Monte Carlo ───────────────────────────────────────

    /// <summary>
    /// European option price via Monte Carlo simulation.
    /// Converges to the Black-Scholes price as paths → ∞.
    /// Cross-validate with <see cref="BlackScholes.Call"/> / <see cref="BlackScholes.Put"/>.
    /// </summary>
    /// <param name="s">Current asset price</param>
    /// <param name="k">Strike price</param>
    /// <param name="t">Time to expiry in years</param>
    /// <param name="r">Continuous risk-free rate</param>
    /// <param name="sigma">Annualised volatility</param>
    /// <param name="paths">Number of simulated paths (default 10 000)</param>
    /// <param name="steps">Number of time steps (default 50)</param>
    /// <param name="isPut">True for put, false for call</param>
    /// <param name="seed">Random seed (default 42)</param>
    public static double EuropeanPrice(
        double s, double k, double t, double r, double sigma,
        int paths = 10_000, int steps = 50,
        bool isPut = false, int seed = 42)
    {
        if (t <= 0) return isPut ? Math.Max(k - s, 0) : Math.Max(s - k, 0);

        double dt    = t / steps;
        double drift = (r - 0.5 * sigma * sigma) * dt;
        double diff  = sigma * Math.Sqrt(dt);
        double disc  = Math.Exp(-r * t);

        var rng   = new Random(seed);
        double sum = 0;

        for (int p = 0; p < paths; p++)
        {
            double st = s;
            for (int i = 0; i < steps; i++)
                st *= Math.Exp(drift + diff * BoxMuller(rng));
            sum += isPut ? Math.Max(k - st, 0) : Math.Max(st - k, 0);
        }
        return disc * sum / paths;
    }

    // ── American option via LSM ───────────────────────────────────────────────

    /// <summary>
    /// American option price via the Longstaff-Schwartz (2001) Least-Squares Monte Carlo method.
    /// At each time step, in-the-money paths are identified and an OLS regression of
    /// discounted continuation values against Laguerre basis functions of the spot price is used
    /// to estimate the continuation value. Early exercise is triggered when intrinsic > fitted continuation.
    /// </summary>
    /// <param name="s">Current asset price</param>
    /// <param name="k">Strike price</param>
    /// <param name="t">Time to expiry in years</param>
    /// <param name="r">Continuous risk-free rate</param>
    /// <param name="sigma">Annualised volatility</param>
    /// <param name="paths">Number of simulated paths (default 10 000)</param>
    /// <param name="steps">Number of time steps for backward induction (default 50)</param>
    /// <param name="isPut">True for put, false for call</param>
    /// <param name="seed">Random seed (default 42)</param>
    public static double AmericanPrice(
        double s, double k, double t, double r, double sigma,
        int paths = 10_000, int steps = 50,
        bool isPut = false, int seed = 42)
    {
        if (t <= 0) return isPut ? Math.Max(k - s, 0) : Math.Max(s - k, 0);
        if (paths < 1) throw new ArgumentOutOfRangeException(nameof(paths));
        if (steps < 1) throw new ArgumentOutOfRangeException(nameof(steps));

        double dt = t / steps;

        // Simulate full path matrix
        var spot = new double[paths, steps + 1];
        {
            double drift = (r - 0.5 * sigma * sigma) * dt;
            double diff  = sigma * Math.Sqrt(dt);
            var rng      = new Random(seed);
            for (int p = 0; p < paths; p++)
            {
                spot[p, 0] = s;
                for (int i = 1; i <= steps; i++)
                    spot[p, i] = spot[p, i - 1] * Math.Exp(drift + diff * BoxMuller(rng));
            }
        }

        // Initialise: exercise at expiry for all paths
        var cashflow     = new double[paths];
        var exerciseStep = new int[paths];
        for (int p = 0; p < paths; p++)
        {
            cashflow[p]     = Payoff(spot[p, steps], k, isPut);
            exerciseStep[p] = steps;
        }

        // Backward induction
        for (int i = steps - 1; i >= 1; i--)
        {
            // Collect in-the-money paths
            int itmCount = 0;
            for (int p = 0; p < paths; p++)
                if (Payoff(spot[p, i], k, isPut) > 0) itmCount++;

            if (itmCount < 4) continue; // too few to regress

            var xData = new double[itmCount][];
            var yData = new double[itmCount];
            var itmIdx = new int[itmCount];
            int idx = 0;
            for (int p = 0; p < paths; p++)
            {
                if (Payoff(spot[p, i], k, isPut) <= 0) continue;
                itmIdx[idx]  = p;
                xData[idx]   = LaguerreBasis(spot[p, i] / k);
                // discounted continuation value from path p's future exercise
                yData[idx]   = cashflow[p] * Math.Exp(-r * dt * (exerciseStep[p] - i));
                idx++;
            }

            double[] beta = OlsSolve(xData, yData, itmCount);

            // Early exercise decision for each ITM path
            for (int j = 0; j < itmCount; j++)
            {
                int p          = itmIdx[j];
                double intrins = Payoff(spot[p, i], k, isPut);
                double cont    = Dot(beta, xData[j]);
                if (intrins > cont)
                {
                    cashflow[p]     = intrins;
                    exerciseStep[p] = i;
                }
            }
        }

        // Price = mean of discounted cashflows
        double sum = 0;
        for (int p = 0; p < paths; p++)
            sum += cashflow[p] * Math.Exp(-r * dt * exerciseStep[p]);
        return sum / paths;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static double Payoff(double spot, double k, bool isPut) =>
        isPut ? Math.Max(k - spot, 0) : Math.Max(spot - k, 0);

    // Box-Muller transform: maps two uniform(0,1) samples to one standard normal.
    private static double BoxMuller(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble(); // avoid log(0)
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    // Laguerre polynomial basis (Longstaff-Schwartz 2001 equation 3):
    // L0(x) = exp(−x/2)
    // L1(x) = exp(−x/2) · (1 − x)
    // L2(x) = exp(−x/2) · (1 − 2x + x²/2)
    private static double[] LaguerreBasis(double x)
    {
        double e = Math.Exp(-x / 2);
        return new double[] { e, e * (1 - x), e * (1 - 2 * x + 0.5 * x * x) };
    }

    // OLS regression: solves (XᵀX)·β = Xᵀy for a 3-column design matrix.
    private static double[] OlsSolve(double[][] x, double[] y, int n)
    {
        var xtx = new double[3, 3];
        var xty = new double[3];

        for (int i = 0; i < n; i++)
        {
            for (int a = 0; a < 3; a++)
            {
                xty[a] += x[i][a] * y[i];
                for (int b = 0; b < 3; b++)
                    xtx[a, b] += x[i][a] * x[i][b];
            }
        }
        return Solve3x3(xtx, xty);
    }

    // 3×3 linear system via Gaussian elimination with partial pivoting.
    // Returns [0,0,0] if the matrix is near-singular (degenerate ITM set).
    private static double[] Solve3x3(double[,] a, double[] b)
    {
        // Augment: [a | b] as a 3×4 working matrix
        var m = new double[3, 4];
        for (int i = 0; i < 3; i++) { m[i, 0] = a[i, 0]; m[i, 1] = a[i, 1]; m[i, 2] = a[i, 2]; m[i, 3] = b[i]; }

        for (int col = 0; col < 3; col++)
        {
            // Partial pivot
            int pivot = col;
            for (int row = col + 1; row < 3; row++)
                if (Math.Abs(m[row, col]) > Math.Abs(m[pivot, col])) pivot = row;

            if (Math.Abs(m[pivot, col]) < 1e-14) return new double[] { 0, 0, 0 }; // singular

            if (pivot != col)
                for (int k = 0; k < 4; k++) (m[col, k], m[pivot, k]) = (m[pivot, k], m[col, k]);

            double diag = m[col, col];
            for (int k = col; k < 4; k++) m[col, k] /= diag;

            for (int row = 0; row < 3; row++)
            {
                if (row == col) continue;
                double factor = m[row, col];
                for (int k = col; k < 4; k++) m[row, k] -= factor * m[col, k];
            }
        }
        return new double[] { m[0, 3], m[1, 3], m[2, 3] };
    }

    private static double Dot(double[] a, double[] b)
    {
        double s = 0;
        for (int i = 0; i < a.Length; i++) s += a[i] * b[i];
        return s;
    }
}
