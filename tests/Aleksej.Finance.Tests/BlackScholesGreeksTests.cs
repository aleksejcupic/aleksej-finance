using Aleksej.Finance.Options;

namespace Aleksej.Finance.Tests;

/// <summary>
/// Greek and implied-volatility coverage for <see cref="BlackScholes"/>.
/// Greeks are cross-checked with finite differences derived from the model's own
/// price function, matching each implementation's documented scaling convention
/// (Vega/Rho/Volga per 1% i.e. /100; Theta/Charm per calendar day i.e. /365).
/// </summary>
public class BlackScholesGreeksTests
{
    // Standard textbook case: S=100, K=100, T=1, r=5%, sigma=20%.
    private const double S = 100, K = 100, T = 1, R = 0.05, Sigma = 0.20;

    // ── Delta ──────────────────────────────────────────────────────────────────

    [Fact]
    public void CallDelta_MatchesCentralDifferenceInSpot()
    {
        const double h = 0.01;
        double fd = (BlackScholes.Call(S + h, K, T, R, Sigma) - BlackScholes.Call(S - h, K, T, R, Sigma)) / (2 * h);
        double delta = BlackScholes.Delta(S, K, T, R, Sigma, isPut: false);
        Assert.Equal(fd, delta, 1e-3);
    }

    [Fact]
    public void PutDelta_MatchesCentralDifferenceInSpot()
    {
        const double h = 0.01;
        double fd = (BlackScholes.Put(S + h, K, T, R, Sigma) - BlackScholes.Put(S - h, K, T, R, Sigma)) / (2 * h);
        double delta = BlackScholes.Delta(S, K, T, R, Sigma, isPut: true);
        Assert.Equal(fd, delta, 1e-3);
    }

    [Fact]
    public void PutDelta_IsBetweenMinusOneAndZero()
    {
        double delta = BlackScholes.Delta(S, K, T, R, Sigma, isPut: true);
        Assert.InRange(delta, -1.0, 0.0);
    }

    [Fact]
    public void Delta_AtExpiry_IsZero()
    {
        Assert.Equal(0.0, BlackScholes.Delta(S, K, t: 0, R, Sigma, isPut: false));
        Assert.Equal(0.0, BlackScholes.Delta(S, K, t: 0, R, Sigma, isPut: true));
    }

    // ── Gamma ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Gamma_MatchesSecondDifferenceOfPrice()
    {
        const double h = 0.5;
        double fd = (BlackScholes.Call(S + h, K, T, R, Sigma)
                     - 2 * BlackScholes.Call(S, K, T, R, Sigma)
                     + BlackScholes.Call(S - h, K, T, R, Sigma)) / (h * h);
        double gamma = BlackScholes.Gamma(S, K, T, R, Sigma);
        Assert.Equal(fd, gamma, 1e-2);
    }

    [Fact]
    public void Gamma_IsPositive()
    {
        Assert.True(BlackScholes.Gamma(S, K, T, R, Sigma) > 0);
    }

    [Fact]
    public void Gamma_AtExpiry_IsZero()
    {
        Assert.Equal(0.0, BlackScholes.Gamma(S, K, t: 0, R, Sigma));
    }

    // ── Vega ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Vega_MatchesCentralDifferenceInVol_PerOnePercent()
    {
        // Implementation returns dV/dsigma / 100, so the finite difference is scaled by /100.
        const double dv = 0.0001;
        double fd = (BlackScholes.Call(S, K, T, R, Sigma + dv) - BlackScholes.Call(S, K, T, R, Sigma - dv)) / (2 * dv);
        double vega = BlackScholes.Vega(S, K, T, R, Sigma);
        Assert.Equal(fd / 100, vega, 1e-4);
    }

    [Fact]
    public void Vega_IsPositive()
    {
        Assert.True(BlackScholes.Vega(S, K, T, R, Sigma) > 0);
    }

    [Fact]
    public void Vega_AtExpiry_IsZero()
    {
        Assert.Equal(0.0, BlackScholes.Vega(S, K, t: 0, R, Sigma));
    }

    // ── Theta ──────────────────────────────────────────────────────────────────

    [Fact]
    public void CallTheta_MatchesTimeDifference_PerDay()
    {
        // dV/dt is the change as t increases; calendar decay is -dV/dt, scaled /365.
        const double dt = 1e-5;
        double dPrice_dt = (BlackScholes.Call(S, K, T + dt, R, Sigma) - BlackScholes.Call(S, K, T - dt, R, Sigma)) / (2 * dt);
        double expected = -dPrice_dt / 365;
        double theta = BlackScholes.Theta(S, K, T, R, Sigma, isPut: false);
        Assert.Equal(expected, theta, 1e-4);
    }

    [Fact]
    public void PutTheta_MatchesTimeDifference_PerDay()
    {
        const double dt = 1e-5;
        double dPrice_dt = (BlackScholes.Put(S, K, T + dt, R, Sigma) - BlackScholes.Put(S, K, T - dt, R, Sigma)) / (2 * dt);
        double expected = -dPrice_dt / 365;
        double theta = BlackScholes.Theta(S, K, T, R, Sigma, isPut: true);
        Assert.Equal(expected, theta, 1e-4);
    }

    [Fact]
    public void CallTheta_IsNegative_ForAtmOption()
    {
        Assert.True(BlackScholes.Theta(S, K, T, R, Sigma, isPut: false) < 0);
    }

    // ── Rho ────────────────────────────────────────────────────────────────────

    [Fact]
    public void CallRho_MatchesCentralDifferenceInRate_PerOnePercent()
    {
        const double dr = 1e-6;
        double fd = (BlackScholes.Call(S, K, T, R + dr, Sigma) - BlackScholes.Call(S, K, T, R - dr, Sigma)) / (2 * dr);
        double rho = BlackScholes.Rho(S, K, T, R, Sigma, isPut: false);
        Assert.Equal(fd / 100, rho, 1e-4);
    }

    [Fact]
    public void PutRho_MatchesCentralDifferenceInRate_PerOnePercent()
    {
        const double dr = 1e-6;
        double fd = (BlackScholes.Put(S, K, T, R + dr, Sigma) - BlackScholes.Put(S, K, T, R - dr, Sigma)) / (2 * dr);
        double rho = BlackScholes.Rho(S, K, T, R, Sigma, isPut: true);
        Assert.Equal(fd / 100, rho, 1e-4);
    }

    [Fact]
    public void CallRho_IsPositive_PutRho_IsNegative()
    {
        Assert.True(BlackScholes.Rho(S, K, T, R, Sigma, isPut: false) > 0);
        Assert.True(BlackScholes.Rho(S, K, T, R, Sigma, isPut: true) < 0);
    }

    // ── Higher-order Greeks ──────────────────────────────────────────────────────

    [Fact]
    public void Vanna_MatchesCrossDifferenceOfPrice()
    {
        // Vanna = d^2V/dS dsigma (unscaled). Cross central difference of the call price.
        const double ds = 0.5, dv = 0.001;
        double fd = (BlackScholes.Call(S + ds, K, T, R, Sigma + dv)
                     - BlackScholes.Call(S + ds, K, T, R, Sigma - dv)
                     - BlackScholes.Call(S - ds, K, T, R, Sigma + dv)
                     + BlackScholes.Call(S - ds, K, T, R, Sigma - dv)) / (4 * ds * dv);
        double vanna = BlackScholes.Vanna(S, K, T, R, Sigma);
        Assert.Equal(fd, vanna, 1e-2);
    }

    [Fact]
    public void Charm_MatchesDeltaTimeDifference_PerDay()
    {
        // Charm = -dDelta/dt per calendar day (delta bleed as time passes).
        const double dt = 1e-5;
        double dDelta_dt = (BlackScholes.Delta(S, K, T + dt, R, Sigma, isPut: false)
                            - BlackScholes.Delta(S, K, T - dt, R, Sigma, isPut: false)) / (2 * dt);
        double expected = -dDelta_dt / 365;
        double charm = BlackScholes.Charm(S, K, T, R, Sigma);
        Assert.Equal(expected, charm, 1e-4);
    }

    [Fact]
    public void Volga_MatchesVegaDifferenceInVol()
    {
        // Volga = dVega/dsigma. Vega and Volga both carry the same /100 scaling, so a central
        // difference of the (already-scaled) Vega in sigma should match Volga directly.
        const double dv = 0.001;
        double dVega_dsigma = (BlackScholes.Vega(S, K, T, R, Sigma + dv) - BlackScholes.Vega(S, K, T, R, Sigma - dv)) / (2 * dv);
        double volga = BlackScholes.Volga(S, K, T, R, Sigma);
        Assert.Equal(dVega_dsigma, volga, 1e-3);
    }

    [Fact]
    public void Speed_MatchesGammaDifferenceInSpot()
    {
        // Speed = dGamma/dS.
        const double ds = 0.1;
        double fd = (BlackScholes.Gamma(S + ds, K, T, R, Sigma) - BlackScholes.Gamma(S - ds, K, T, R, Sigma)) / (2 * ds);
        double speed = BlackScholes.Speed(S, K, T, R, Sigma);
        Assert.Equal(fd, speed, 1e-4);
    }

    [Fact]
    public void Zomma_MatchesGammaDifferenceInVol()
    {
        // Zomma = dGamma/dsigma (unscaled).
        const double dv = 0.001;
        double fd = (BlackScholes.Gamma(S, K, T, R, Sigma + dv) - BlackScholes.Gamma(S, K, T, R, Sigma - dv)) / (2 * dv);
        double zomma = BlackScholes.Zomma(S, K, T, R, Sigma);
        Assert.Equal(fd, zomma, 1e-2);
    }

    [Fact]
    public void HigherOrderGreeks_AreFinite()
    {
        Assert.True(double.IsFinite(BlackScholes.Vanna(S, K, T, R, Sigma)));
        Assert.True(double.IsFinite(BlackScholes.Charm(S, K, T, R, Sigma)));
        Assert.True(double.IsFinite(BlackScholes.Volga(S, K, T, R, Sigma)));
        Assert.True(double.IsFinite(BlackScholes.Speed(S, K, T, R, Sigma)));
        Assert.True(double.IsFinite(BlackScholes.Zomma(S, K, T, R, Sigma)));
    }

    [Fact]
    public void HigherOrderGreeks_AtExpiry_AreZero()
    {
        Assert.Equal(0.0, BlackScholes.Vanna(S, K, t: 0, R, Sigma));
        Assert.Equal(0.0, BlackScholes.Charm(S, K, t: 0, R, Sigma));
        Assert.Equal(0.0, BlackScholes.Volga(S, K, t: 0, R, Sigma));
        Assert.Equal(0.0, BlackScholes.Speed(S, K, t: 0, R, Sigma));
        Assert.Equal(0.0, BlackScholes.Zomma(S, K, t: 0, R, Sigma));
    }

    // ── Implied volatility ───────────────────────────────────────────────────────

    [Fact]
    public void ImpliedVolatility_Call_RoundTrips()
    {
        double price = BlackScholes.Call(S, K, T, R, Sigma);
        double iv = BlackScholes.ImpliedVolatility(price, S, K, T, R, isPut: false);
        Assert.Equal(Sigma, iv, 1e-3);
    }

    [Fact]
    public void ImpliedVolatility_Put_RoundTrips()
    {
        double price = BlackScholes.Put(S, K, T, R, Sigma);
        double iv = BlackScholes.ImpliedVolatility(price, S, K, T, R, isPut: true);
        Assert.Equal(Sigma, iv, 1e-3);
    }

    [Fact]
    public void ImpliedVolatility_ReturnsNaN_WhenPriceBelowIntrinsic()
    {
        // A price far below any achievable value has no bracketed root.
        double iv = BlackScholes.ImpliedVolatility(-1.0, S, K, T, R, isPut: false);
        Assert.True(double.IsNaN(iv));
    }
}
