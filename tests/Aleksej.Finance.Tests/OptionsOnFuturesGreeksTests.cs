using Aleksej.Finance.Options;

namespace Aleksej.Finance.Tests;

/// <summary>
/// Greek and implied-volatility coverage for <see cref="OptionsOnFutures"/> (Black 1976).
/// Greeks are cross-checked with finite differences from the model's own price
/// function, matching documented scaling (Vega/Rho per 1% i.e. /100; Theta per
/// calendar day i.e. /365).
/// </summary>
public class OptionsOnFuturesGreeksTests
{
    // Standard case: futures F=100, K=100, T=1, r=5%, sigma=20%.
    private const double F = 100, K = 100, T = 1, R = 0.05, Sigma = 0.20;

    // ── Delta ──────────────────────────────────────────────────────────────────

    [Fact]
    public void CallDelta_MatchesCentralDifferenceInFutures()
    {
        const double h = 0.01;
        double fd = (OptionsOnFutures.Call(F + h, K, T, R, Sigma) - OptionsOnFutures.Call(F - h, K, T, R, Sigma)) / (2 * h);
        double delta = OptionsOnFutures.Delta(F, K, T, R, Sigma, isPut: false);
        Assert.Equal(fd, delta, 1e-3);
    }

    [Fact]
    public void PutDelta_MatchesCentralDifferenceInFutures()
    {
        const double h = 0.01;
        double fd = (OptionsOnFutures.Put(F + h, K, T, R, Sigma) - OptionsOnFutures.Put(F - h, K, T, R, Sigma)) / (2 * h);
        double delta = OptionsOnFutures.Delta(F, K, T, R, Sigma, isPut: true);
        Assert.Equal(fd, delta, 1e-3);
    }

    [Fact]
    public void PutDelta_IsBetweenMinusDiscountFactorAndZero()
    {
        // Put delta = e^{-rT}(N(d1) - 1) in [-e^{-rT}, 0].
        double delta = OptionsOnFutures.Delta(F, K, T, R, Sigma, isPut: true);
        Assert.InRange(delta, -Math.Exp(-R * T), 0.0);
    }

    // ── Gamma ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Gamma_MatchesSecondDifferenceOfPrice()
    {
        const double h = 0.5;
        double fd = (OptionsOnFutures.Call(F + h, K, T, R, Sigma)
                     - 2 * OptionsOnFutures.Call(F, K, T, R, Sigma)
                     + OptionsOnFutures.Call(F - h, K, T, R, Sigma)) / (h * h);
        double gamma = OptionsOnFutures.Gamma(F, K, T, R, Sigma);
        Assert.Equal(fd, gamma, 1e-2);
    }

    // ── Vega ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Vega_MatchesCentralDifferenceInVol_PerOnePercent()
    {
        const double dv = 1e-4;
        double fd = (OptionsOnFutures.Call(F, K, T, R, Sigma + dv) - OptionsOnFutures.Call(F, K, T, R, Sigma - dv)) / (2 * dv);
        double vega = OptionsOnFutures.Vega(F, K, T, R, Sigma);
        Assert.Equal(fd / 100, vega, 1e-4);
    }

    // ── Theta ──────────────────────────────────────────────────────────────────

    [Fact]
    public void CallTheta_MatchesTimeDifference_PerDay()
    {
        const double dt = 1e-5;
        double dPrice_dt = (OptionsOnFutures.Call(F, K, T + dt, R, Sigma) - OptionsOnFutures.Call(F, K, T - dt, R, Sigma)) / (2 * dt);
        double expected = -dPrice_dt / 365;
        double theta = OptionsOnFutures.Theta(F, K, T, R, Sigma, isPut: false);
        Assert.Equal(expected, theta, 1e-4);
    }

    [Fact]
    public void PutTheta_MatchesTimeDifference_PerDay()
    {
        const double dt = 1e-5;
        double dPrice_dt = (OptionsOnFutures.Put(F, K, T + dt, R, Sigma) - OptionsOnFutures.Put(F, K, T - dt, R, Sigma)) / (2 * dt);
        double expected = -dPrice_dt / 365;
        double theta = OptionsOnFutures.Theta(F, K, T, R, Sigma, isPut: true);
        Assert.Equal(expected, theta, 1e-4);
    }

    // ── Rho ────────────────────────────────────────────────────────────────────

    [Fact]
    public void CallRho_MatchesCentralDifferenceInRate_PerOnePercent()
    {
        const double dr = 1e-6;
        double fd = (OptionsOnFutures.Call(F, K, T, R + dr, Sigma) - OptionsOnFutures.Call(F, K, T, R - dr, Sigma)) / (2 * dr);
        double rho = OptionsOnFutures.Rho(F, K, T, R, Sigma, isPut: false);
        Assert.Equal(fd / 100, rho, 1e-4);
    }

    [Fact]
    public void PutRho_MatchesCentralDifferenceInRate_PerOnePercent()
    {
        const double dr = 1e-6;
        double fd = (OptionsOnFutures.Put(F, K, T, R + dr, Sigma) - OptionsOnFutures.Put(F, K, T, R - dr, Sigma)) / (2 * dr);
        double rho = OptionsOnFutures.Rho(F, K, T, R, Sigma, isPut: true);
        Assert.Equal(fd / 100, rho, 1e-4);
    }

    [Fact]
    public void Rho_IsNegative_ForBothCallAndPut()
    {
        // For futures options rho = -T * price / 100, always negative for positive prices.
        Assert.True(OptionsOnFutures.Rho(F, K, T, R, Sigma, isPut: false) < 0);
        Assert.True(OptionsOnFutures.Rho(F, K, T, R, Sigma, isPut: true) < 0);
    }

    // ── At-expiry edge cases ───────────────────────────────────────────────────────

    [Fact]
    public void Greeks_AtExpiry_AreZero()
    {
        Assert.Equal(0.0, OptionsOnFutures.Delta(F, K, t: 0, R, Sigma, isPut: false));
        Assert.Equal(0.0, OptionsOnFutures.Gamma(F, K, t: 0, R, Sigma));
        Assert.Equal(0.0, OptionsOnFutures.Vega(F, K, t: 0, R, Sigma));
        Assert.Equal(0.0, OptionsOnFutures.Theta(F, K, t: 0, R, Sigma, isPut: false));
        Assert.Equal(0.0, OptionsOnFutures.Rho(F, K, t: 0, R, Sigma, isPut: false));
    }

    // ── Implied volatility ───────────────────────────────────────────────────────

    [Fact]
    public void ImpliedVolatility_Put_RoundTrips()
    {
        double price = OptionsOnFutures.Put(F, K, T, R, Sigma);
        double iv = OptionsOnFutures.ImpliedVolatility(price, F, K, T, R, isPut: true);
        Assert.Equal(Sigma, iv, 1e-3);
    }

    [Fact]
    public void ImpliedVolatility_ReturnsNaN_WhenUnsolvable()
    {
        double iv = OptionsOnFutures.ImpliedVolatility(-1.0, F, K, T, R, isPut: false);
        Assert.True(double.IsNaN(iv));
    }
}
