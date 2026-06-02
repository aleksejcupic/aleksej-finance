using System;
using Aleksej.Finance.Options;

namespace Aleksej.Finance.Tests;

public class MonteCarloMoreTests
{
    private const double S = 100, K = 100, T = 1, R = 0.05, Sigma = 0.20;

    // ── SimulatePaths ───────────────────────────────────────────────────────────

    [Fact]
    public void SimulatePaths_HasExpectedShapeAndPositivePrices()
    {
        const int paths = 1_000, steps = 50;
        double[,] sim = MonteCarlo.SimulatePaths(S, T, R, Sigma, paths, steps, seed: 7);

        // Documented shape is [paths, steps + 1].
        Assert.Equal(paths, sim.GetLength(0));
        Assert.Equal(steps + 1, sim.GetLength(1));

        for (int p = 0; p < paths; p++)
        {
            // Every path starts at the spot price.
            Assert.Equal(S, sim[p, 0], 10);
            // All simulated prices (including terminal) are strictly positive under GBM.
            for (int i = 0; i <= steps; i++)
                Assert.True(sim[p, i] > 0, "GBM prices must be positive.");
        }
    }

    [Fact]
    public void SimulatePaths_IsDeterministicForSameSeed()
    {
        double[,] a = MonteCarlo.SimulatePaths(S, T, R, Sigma, 200, 20, seed: 123);
        double[,] b = MonteCarlo.SimulatePaths(S, T, R, Sigma, 200, 20, seed: 123);

        Assert.Equal(a.GetLength(0), b.GetLength(0));
        Assert.Equal(a.GetLength(1), b.GetLength(1));
        for (int p = 0; p < a.GetLength(0); p++)
            for (int i = 0; i < a.GetLength(1); i++)
                Assert.Equal(a[p, i], b[p, i], 12);
    }

    [Fact]
    public void SimulatePaths_AntitheticRequiresEvenPaths()
    {
        Assert.Throws<ArgumentException>(
            () => MonteCarlo.SimulatePaths(S, T, R, Sigma, paths: 5, steps: 10, seed: 1, antithetic: true));
    }

    [Fact]
    public void SimulatePaths_ThrowsOnNonPositiveDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MonteCarlo.SimulatePaths(S, T, R, Sigma, paths: 0, steps: 10));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MonteCarlo.SimulatePaths(S, T, R, Sigma, paths: 10, steps: 0));
    }

    // ── AmericanPrice ────────────────────────────────────────────────────────────

    [Fact]
    public void AmericanPut_IsAtLeastEuropeanPut()
    {
        const int paths = 40_000, steps = 50, seed = 42;

        double american = MonteCarlo.AmericanPrice(
            S, K, T, R, Sigma, paths: paths, steps: steps, isPut: true, seed: seed);
        double european = MonteCarlo.EuropeanPrice(
            S, K, T, R, Sigma, paths: paths, steps: steps, isPut: true, seed: seed);

        // Early exercise can only add value; allow a tiny MC slack.
        Assert.True(american >= european - 0.05,
            $"American put ({american}) should be >= European put ({european}).");
        Assert.True(american > 0);
    }

    [Fact]
    public void AmericanCall_NoDividends_ApproximatesEuropeanCall()
    {
        const int paths = 40_000, steps = 50, seed = 42;

        double american = MonteCarlo.AmericanPrice(
            S, K, T, R, Sigma, paths: paths, steps: steps, isPut: false, seed: seed);
        double european = MonteCarlo.EuropeanPrice(
            S, K, T, R, Sigma, paths: paths, steps: steps, isPut: false, seed: seed);

        // With no dividends, early exercise of an American call is never optimal,
        // so the two prices should agree within Monte Carlo tolerance.
        Assert.Equal(european, american, 0.5);
    }

    [Fact]
    public void AmericanPrice_IsDeterministicForSameSeed()
    {
        double a = MonteCarlo.AmericanPrice(S, K, T, R, Sigma, paths: 5_000, steps: 25, isPut: true, seed: 99);
        double b = MonteCarlo.AmericanPrice(S, K, T, R, Sigma, paths: 5_000, steps: 25, isPut: true, seed: 99);
        Assert.Equal(a, b, 12);
    }

    [Fact]
    public void AmericanPrice_ReturnsIntrinsicValueAtExpiry()
    {
        // t <= 0 short-circuits to the intrinsic payoff.
        double put  = MonteCarlo.AmericanPrice(s: 90, k: 100, t: 0, r: R, sigma: Sigma, isPut: true);
        double call = MonteCarlo.AmericanPrice(s: 110, k: 100, t: 0, r: R, sigma: Sigma, isPut: false);

        Assert.Equal(10.0, put, 12);
        Assert.Equal(10.0, call, 12);
    }

    [Fact]
    public void AmericanPrice_ThrowsOnNonPositiveDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MonteCarlo.AmericanPrice(S, K, T, R, Sigma, paths: 0, steps: 10));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MonteCarlo.AmericanPrice(S, K, T, R, Sigma, paths: 10, steps: 0));
    }
}
