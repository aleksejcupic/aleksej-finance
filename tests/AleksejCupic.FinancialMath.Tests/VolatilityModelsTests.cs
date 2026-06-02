using System;
using System.Linq;
using AleksejCupic.FinancialMath.Risk;

namespace AleksejCupic.FinancialMath.Tests;

public class VolatilityModelsTests
{
    // ── EWMA ──────────────────────────────────────────────────────────────────

    [Fact]
    public void EwmaVolatility_IsPositive_ForNonConstantReturns()
    {
        // Alternating up/down returns -> genuine variation, non-zero variance.
        var returns = new double[40];
        for (int i = 0; i < returns.Length; i++)
            returns[i] = (i % 2 == 0) ? 0.02 : -0.015;

        double[] vol = VolatilityModels.EwmaVolatility(returns);

        Assert.Equal(returns.Length, vol.Length);
        Assert.True(vol[^1] > 0, "Latest EWMA volatility should be strictly positive for a varying series.");
        Assert.All(vol, v => Assert.True(v >= 0, "EWMA volatility values must be non-negative."));
    }

    [Fact]
    public void EwmaVolatility_IsZero_ForAllZeroReturns()
    {
        var returns = new double[30]; // all zeros

        double[] vol = VolatilityModels.EwmaVolatility(returns);

        // With zero returns the warm-up variance is zero and the recursion stays at zero.
        Assert.All(vol, v => Assert.Equal(0.0, v, 12));
    }

    [Fact]
    public void EwmaVolatilityLatest_MatchesLastElementOfSeries()
    {
        var returns = new double[] { 0.01, -0.02, 0.015, -0.005, 0.03, -0.01, 0.02, -0.025 };

        double[] series = VolatilityModels.EwmaVolatility(returns);
        double latest = VolatilityModels.EwmaVolatilityLatest(returns);

        Assert.Equal(series[^1], latest, 12);
    }

    [Fact]
    public void EwmaVolatility_Throws_ForTooFewReturns()
    {
        Assert.Throws<ArgumentException>(() => VolatilityModels.EwmaVolatility(new double[] { 0.01 }));
    }

    // ── Rolling historical volatility ───────────────────────────────────────────

    [Fact]
    public void RollingVolatility_IsPositiveAndPlausible_ForConstantVolSeries()
    {
        // Deterministic alternating series of magnitude ~1% per day -> stable, positive vol.
        var returns = new double[60];
        for (int i = 0; i < returns.Length; i++)
            returns[i] = (i % 2 == 0) ? 0.01 : -0.01;

        int window = 21;
        double[] vol = VolatilityModels.RollingVolatility(returns, window);

        Assert.Equal(returns.Length, vol.Length);

        // Elements before the window is full are NaN.
        for (int i = 0; i < window - 1; i++)
            Assert.True(double.IsNaN(vol[i]), "Values before the window is full should be NaN.");

        // Once the window is full, values are real, positive and of a plausible annualised magnitude.
        for (int i = window - 1; i < vol.Length; i++)
        {
            Assert.False(double.IsNaN(vol[i]), "Filled window values must not be NaN.");
            Assert.True(vol[i] > 0, "Constant-vol series should produce positive volatility.");
            Assert.True(vol[i] < 1.0, "Annualised vol of a ~1%/day series should be well under 100%.");
        }
    }

    [Fact]
    public void RollingVolatility_Throws_ForWindowBelowTwo()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => VolatilityModels.RollingVolatility(new double[] { 0.01, 0.02, -0.01 }, window: 1));
    }

    // ── GARCH long-run variance ─────────────────────────────────────────────────

    [Fact]
    public void GarchLongRunVariance_EqualsClosedForm()
    {
        double omega = 0.000002;
        double alpha = 0.05;
        double beta = 0.90;

        double vL = VolatilityModels.GarchLongRunVariance(omega, alpha, beta);

        double expected = omega / (1 - alpha - beta);
        Assert.Equal(expected, vL, 15);
    }

    [Fact]
    public void GarchLongRunVariance_Throws_WhenNonStationary()
    {
        Assert.Throws<ArgumentException>(
            () => VolatilityModels.GarchLongRunVariance(0.0001, alpha: 0.6, beta: 0.5));
    }

    // ── GARCH forecast convergence ──────────────────────────────────────────────

    [Fact]
    public void GarchForecast_ConvergesTowardLongRunVolatility_FromAbove()
    {
        double omega = 0.000002;
        double alpha = 0.05;
        double beta = 0.90;

        double vL = VolatilityModels.GarchLongRunVariance(omega, alpha, beta);
        double annualisedLongRunVol = Math.Sqrt(vL * 252);

        // Start well above the long-run variance.
        double currentVariance = vL * 4.0;

        int[] horizons = { 1, 5, 20, 60, 250 };
        var forecasts = horizons
            .Select(n => VolatilityModels.GarchForecast(currentVariance, omega, alpha, beta, n))
            .ToArray();

        // Starting above V_L, the forecast volatility should decrease monotonically toward it.
        for (int i = 1; i < forecasts.Length; i++)
            Assert.True(forecasts[i] <= forecasts[i - 1] + 1e-12,
                $"Forecast should be non-increasing toward V_L (step {i}).");

        // Every forecast stays above the long-run level when starting above it.
        Assert.All(forecasts, f => Assert.True(f >= annualisedLongRunVol - 1e-9));

        // The far-horizon forecast should be very close to the long-run volatility.
        Assert.Equal(annualisedLongRunVol, forecasts[^1], 4);
    }

    [Fact]
    public void GarchForecast_ConvergesTowardLongRunVolatility_FromBelow()
    {
        double omega = 0.000002;
        double alpha = 0.05;
        double beta = 0.90;

        double vL = VolatilityModels.GarchLongRunVariance(omega, alpha, beta);
        double annualisedLongRunVol = Math.Sqrt(vL * 252);

        // Start below the long-run variance.
        double currentVariance = vL * 0.25;

        int[] horizons = { 1, 5, 20, 60, 250 };
        var forecasts = horizons
            .Select(n => VolatilityModels.GarchForecast(currentVariance, omega, alpha, beta, n))
            .ToArray();

        // Starting below V_L, the forecast should increase monotonically toward it.
        for (int i = 1; i < forecasts.Length; i++)
            Assert.True(forecasts[i] >= forecasts[i - 1] - 1e-12,
                $"Forecast should be non-decreasing toward V_L (step {i}).");

        Assert.All(forecasts, f => Assert.True(f <= annualisedLongRunVol + 1e-9));
        Assert.Equal(annualisedLongRunVol, forecasts[^1], 4);
    }

    [Fact]
    public void GarchForecast_EqualsLongRunVol_WhenCurrentVarianceIsLongRun()
    {
        double omega = 0.000002;
        double alpha = 0.05;
        double beta = 0.90;

        double vL = VolatilityModels.GarchLongRunVariance(omega, alpha, beta);
        double expected = Math.Sqrt(vL * 252);

        // At V_L the forecast is flat at every horizon: V_L + (a+b)^n*(0).
        Assert.Equal(expected, VolatilityModels.GarchForecast(vL, omega, alpha, beta, 1), 12);
        Assert.Equal(expected, VolatilityModels.GarchForecast(vL, omega, alpha, beta, 100), 12);
    }

    // ── GARCH conditional variance series ────────────────────────────────────────

    [Fact]
    public void GarchVolatility_Throws_WhenNonStationary()
    {
        var returns = new double[] { 0.01, -0.02, 0.015, -0.005 };
        Assert.Throws<ArgumentException>(
            () => VolatilityModels.GarchVolatility(returns, omega: 0.0001, alpha: 0.6, beta: 0.5));
    }

    [Fact]
    public void GarchVolatility_FirstValueEqualsAnnualisedLongRunVol()
    {
        var returns = new double[] { 0.01, -0.02, 0.015, -0.005, 0.02, -0.01 };
        double omega = 0.000002, alpha = 0.05, beta = 0.90;

        double[] vol = VolatilityModels.GarchVolatility(returns, omega, alpha, beta);

        double vL = VolatilityModels.GarchLongRunVariance(omega, alpha, beta);
        double expectedFirst = Math.Sqrt(vL * 252);

        Assert.Equal(returns.Length, vol.Length);
        Assert.Equal(expectedFirst, vol[0], 12);
        Assert.All(vol, v => Assert.True(v > 0, "GARCH volatility values must be positive."));
    }
}
