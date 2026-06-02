using Aleksej.Finance.Options;

namespace Aleksej.Finance.Tests;

public class BlackScholesTests
{
    // Standard textbook case: S=100, K=100, T=1, r=5%, sigma=20%.
    private const double S = 100, K = 100, T = 1, R = 0.05, Sigma = 0.20;

    [Fact]
    public void Call_MatchesKnownReferenceValue()
    {
        double call = BlackScholes.Call(S, K, T, R, Sigma);
        Assert.Equal(10.4506, call, 3);
    }

    [Fact]
    public void Put_MatchesKnownReferenceValue()
    {
        double put = BlackScholes.Put(S, K, T, R, Sigma);
        Assert.Equal(5.5735, put, 3);
    }

    [Fact]
    public void PutCallParity_Holds()
    {
        double call = BlackScholes.Call(S, K, T, R, Sigma);
        double put = BlackScholes.Put(S, K, T, R, Sigma);
        double parity = S - K * Math.Exp(-R * T); // C - P = S - K e^{-rT}
        Assert.Equal(parity, call - put, 6);
    }

    [Fact]
    public void CallDelta_IsBetweenZeroAndOne()
    {
        double delta = BlackScholes.Delta(S, K, T, R, Sigma, isPut: false);
        Assert.InRange(delta, 0.0, 1.0);
    }
}
