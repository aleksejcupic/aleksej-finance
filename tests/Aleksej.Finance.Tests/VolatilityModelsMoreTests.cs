using System;
using Aleksej.Finance.Risk;

namespace Aleksej.Finance.Tests;

public class VolatilityModelsMoreTests
{
    // Deterministic synthetic return series (no time-seeded randomness).
    // A fixed-seed LCG drives a Box-Muller-ish deterministic sequence scaled to
    // a realistic daily-return magnitude (~1% std). Fully reproducible.
    private static double[] MakeSyntheticReturns(int n)
    {
        var r = new double[n];
        // Simple deterministic pseudo-random generator with a fixed seed.
        ulong state = 0x9E3779B97F4A7C15UL;
        for (int i = 0; i < n; i++)
        {
            state = state * 6364136223846793005UL + 1442695040888963407UL;
            // Map top bits to (0,1).
            double u = ((state >> 11) & 0x1FFFFFFFFFFFFFUL) / (double)(1UL << 53);
            // Center around zero, scale to ~1% daily move.
            r[i] = (u - 0.5) * 0.04; // range roughly [-0.02, 0.02]
        }
        return r;
    }

    [Fact]
    public void GarchMle_ReturnsFiniteParametersInValidRanges()
    {
        double[] returns = MakeSyntheticReturns(800);

        (double omega, double alpha, double beta) = VolatilityModels.GarchMle(returns);

        Assert.True(double.IsFinite(omega), "omega must be finite.");
        Assert.True(double.IsFinite(alpha), "alpha must be finite.");
        Assert.True(double.IsFinite(beta), "beta must be finite.");

        Assert.True(omega > 0, "omega must be strictly positive.");
        Assert.True(alpha >= 0, "alpha must be non-negative.");
        Assert.True(beta >= 0, "beta must be non-negative.");
        Assert.True(alpha + beta < 1.0, "alpha + beta must be < 1 for stationarity.");
    }

    [Fact]
    public void GarchMle_ParametersFeedConsistentlyIntoGarchVolatility()
    {
        double[] returns = MakeSyntheticReturns(600);

        (double omega, double alpha, double beta) = VolatilityModels.GarchMle(returns);

        // The estimated parameters must be valid inputs to GarchVolatility
        // (stationary), and the resulting series should be finite and positive.
        double[] vol = VolatilityModels.GarchVolatility(returns, omega, alpha, beta);

        Assert.Equal(returns.Length, vol.Length);
        Assert.All(vol, v =>
        {
            Assert.True(double.IsFinite(v), "GARCH volatility must be finite.");
            Assert.True(v > 0, "GARCH volatility must be positive.");
        });

        // First value equals the annualised long-run vol implied by the estimates.
        double vL = VolatilityModels.GarchLongRunVariance(omega, alpha, beta);
        Assert.Equal(Math.Sqrt(vL * 252), vol[0], 12);
    }

    [Fact]
    public void GarchMle_Throws_ForTooFewReturns()
    {
        Assert.Throws<ArgumentException>(() => VolatilityModels.GarchMle(MakeSyntheticReturns(29)));
    }
}
