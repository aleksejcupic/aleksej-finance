using Aleksej.Finance.Risk;

namespace Aleksej.Finance.Tests;

public class RiskMetricsTests
{
    [Fact]
    public void MaxDrawdown_OnHandComputedSeries()
    {
        // Returns: +10%, -20%, +5%.
        // Equity: 1.0 -> 1.10 -> 0.88 -> 0.924. Peak 1.10, trough 0.88.
        // Max drawdown = (1.10 - 0.88) / 1.10 = 0.20.
        double[] returns = { 0.10, -0.20, 0.05 };
        double dd = RiskMetrics.MaxDrawdown(returns);
        Assert.Equal(0.20, dd, 6);
    }

    [Fact]
    public void Sharpe_IsPositive_WhenReturnsBeatRiskFree()
    {
        // Steady positive daily returns with some variation, zero risk-free rate.
        double[] returns = { 0.001, 0.002, 0.0015, 0.0005, 0.0025, 0.001 };
        double sharpe = RiskMetrics.SharpeRatio(returns, riskFreeRateAnnual: 0.0);
        Assert.True(sharpe > 0);
    }

    [Fact]
    public void MaxDrawdown_IsZero_ForMonotonicGains()
    {
        double[] returns = { 0.01, 0.02, 0.005, 0.03 };
        Assert.Equal(0.0, RiskMetrics.MaxDrawdown(returns), 12);
    }
}
