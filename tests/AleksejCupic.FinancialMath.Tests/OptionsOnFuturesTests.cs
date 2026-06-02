using AleksejCupic.FinancialMath.Options;

namespace AleksejCupic.FinancialMath.Tests;

public class OptionsOnFuturesTests
{
    // Standard case: futures F=100, K=100, T=1, r=5%, sigma=20%.
    private const double F = 100, K = 100, T = 1, R = 0.05, Sigma = 0.20;

    [Fact]
    public void PutCallParity_Holds()
    {
        // Black's model parity: C - P = e^{-rT} (F - K).
        double call = OptionsOnFutures.Call(F, K, T, R, Sigma);
        double put = OptionsOnFutures.Put(F, K, T, R, Sigma);
        double parity = Math.Exp(-R * T) * (F - K);
        Assert.Equal(parity, call - put, 1e-10);
    }

    [Fact]
    public void CallFromPut_MatchesDirectCall()
    {
        double put = OptionsOnFutures.Put(F, K, T, R, Sigma);
        double call = OptionsOnFutures.Call(F, K, T, R, Sigma);
        Assert.Equal(call, OptionsOnFutures.CallFromPut(put, F, K, T, R), 1e-10);
    }

    [Fact]
    public void PutFromCall_MatchesDirectPut()
    {
        double call = OptionsOnFutures.Call(F, K, T, R, Sigma);
        double put = OptionsOnFutures.Put(F, K, T, R, Sigma);
        Assert.Equal(put, OptionsOnFutures.PutFromCall(call, F, K, T, R), 1e-10);
    }

    [Fact]
    public void AtTheMoney_CallEqualsPut()
    {
        // When F = K, parity gives C - P = 0, so call and put are equal.
        double call = OptionsOnFutures.Call(F, K, T, R, Sigma);
        double put = OptionsOnFutures.Put(F, K, T, R, Sigma);
        Assert.Equal(call, put, 1e-10);
    }

    [Fact]
    public void Prices_ArePositive()
    {
        Assert.True(OptionsOnFutures.Call(F, K, T, R, Sigma) > 0);
        Assert.True(OptionsOnFutures.Put(F, K, T, R, Sigma) > 0);
    }

    [Fact]
    public void Call_IsIncreasingInFuturesPrice()
    {
        double low = OptionsOnFutures.Call(90, K, T, R, Sigma);
        double high = OptionsOnFutures.Call(110, K, T, R, Sigma);
        Assert.True(high > low);
    }

    [Fact]
    public void Put_IsDecreasingInFuturesPrice()
    {
        double low = OptionsOnFutures.Put(90, K, T, R, Sigma);
        double high = OptionsOnFutures.Put(110, K, T, R, Sigma);
        Assert.True(high < low);
    }

    [Fact]
    public void CallDelta_IsBetweenZeroAndDiscountFactor()
    {
        // Call delta = e^{-rT} N(d1) in [0, e^{-rT}].
        double delta = OptionsOnFutures.Delta(F, K, T, R, Sigma, isPut: false);
        Assert.InRange(delta, 0.0, Math.Exp(-R * T));
    }

    [Fact]
    public void Gamma_And_Vega_ArePositive()
    {
        Assert.True(OptionsOnFutures.Gamma(F, K, T, R, Sigma) > 0);
        Assert.True(OptionsOnFutures.Vega(F, K, T, R, Sigma) > 0);
    }

    [Fact]
    public void ImpliedVolatility_RecoversInputVol()
    {
        double price = OptionsOnFutures.Call(F, K, T, R, Sigma);
        double iv = OptionsOnFutures.ImpliedVolatility(price, F, K, T, R, isPut: false);
        Assert.Equal(Sigma, iv, 1e-4);
    }

    [Fact]
    public void AtExpiry_ReturnsIntrinsicValue()
    {
        Assert.Equal(Math.Max(110 - K, 0), OptionsOnFutures.Call(110, K, t: 0, R, Sigma), 1e-12);
        Assert.Equal(Math.Max(K - 90, 0), OptionsOnFutures.Put(90, K, t: 0, R, Sigma), 1e-12);
    }
}
