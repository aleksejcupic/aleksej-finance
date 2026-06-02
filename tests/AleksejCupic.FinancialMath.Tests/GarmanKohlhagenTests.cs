using AleksejCupic.FinancialMath.Options;

namespace AleksejCupic.FinancialMath.Tests;

public class GarmanKohlhagenTests
{
    // Standard FX case: spot=1.20, strike=1.25, T=1, domestic r=5%, foreign rf=3%, sigma=15%.
    private const double S = 1.20, K = 1.25, T = 1.0, R = 0.05, Rf = 0.03, Sigma = 0.15;

    [Fact]
    public void Call_WithZeroForeignRate_MatchesBlackScholes()
    {
        // With rf = 0 the Garman-Kohlhagen model reduces exactly to Black-Scholes.
        double gk = GarmanKohlhagen.Call(S, K, T, R, rf: 0.0, Sigma);
        double bs = BlackScholes.Call(S, K, T, R, Sigma);
        Assert.Equal(bs, gk, 1e-10);
    }

    [Fact]
    public void Put_WithZeroForeignRate_MatchesBlackScholes()
    {
        double gk = GarmanKohlhagen.Put(S, K, T, R, rf: 0.0, Sigma);
        double bs = BlackScholes.Put(S, K, T, R, Sigma);
        Assert.Equal(bs, gk, 1e-10);
    }

    [Fact]
    public void PutCallParity_Holds()
    {
        // FX put-call parity: C - P = S e^{-rf T} - K e^{-r T}.
        double call = GarmanKohlhagen.Call(S, K, T, R, Rf, Sigma);
        double put = GarmanKohlhagen.Put(S, K, T, R, Rf, Sigma);
        double parity = S * Math.Exp(-Rf * T) - K * Math.Exp(-R * T);
        Assert.Equal(parity, call - put, 1e-10);
    }

    [Fact]
    public void Prices_ArePositive()
    {
        Assert.True(GarmanKohlhagen.Call(S, K, T, R, Rf, Sigma) > 0);
        Assert.True(GarmanKohlhagen.Put(S, K, T, R, Rf, Sigma) > 0);
    }

    [Fact]
    public void Call_IsIncreasingInSpot()
    {
        double low = GarmanKohlhagen.Call(1.10, K, T, R, Rf, Sigma);
        double high = GarmanKohlhagen.Call(1.30, K, T, R, Rf, Sigma);
        Assert.True(high > low);
    }

    [Fact]
    public void CallDelta_IsBetweenZeroAndOne()
    {
        double delta = GarmanKohlhagen.Delta(S, K, T, R, Rf, Sigma, isPut: false);
        Assert.InRange(delta, 0.0, 1.0);
    }

    [Fact]
    public void PutDelta_IsBetweenMinusOneAndZero()
    {
        double delta = GarmanKohlhagen.Delta(S, K, T, R, Rf, Sigma, isPut: true);
        Assert.InRange(delta, -1.0, 0.0);
    }

    [Fact]
    public void Gamma_And_Vega_ArePositive()
    {
        Assert.True(GarmanKohlhagen.Gamma(S, K, T, R, Rf, Sigma) > 0);
        Assert.True(GarmanKohlhagen.Vega(S, K, T, R, Rf, Sigma) > 0);
    }

    [Fact]
    public void ImpliedVolatility_RecoversInputVol()
    {
        double price = GarmanKohlhagen.Call(S, K, T, R, Rf, Sigma);
        double iv = GarmanKohlhagen.ImpliedVolatility(price, S, K, T, R, Rf, isPut: false);
        Assert.Equal(Sigma, iv, 1e-4);
    }

    [Fact]
    public void AtExpiry_ReturnsIntrinsicValue()
    {
        Assert.Equal(Math.Max(S - K, 0), GarmanKohlhagen.Call(S, K, t: 0, R, Rf, Sigma), 1e-12);
        Assert.Equal(Math.Max(K - S, 0), GarmanKohlhagen.Put(S, K, t: 0, R, Rf, Sigma), 1e-12);
    }
}
