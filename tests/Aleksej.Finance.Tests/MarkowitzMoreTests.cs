using System;
using Aleksej.Finance.Portfolio;

namespace Aleksej.Finance.Tests;

public class MarkowitzMoreTests
{
    // A small, well-conditioned 3-asset covariance matrix and mean-return vector
    // reused across the optimizer tests.
    private static double[,] Cov() => new double[,]
    {
        { 0.04, 0.01, 0.00 },
        { 0.01, 0.09, 0.02 },
        { 0.00, 0.02, 0.16 },
    };

    private static double[] Mu() => new[] { 0.08, 0.12, 0.18 };

    private static double Sum(double[] v)
    {
        double s = 0;
        foreach (double x in v) s += x;
        return s;
    }

    // ── Input statistics ──────────────────────────────────────────────────────

    [Fact]
    public void MeanReturns_EqualsHandComputedColumnMeans()
    {
        // 3 days × 2 assets daily returns.
        double[,] returns =
        {
            { 0.01, -0.02 },
            { 0.03,  0.04 },
            { 0.02,  0.01 },
        };

        // Annualised with 1 trading day per year => just the column means.
        double[] mu = Markowitz.MeanReturns(returns, tradingDays: 1);

        double expected0 = (0.01 + 0.03 + 0.02) / 3.0;
        double expected1 = (-0.02 + 0.04 + 0.01) / 3.0;

        Assert.Equal(2, mu.Length);
        Assert.Equal(expected0, mu[0], 12);
        Assert.Equal(expected1, mu[1], 12);
    }

    [Fact]
    public void MeanReturns_AnnualisationScalesLinearly()
    {
        double[,] returns =
        {
            { 0.01, -0.02 },
            { 0.03,  0.04 },
            { 0.02,  0.01 },
        };

        double[] daily  = Markowitz.MeanReturns(returns, tradingDays: 1);
        double[] annual = Markowitz.MeanReturns(returns, tradingDays: 252);

        Assert.Equal(daily[0] * 252, annual[0], 10);
        Assert.Equal(daily[1] * 252, annual[1], 10);
    }

    [Fact]
    public void CovarianceMatrix_IsSymmetricWithNonNegativeDiagonal()
    {
        double[,] returns =
        {
            {  0.01, -0.02,  0.00 },
            {  0.03,  0.04,  0.01 },
            { -0.01,  0.02, -0.02 },
            {  0.02,  0.01,  0.03 },
        };

        double[,] cov = Markowitz.CovarianceMatrix(returns);
        int n = cov.GetLength(0);

        Assert.Equal(3, n);
        for (int i = 0; i < n; i++)
        {
            Assert.True(cov[i, i] >= 0, "Diagonal variance must be non-negative.");
            for (int j = 0; j < n; j++)
                Assert.Equal(cov[i, j], cov[j, i], 12);
        }
    }

    [Fact]
    public void CovarianceMatrix_DiagonalMatchesScaledSampleVariance()
    {
        double[,] returns =
        {
            { 0.01 },
            { 0.03 },
            { 0.02 },
            { 0.05 },
        };

        // Hand-compute sample variance (denominator T-1) for the single asset.
        double mean = (0.01 + 0.03 + 0.02 + 0.05) / 4.0;
        double ss = 0;
        foreach (double r in new[] { 0.01, 0.03, 0.02, 0.05 }) ss += (r - mean) * (r - mean);
        double sampleVar = ss / 3.0; // T-1 = 3

        double[,] cov = Markowitz.CovarianceMatrix(returns, tradingDays: 1);
        Assert.Equal(sampleVar, cov[0, 0], 12);
    }

    // ── Portfolio statistics identities ─────────────────────────────────────────

    [Fact]
    public void PortfolioReturn_EqualsDotOfWeightsAndMeans()
    {
        double[] mu = Mu();
        double[] w  = { 0.5, 0.3, 0.2 };

        double expected = 0.5 * mu[0] + 0.3 * mu[1] + 0.2 * mu[2];
        Assert.Equal(expected, Markowitz.PortfolioReturn(w, mu), 12);
    }

    [Fact]
    public void PortfolioVolatility_EqualsSqrtOfVariance()
    {
        double[,] cov = Cov();
        double[] w    = { 0.4, 0.4, 0.2 };

        double var = Markowitz.PortfolioVariance(w, cov);
        double vol = Markowitz.PortfolioVolatility(w, cov);

        Assert.True(var > 0);
        Assert.Equal(Math.Sqrt(var), vol, 12);
    }

    [Fact]
    public void PortfolioSharpe_MatchesDefinition()
    {
        double[,] cov = Cov();
        double[] mu   = Mu();
        double[] w    = { 0.4, 0.4, 0.2 };
        double rf     = 0.02;

        double ret = Markowitz.PortfolioReturn(w, mu);
        double vol = Markowitz.PortfolioVolatility(w, cov);
        double expected = (ret - rf) / vol;

        Assert.Equal(expected, Markowitz.PortfolioSharpe(w, mu, cov, rf), 12);
    }

    // ── Optimal portfolios: weights sum to 1 ────────────────────────────────────

    [Fact]
    public void EfficientPortfolio_WeightsSumToOne_AndAchievesTargetReturn()
    {
        double[] mu   = Mu();
        double[,] cov = Cov();
        double target = 0.13;

        double[]? w = Markowitz.EfficientPortfolio(mu, cov, target);
        Assert.NotNull(w);

        Assert.Equal(1.0, Sum(w!), 8);
        // The two-constraint Lagrange solution hits the target return exactly.
        Assert.Equal(target, Markowitz.PortfolioReturn(w!, mu), 8);
    }

    [Fact]
    public void EfficientPortfolio_ReturnsNull_WhenAllMeansEqual()
    {
        // Equal expected returns => discriminant D ≈ 0 => null.
        double[] mu   = { 0.10, 0.10, 0.10 };
        double[,] cov = Cov();

        Assert.Null(Markowitz.EfficientPortfolio(mu, cov, 0.10));
    }

    [Fact]
    public void MaxSharpePortfolio_Unconstrained_WeightsSumToOne()
    {
        double[] w = Markowitz.MaxSharpePortfolio(Mu(), Cov(), riskFreeRate: 0.02);
        Assert.Equal(1.0, Sum(w), 8);
    }

    [Fact]
    public void MaxSharpePortfolioConstrained_LongOnly_AndWeightsSumToOne()
    {
        double[] w = Markowitz.MaxSharpePortfolioConstrained(Mu(), Cov(), riskFreeRate: 0.02);

        Assert.Equal(1.0, Sum(w), 6);
        foreach (double wi in w)
        {
            Assert.True(wi >= -1e-9, $"Constrained weight should be >= 0 but was {wi}.");
            Assert.True(wi <= 1.0 + 1e-9, $"Constrained weight should be <= 1 but was {wi}.");
        }
    }

    // ── Risk parity / risk contributions ────────────────────────────────────────

    [Fact]
    public void RiskParityPortfolio_WeightsSumToOne_AndArePositive()
    {
        double[] w = Markowitz.RiskParityPortfolio(Cov());

        Assert.Equal(1.0, Sum(w), 6);
        foreach (double wi in w)
            Assert.True(wi > 0, "Risk parity weights are long-only by construction.");
    }

    [Fact]
    public void RiskContributions_SumToOne()
    {
        double[,] cov = Cov();
        double[] w    = { 0.4, 0.4, 0.2 };

        double[] rc = Markowitz.RiskContributions(w, cov);
        Assert.Equal(1.0, Sum(rc), 10);
    }

    [Fact]
    public void RiskParityPortfolio_HasApproximatelyEqualRiskContributions()
    {
        double[,] cov = Cov();
        double[] w    = Markowitz.RiskParityPortfolio(cov);
        double[] rc   = Markowitz.RiskContributions(w, cov);

        // For an ERC portfolio each contribution should be ~ 1/n.
        double target = 1.0 / rc.Length;
        foreach (double c in rc)
            Assert.Equal(target, c, 4);
    }

    // ── FrontierPoint / EfficientFrontier ───────────────────────────────────────

    [Fact]
    public void EfficientFrontier_ProducesConsistentFrontierPoints()
    {
        double[] mu   = Mu();
        double[,] cov = Cov();

        Markowitz.FrontierPoint[] frontier = Markowitz.EfficientFrontier(mu, cov, numPoints: 25);

        Assert.Equal(25, frontier.Length);
        foreach (Markowitz.FrontierPoint pt in frontier)
        {
            Assert.True(pt.Volatility > 0, "Frontier volatility must be positive.");
            Assert.NotNull(pt.Weights);
            Assert.Equal(mu.Length, pt.Weights.Length);
            Assert.Equal(1.0, Sum(pt.Weights), 6);
            // The stored volatility must equal the recomputed volatility for the weights.
            Assert.Equal(Markowitz.PortfolioVolatility(pt.Weights, cov), pt.Volatility, 10);
        }

        // Expected returns are monotonically non-decreasing across the frontier.
        for (int i = 1; i < frontier.Length; i++)
            Assert.True(frontier[i].ExpectedReturn >= frontier[i - 1].ExpectedReturn);
    }

    [Fact]
    public void EfficientFrontier_ThrowsWhenNumPointsTooSmall()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Markowitz.EfficientFrontier(Mu(), Cov(), numPoints: 1));
    }
}
