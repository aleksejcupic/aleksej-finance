using AleksejCupic.FinancialMath.Options;

namespace AleksejCupic.FinancialMath.Tests;

public class MonteCarloTests
{
    private const double S = 100, K = 100, T = 1, R = 0.05, Sigma = 0.20;

    [Fact]
    public void EuropeanCall_ConvergesToBlackScholes()
    {
        double analytic = BlackScholes.Call(S, K, T, R, Sigma);
        double simulated = MonteCarlo.EuropeanPrice(
            S, K, T, R, Sigma, paths: 200_000, steps: 50, isPut: false, seed: 42);

        // Monte Carlo is stochastic; allow a tolerance around the closed-form value.
        Assert.Equal(analytic, simulated, 0.3);
    }

    [Fact]
    public void EuropeanPut_ConvergesToBlackScholes()
    {
        double analytic = BlackScholes.Put(S, K, T, R, Sigma);
        double simulated = MonteCarlo.EuropeanPrice(
            S, K, T, R, Sigma, paths: 200_000, steps: 50, isPut: true, seed: 42);

        Assert.Equal(analytic, simulated, 0.3);
    }
}
