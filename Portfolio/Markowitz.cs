using System;

namespace AleksejCupic.FinancialMath.Portfolio;

/// <summary>
/// Markowitz (1952) mean-variance portfolio optimization.
/// Computes the efficient frontier, minimum variance portfolio, and the
/// maximum Sharpe ratio (tangency) portfolio from historical return data.
/// Ported and extended from the Python portfolio-optimizer project.
/// </summary>
public static class Markowitz
{
    // ── Input statistics ──────────────────────────────────────────────────────

    /// <summary>
    /// Annualised mean return vector from a T × n matrix of daily returns.
    /// </summary>
    /// <param name="returns">Daily returns matrix: returns[t, i] = return of asset i on day t</param>
    /// <param name="tradingDays">Trading days per year for annualisation (default 252)</param>
    /// <returns>Array of length n: annualised mean returns</returns>
    public static double[] MeanReturns(double[,] returns, int tradingDays = 252)
    {
        int t = returns.GetLength(0), n = returns.GetLength(1);
        var mu = new double[n];
        for (int i = 0; i < n; i++)
        {
            double sum = 0;
            for (int d = 0; d < t; d++) sum += returns[d, i];
            mu[i] = (sum / t) * tradingDays;
        }
        return mu;
    }

    /// <summary>
    /// Annualised sample covariance matrix (n × n) from a T × n matrix of daily returns.
    /// Uses denominator T−1. Scaled by tradingDays.
    /// </summary>
    /// <param name="returns">Daily returns matrix</param>
    /// <param name="tradingDays">Trading days per year (default 252)</param>
    public static double[,] CovarianceMatrix(double[,] returns, int tradingDays = 252)
    {
        int t = returns.GetLength(0), n = returns.GetLength(1);

        // Daily means
        var mu = new double[n];
        for (int i = 0; i < n; i++)
        {
            double sum = 0;
            for (int d = 0; d < t; d++) sum += returns[d, i];
            mu[i] = sum / t;
        }

        var cov = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = i; j < n; j++)
            {
                double c = 0;
                for (int d = 0; d < t; d++)
                    c += (returns[d, i] - mu[i]) * (returns[d, j] - mu[j]);
                cov[i, j] = cov[j, i] = (c / (t - 1)) * tradingDays;
            }
        return cov;
    }

    // ── Portfolio statistics ───────────────────────────────────────────────────

    /// <summary>Expected annualised return of a portfolio: w · μ</summary>
    public static double PortfolioReturn(double[] weights, double[] mu)
        => Dot(weights, mu);

    /// <summary>Annualised portfolio variance: w' Σ w</summary>
    public static double PortfolioVariance(double[] weights, double[,] cov)
        => Dot(weights, MatVec(cov, weights));

    /// <summary>Annualised portfolio volatility: √(w' Σ w)</summary>
    public static double PortfolioVolatility(double[] weights, double[,] cov)
        => Math.Sqrt(PortfolioVariance(weights, cov));

    /// <summary>Sharpe ratio: (w·μ − rf) / √(w' Σ w)</summary>
    public static double PortfolioSharpe(double[] weights, double[] mu, double[,] cov, double riskFreeRate)
    {
        double vol = PortfolioVolatility(weights, cov);
        return vol < 1e-12 ? 0 : (PortfolioReturn(weights, mu) - riskFreeRate) / vol;
    }

    // ── Optimal portfolios ─────────────────────────────────────────────────────

    /// <summary>
    /// Global minimum variance portfolio (analytical closed form).
    /// Solves: min w'Σw  s.t. w'1 = 1  (no return constraint, no short-selling constraint).
    /// w* = Σ⁻¹·1 / (1'·Σ⁻¹·1)
    /// </summary>
    /// <param name="cov">Annualised covariance matrix (n × n)</param>
    public static double[] MinVariancePortfolio(double[,] cov)
    {
        int n = cov.GetLength(0);
        var sigInv = MatInverse(cov) ?? throw new InvalidOperationException("Covariance matrix is singular.");
        var ones   = Fill(n, 1.0);
        var v      = MatVec(sigInv, ones);
        double d   = Dot(ones, v);
        return Scale(v, 1.0 / d);
    }

    /// <summary>
    /// Efficient portfolio achieving exactly targetReturn (analytical, two-constraint Lagrange).
    /// Solves: min w'Σw  s.t. w'μ = targetReturn,  w'1 = 1.
    /// Returns null if the covariance matrix is singular or the discriminant is zero
    /// (all assets have the same expected return).
    /// Note: this is the unconstrained frontier; weights may be negative (short positions allowed).
    /// For long-only, use <see cref="MaxSharpePortfolioConstrained"/> or clip/renormalise.
    /// </summary>
    public static double[]? EfficientPortfolio(double[] mu, double[,] cov, double targetReturn)
    {
        int n       = mu.Length;
        var sigInv  = MatInverse(cov);
        if (sigInv == null) return null;

        var ones    = Fill(n, 1.0);
        var siMu    = MatVec(sigInv, mu);
        var siOnes  = MatVec(sigInv, ones);

        double a = Dot(ones, siOnes); // 1' Σ⁻¹ 1
        double b = Dot(ones, siMu);   // 1' Σ⁻¹ μ
        double c = Dot(mu,   siMu);   // μ' Σ⁻¹ μ
        double D = a * c - b * b;

        if (Math.Abs(D) < 1e-14) return null;

        double lambda1 = (c - b * targetReturn) / D;
        double lambda2 = (a * targetReturn - b) / D;

        // w = Σ⁻¹ · (λ1·1 + λ2·μ)
        var rhs = new double[n];
        for (int i = 0; i < n; i++) rhs[i] = lambda1 * ones[i] + lambda2 * mu[i];
        return MatVec(sigInv, rhs);
    }

    /// <summary>
    /// Efficient frontier — array of (return, volatility, weights) points spanning
    /// from the minimum variance portfolio return to the maximum individual asset return.
    /// Uses the analytical two-constraint Lagrange solution (may include short positions).
    /// </summary>
    /// <param name="mu">Annualised mean return vector</param>
    /// <param name="cov">Annualised covariance matrix</param>
    /// <param name="numPoints">Number of frontier points (default 100)</param>
    public static FrontierPoint[] EfficientFrontier(double[] mu, double[,] cov, int numPoints = 100)
    {
        if (numPoints < 2) throw new ArgumentOutOfRangeException(nameof(numPoints));

        double muMin = PortfolioReturn(MinVariancePortfolio(cov), mu);
        double muMax = double.MinValue;
        foreach (double m in mu) if (m > muMax) muMax = m;

        var points = new FrontierPoint[numPoints];
        for (int i = 0; i < numPoints; i++)
        {
            double target  = muMin + (muMax - muMin) * i / (numPoints - 1);
            double[]? w    = EfficientPortfolio(mu, cov, target);
            w            ??= MinVariancePortfolio(cov);
            points[i]      = new FrontierPoint
            {
                ExpectedReturn = target,
                Volatility     = PortfolioVolatility(w, cov),
                Weights        = w
            };
        }
        return points;
    }

    /// <summary>
    /// Maximum Sharpe ratio (tangency) portfolio — unconstrained analytical solution.
    /// w* ∝ Σ⁻¹ · (μ − rf · 1)
    /// Weights are normalised to sum to 1 but may be negative (short positions).
    /// If all excess returns are zero or the matrix is singular, returns equal weights.
    /// </summary>
    /// <param name="mu">Annualised mean return vector</param>
    /// <param name="cov">Annualised covariance matrix</param>
    /// <param name="riskFreeRate">Annual risk-free rate</param>
    public static double[] MaxSharpePortfolio(double[] mu, double[,] cov, double riskFreeRate)
    {
        int n         = mu.Length;
        var sigInvRaw = MatInverse(cov);
        if (sigInvRaw == null) return EqualWeight(n);
        var sigInv    = sigInvRaw;
        var excess    = new double[n];
        for (int i = 0; i < n; i++) excess[i] = mu[i] - riskFreeRate;

        var v     = MatVec(sigInv, excess);
        var ones  = Fill(n, 1.0);
        double d  = Dot(ones, v);

        if (Math.Abs(d) < 1e-12) return EqualWeight(n);
        return Scale(v, 1.0 / d);
    }

    /// <summary>
    /// Maximum Sharpe ratio portfolio with long-only constraint (0 ≤ w_i ≤ 1, Σw_i = 1).
    /// Uses projected gradient ascent: gradient step on Sharpe, then project onto the simplex.
    /// </summary>
    /// <param name="mu">Annualised mean return vector</param>
    /// <param name="cov">Annualised covariance matrix</param>
    /// <param name="riskFreeRate">Annual risk-free rate</param>
    /// <param name="maxIter">Maximum iterations (default 2000)</param>
    /// <param name="learningRate">Gradient step size (default 1e-3)</param>
    public static double[] MaxSharpePortfolioConstrained(
        double[] mu, double[,] cov, double riskFreeRate,
        int maxIter = 2000, double learningRate = 1e-3)
    {
        int n  = mu.Length;
        var w  = EqualWeight(n); // feasible starting point on the simplex

        for (int iter = 0; iter < maxIter; iter++)
        {
            double ret  = PortfolioReturn(w, mu);
            double var_ = PortfolioVariance(w, cov);
            double vol  = Math.Sqrt(var_);
            if (vol < 1e-12) break;
            double sr   = (ret - riskFreeRate) / vol;

            // Gradient of Sharpe w.r.t. w_i: dSR/dw_i = (μ_i − rf)/vol − sr·(Σw)_i/var
            var sigw = MatVec(cov, w);
            var grad = new double[n];
            for (int i = 0; i < n; i++)
                grad[i] = (mu[i] - riskFreeRate) / vol - sr * sigw[i] / var_;

            // Gradient ascent step
            var wNew = new double[n];
            for (int i = 0; i < n; i++) wNew[i] = w[i] + learningRate * grad[i];

            // Project onto probability simplex
            wNew = ProjectSimplex(wNew);

            // Convergence check
            double maxDiff = 0;
            for (int i = 0; i < n; i++) maxDiff = Math.Max(maxDiff, Math.Abs(wNew[i] - w[i]));
            w = wNew;
            if (maxDiff < 1e-9) break;
        }
        return w;
    }

    // ── Risk parity ───────────────────────────────────────────────────────────

    /// <summary>
    /// Risk parity portfolio — weights such that each asset contributes equally
    /// to total portfolio volatility (equal risk contribution, ERC).
    /// The marginal risk contribution of asset i is: MRC_i = w_i · (Σw)_i / σ_p.
    /// We solve for w such that all MRC_i are equal.
    /// Uses iterative reweighting (Maillard et al. 2010).
    /// All weights are positive (long-only by construction).
    /// </summary>
    /// <param name="cov">Annualised covariance matrix (n × n)</param>
    /// <param name="maxIter">Maximum iterations (default 1000)</param>
    /// <param name="tol">Convergence tolerance on weight change (default 1e-10)</param>
    public static double[] RiskParityPortfolio(
        double[,] cov, int maxIter = 1000, double tol = 1e-10)
    {
        int n = cov.GetLength(0);
        var w = EqualWeight(n);

        for (int iter = 0; iter < maxIter; iter++)
        {
            var sigW  = MatVec(cov, w);
            double vp = Dot(w, sigW); // portfolio variance
            if (vp < 1e-14) break;

            // Risk contribution of each asset: RC_i = w_i · (Σw)_i / sqrt(vp)
            // Target: RC_i = σ_p / n for all i  =>  w_i · (Σw)_i = vp / n
            // Update: w_i_new = w_i / (Σw)_i * sqrt(vp/n)  (iterative reweighting)
            var wNew = new double[n];
            for (int i = 0; i < n; i++)
                wNew[i] = sigW[i] > 1e-14 ? w[i] * Math.Sqrt(vp / n) / sigW[i] : w[i];

            // Normalise to sum to 1
            double sum = 0;
            foreach (double wi in wNew) sum += wi;
            for (int i = 0; i < n; i++) wNew[i] /= sum;

            double maxDiff = 0;
            for (int i = 0; i < n; i++) maxDiff = Math.Max(maxDiff, Math.Abs(wNew[i] - w[i]));
            w = wNew;
            if (maxDiff < tol) break;
        }
        return w;
    }

    /// <summary>
    /// Risk contribution of each asset as a fraction of total portfolio volatility.
    /// RC_i = w_i · (Σw)_i / (w' Σ w)
    /// Sum of all RC_i = 1. For risk parity, all RC_i ≈ 1/n.
    /// </summary>
    public static double[] RiskContributions(double[] weights, double[,] cov)
    {
        var sigW   = MatVec(cov, weights);
        double var_ = Dot(weights, sigW);
        if (var_ < 1e-14) return Fill(weights.Length, 1.0 / weights.Length);
        var rc = new double[weights.Length];
        for (int i = 0; i < weights.Length; i++)
            rc[i] = weights[i] * sigW[i] / var_;
        return rc;
    }

    // ── Result type ───────────────────────────────────────────────────────────

    /// <summary>One point on the efficient frontier.</summary>
    public readonly struct FrontierPoint
    {
        /// <summary>Annualised expected return</summary>
        public double ExpectedReturn { get; init; }
        /// <summary>Annualised volatility (standard deviation)</summary>
        public double Volatility     { get; init; }
        /// <summary>Portfolio weights (length n)</summary>
        public double[] Weights      { get; init; }
    }

    // ── Private linear algebra helpers ────────────────────────────────────────

    // Matrix-vector product: A·v
    private static double[] MatVec(double[,] a, double[] v)
    {
        int n = v.Length;
        var r = new double[n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                r[i] += a[i, j] * v[j];
        return r;
    }

    // n×n matrix inverse via Gaussian elimination with partial pivoting.
    // Returns null if the matrix is singular (det < 1e-14).
    private static double[,]? MatInverse(double[,] a)
    {
        int n   = a.GetLength(0);
        var aug = new double[n, 2 * n];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++) aug[i, j]     = a[i, j];
            aug[i, n + i] = 1;
        }

        for (int col = 0; col < n; col++)
        {
            // Partial pivot
            int pivot = col;
            for (int row = col + 1; row < n; row++)
                if (Math.Abs(aug[row, col]) > Math.Abs(aug[pivot, col])) pivot = row;

            if (Math.Abs(aug[pivot, col]) < 1e-14) return null;

            if (pivot != col)
                for (int k = 0; k < 2 * n; k++) (aug[col, k], aug[pivot, k]) = (aug[pivot, k], aug[col, k]);

            double diag = aug[col, col];
            for (int k = 0; k < 2 * n; k++) aug[col, k] /= diag;

            for (int row = 0; row < n; row++)
            {
                if (row == col) continue;
                double factor = aug[row, col];
                for (int k = 0; k < 2 * n; k++) aug[row, k] -= factor * aug[col, k];
            }
        }

        var inv = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                inv[i, j] = aug[i, n + j];
        return inv;
    }

    // Duchi (2008) simplex projection: find w_i ≥ 0 with Σw_i = 1 closest to v.
    private static double[] ProjectSimplex(double[] v)
    {
        int n    = v.Length;
        var u    = (double[])v.Clone();
        Array.Sort(u, (a, b) => b.CompareTo(a)); // descending

        double cumSum = 0;
        int rho = 0;
        for (int j = 0; j < n; j++)
        {
            cumSum += u[j];
            if (u[j] - (cumSum - 1.0) / (j + 1) > 0) rho = j;
        }

        double cumSumRho = 0;
        for (int j = 0; j <= rho; j++) cumSumRho += u[j];
        double theta = (cumSumRho - 1.0) / (rho + 1);

        var w = new double[n];
        for (int i = 0; i < n; i++) w[i] = Math.Max(v[i] - theta, 0);
        return w;
    }

    private static double Dot(double[] a, double[] b)
    {
        double s = 0;
        for (int i = 0; i < a.Length; i++) s += a[i] * b[i];
        return s;
    }

    private static double[] Scale(double[] v, double scalar)
    {
        var r = new double[v.Length];
        for (int i = 0; i < v.Length; i++) r[i] = v[i] * scalar;
        return r;
    }

    private static double[] Fill(int n, double val)
    {
        var r = new double[n];
        Array.Fill(r, val);
        return r;
    }

    private static double[] EqualWeight(int n) => Fill(n, 1.0 / n);
}
