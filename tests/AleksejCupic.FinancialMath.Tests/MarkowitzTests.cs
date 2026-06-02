using AleksejCupic.FinancialMath.Portfolio;

namespace AleksejCupic.FinancialMath.Tests;

public class MarkowitzTests
{
    [Fact]
    public void MinVariancePortfolio_WeightsSumToOne()
    {
        // Simple 2-asset covariance matrix.
        double[,] cov =
        {
            { 0.04, 0.01 },
            { 0.01, 0.09 },
        };

        double[] weights = Markowitz.MinVariancePortfolio(cov);

        double sum = 0;
        foreach (double w in weights) sum += w;
        Assert.Equal(1.0, sum, 8);
    }

    [Fact]
    public void MinVariancePortfolio_FavoursLowerVarianceAsset()
    {
        // Asset 0 has lower variance, so it should carry the larger weight.
        double[,] cov =
        {
            { 0.04, 0.00 },
            { 0.00, 0.16 },
        };

        double[] weights = Markowitz.MinVariancePortfolio(cov);
        Assert.True(weights[0] > weights[1]);
    }
}
