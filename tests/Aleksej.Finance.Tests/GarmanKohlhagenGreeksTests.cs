using Aleksej.Finance.Options;

namespace Aleksej.Finance.Tests;

/// <summary>
/// Greek and implied-volatility coverage for <see cref="GarmanKohlhagen"/>.
/// Greeks are cross-checked with finite differences from the model's own price
/// function, matching documented scaling (Vega/Rho/RhoForeign per 1% i.e. /100;
/// Theta per calendar day i.e. /365).
/// </summary>
public class GarmanKohlhagenGreeksTests
{
    // Standard FX case: spot=1.20, strike=1.25, T=1, domestic r=5%, foreign rf=3%, sigma=15%.
    private const double S = 1.20, K = 1.25, T = 1.0, R = 0.05, Rf = 0.03, Sigma = 0.15;

    // ── Delta ──────────────────────────────────────────────────────────────────

    [Fact]
    public void CallDelta_MatchesCentralDifferenceInSpot()
    {
        const double h = 1e-4;
        double fd = (GarmanKohlhagen.Call(S + h, K, T, R, Rf, Sigma) - GarmanKohlhagen.Call(S - h, K, T, R, Rf, Sigma)) / (2 * h);
        double delta = GarmanKohlhagen.Delta(S, K, T, R, Rf, Sigma, isPut: false);
        Assert.Equal(fd, delta, 1e-4);
    }

    [Fact]
    public void PutDelta_MatchesCentralDifferenceInSpot()
    {
        const double h = 1e-4;
        double fd = (GarmanKohlhagen.Put(S + h, K, T, R, Rf, Sigma) - GarmanKohlhagen.Put(S - h, K, T, R, Rf, Sigma)) / (2 * h);
        double delta = GarmanKohlhagen.Delta(S, K, T, R, Rf, Sigma, isPut: true);
        Assert.Equal(fd, delta, 1e-4);
    }

    // ── Gamma ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Gamma_MatchesSecondDifferenceOfPrice()
    {
        const double h = 0.005;
        double fd = (GarmanKohlhagen.Call(S + h, K, T, R, Rf, Sigma)
                     - 2 * GarmanKohlhagen.Call(S, K, T, R, Rf, Sigma)
                     + GarmanKohlhagen.Call(S - h, K, T, R, Rf, Sigma)) / (h * h);
        double gamma = GarmanKohlhagen.Gamma(S, K, T, R, Rf, Sigma);
        Assert.Equal(fd, gamma, 1e-2);
    }

    // ── Vega ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Vega_MatchesCentralDifferenceInVol_PerOnePercent()
    {
        const double dv = 1e-4;
        double fd = (GarmanKohlhagen.Call(S, K, T, R, Rf, Sigma + dv) - GarmanKohlhagen.Call(S, K, T, R, Rf, Sigma - dv)) / (2 * dv);
        double vega = GarmanKohlhagen.Vega(S, K, T, R, Rf, Sigma);
        Assert.Equal(fd / 100, vega, 1e-5);
    }

    // ── Theta ──────────────────────────────────────────────────────────────────

    [Fact]
    public void CallTheta_MatchesTimeDifference_PerDay()
    {
        const double dt = 1e-6;
        double dPrice_dt = (GarmanKohlhagen.Call(S, K, T + dt, R, Rf, Sigma) - GarmanKohlhagen.Call(S, K, T - dt, R, Rf, Sigma)) / (2 * dt);
        double expected = -dPrice_dt / 365;
        double theta = GarmanKohlhagen.Theta(S, K, T, R, Rf, Sigma, isPut: false);
        Assert.Equal(expected, theta, 1e-5);
    }

    [Fact]
    public void PutTheta_MatchesTimeDifference_PerDay()
    {
        const double dt = 1e-6;
        double dPrice_dt = (GarmanKohlhagen.Put(S, K, T + dt, R, Rf, Sigma) - GarmanKohlhagen.Put(S, K, T - dt, R, Rf, Sigma)) / (2 * dt);
        double expected = -dPrice_dt / 365;
        double theta = GarmanKohlhagen.Theta(S, K, T, R, Rf, Sigma, isPut: true);
        Assert.Equal(expected, theta, 1e-5);
    }

    // ── Rho (domestic) ───────────────────────────────────────────────────────────

    [Fact]
    public void CallRho_MatchesCentralDifferenceInDomesticRate_PerOnePercent()
    {
        const double dr = 1e-6;
        double fd = (GarmanKohlhagen.Call(S, K, T, R + dr, Rf, Sigma) - GarmanKohlhagen.Call(S, K, T, R - dr, Rf, Sigma)) / (2 * dr);
        double rho = GarmanKohlhagen.Rho(S, K, T, R, Rf, Sigma, isPut: false);
        Assert.Equal(fd / 100, rho, 1e-5);
    }

    [Fact]
    public void PutRho_MatchesCentralDifferenceInDomesticRate_PerOnePercent()
    {
        const double dr = 1e-6;
        double fd = (GarmanKohlhagen.Put(S, K, T, R + dr, Rf, Sigma) - GarmanKohlhagen.Put(S, K, T, R - dr, Rf, Sigma)) / (2 * dr);
        double rho = GarmanKohlhagen.Rho(S, K, T, R, Rf, Sigma, isPut: true);
        Assert.Equal(fd / 100, rho, 1e-5);
    }

    // ── Rho (foreign) ────────────────────────────────────────────────────────────

    [Fact]
    public void CallRhoForeign_MatchesCentralDifferenceInForeignRate_PerOnePercent()
    {
        const double drf = 1e-6;
        double fd = (GarmanKohlhagen.Call(S, K, T, R, Rf + drf, Sigma) - GarmanKohlhagen.Call(S, K, T, R, Rf - drf, Sigma)) / (2 * drf);
        double rhoF = GarmanKohlhagen.RhoForeign(S, K, T, R, Rf, Sigma, isPut: false);
        Assert.Equal(fd / 100, rhoF, 1e-5);
    }

    [Fact]
    public void PutRhoForeign_MatchesCentralDifferenceInForeignRate_PerOnePercent()
    {
        const double drf = 1e-6;
        double fd = (GarmanKohlhagen.Put(S, K, T, R, Rf + drf, Sigma) - GarmanKohlhagen.Put(S, K, T, R, Rf - drf, Sigma)) / (2 * drf);
        double rhoF = GarmanKohlhagen.RhoForeign(S, K, T, R, Rf, Sigma, isPut: true);
        Assert.Equal(fd / 100, rhoF, 1e-5);
    }

    [Fact]
    public void CallRhoForeign_IsNegative_PutRhoForeign_IsPositive()
    {
        // Higher foreign rate lowers the discounted spot leg: call rho_f < 0, put rho_f > 0.
        Assert.True(GarmanKohlhagen.RhoForeign(S, K, T, R, Rf, Sigma, isPut: false) < 0);
        Assert.True(GarmanKohlhagen.RhoForeign(S, K, T, R, Rf, Sigma, isPut: true) > 0);
    }

    // ── At-expiry edge cases ───────────────────────────────────────────────────────

    [Fact]
    public void Greeks_AtExpiry_AreZero()
    {
        Assert.Equal(0.0, GarmanKohlhagen.Delta(S, K, t: 0, R, Rf, Sigma, isPut: false));
        Assert.Equal(0.0, GarmanKohlhagen.Gamma(S, K, t: 0, R, Rf, Sigma));
        Assert.Equal(0.0, GarmanKohlhagen.Vega(S, K, t: 0, R, Rf, Sigma));
        Assert.Equal(0.0, GarmanKohlhagen.Theta(S, K, t: 0, R, Rf, Sigma, isPut: false));
        Assert.Equal(0.0, GarmanKohlhagen.Rho(S, K, t: 0, R, Rf, Sigma, isPut: false));
        Assert.Equal(0.0, GarmanKohlhagen.RhoForeign(S, K, t: 0, R, Rf, Sigma, isPut: false));
    }

    // ── Implied volatility ───────────────────────────────────────────────────────

    [Fact]
    public void ImpliedVolatility_Put_RoundTrips()
    {
        double price = GarmanKohlhagen.Put(S, K, T, R, Rf, Sigma);
        double iv = GarmanKohlhagen.ImpliedVolatility(price, S, K, T, R, Rf, isPut: true);
        Assert.Equal(Sigma, iv, 1e-3);
    }

    [Fact]
    public void ImpliedVolatility_ReturnsNaN_WhenUnsolvable()
    {
        double iv = GarmanKohlhagen.ImpliedVolatility(-1.0, S, K, T, R, Rf, isPut: false);
        Assert.True(double.IsNaN(iv));
    }
}
