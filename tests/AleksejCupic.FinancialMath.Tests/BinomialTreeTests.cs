using AleksejCupic.FinancialMath.Options;

namespace AleksejCupic.FinancialMath.Tests;

public class BinomialTreeTests
{
    // Standard textbook case: S=100, K=100, T=1, r=5%, sigma=20%.
    private const double S = 100, K = 100, T = 1, R = 0.05, Sigma = 0.20;

    [Fact]
    public void EuropeanCall_ConvergesToBlackScholes()
    {
        double tree = BinomialTree.Price(S, K, T, R, Sigma, steps: 600, isPut: false, isAmerican: false);
        double bs = BlackScholes.Call(S, K, T, R, Sigma);
        Assert.Equal(bs, tree, 0.1);
    }

    [Fact]
    public void EuropeanPut_ConvergesToBlackScholes()
    {
        double tree = BinomialTree.Price(S, K, T, R, Sigma, steps: 600, isPut: true, isAmerican: false);
        double bs = BlackScholes.Put(S, K, T, R, Sigma);
        Assert.Equal(bs, tree, 0.1);
    }

    [Fact]
    public void AmericanCall_NoDividends_EqualsEuropeanCall()
    {
        // Without dividends an American call equals the European call.
        double american = BinomialTree.Price(S, K, T, R, Sigma, steps: 500, isPut: false, isAmerican: true);
        double european = BinomialTree.Price(S, K, T, R, Sigma, steps: 500, isPut: false, isAmerican: false);
        Assert.Equal(european, american, 1e-9);
    }

    [Fact]
    public void AmericanPut_IsAtLeastEuropeanPut()
    {
        // Early exercise can only add value, so American >= European.
        double american = BinomialTree.Price(S, K, T, R, Sigma, steps: 500, isPut: true, isAmerican: true);
        double european = BinomialTree.Price(S, K, T, R, Sigma, steps: 500, isPut: true, isAmerican: false);
        Assert.True(american >= european - 1e-9);
    }

    [Fact]
    public void Prices_ArePositive()
    {
        Assert.True(BinomialTree.Price(S, K, T, R, Sigma, isPut: false) > 0);
        Assert.True(BinomialTree.Price(S, K, T, R, Sigma, isPut: true) > 0);
    }

    [Fact]
    public void Call_IsIncreasingInSpot()
    {
        double low = BinomialTree.Price(90, K, T, R, Sigma, steps: 300, isPut: false);
        double high = BinomialTree.Price(110, K, T, R, Sigma, steps: 300, isPut: false);
        Assert.True(high > low);
    }

    [Fact]
    public void InvalidSteps_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BinomialTree.Price(S, K, T, R, Sigma, steps: 0));
    }

    [Fact]
    public void AtExpiry_ReturnsIntrinsicValue()
    {
        Assert.Equal(Math.Max(S - K, 0), BinomialTree.Price(S, K, t: 0, R, Sigma, isPut: false), 1e-12);
        Assert.Equal(Math.Max(K - S, 0), BinomialTree.Price(S, K, t: 0, R, Sigma, isPut: true), 1e-12);
    }

    [Fact]
    public void CallDelta_IsBetweenZeroAndOne()
    {
        double delta = BinomialTree.Delta(S, K, T, R, Sigma, steps: 300, isPut: false);
        Assert.InRange(delta, 0.0, 1.0);
    }

    [Fact]
    public void Gamma_IsPositive()
    {
        double gamma = BinomialTree.Gamma(S, K, T, R, Sigma, steps: 300, isPut: false);
        Assert.True(gamma > 0);
    }

    [Fact]
    public void Gamma_ConvergesToBlackScholesGamma()
    {
        // BS gamma for S=K=100, T=1, r=5%, sigma=20% is ~0.01876.
        double gamma = BinomialTree.Gamma(S, K, T, R, Sigma, steps: 400, isPut: false);
        Assert.Equal(0.01876, gamma, 3);
    }
}
