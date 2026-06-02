using AleksejCupic.FinancialMath.Portfolio;

namespace AleksejCupic.FinancialMath.Tests;

public class EquityMetricsTests
{
    [Fact]
    public void MarketCap_IsSharesTimesPrice()
    {
        Assert.Equal(1_000_000.0, EquityMetrics.MarketCap(10_000, 100), 6);
    }

    [Fact]
    public void EnterpriseValue_AddsDebtSubtractsCash()
    {
        // EV = marketCap + totalDebt - cash
        Assert.Equal(1_200_000.0, EquityMetrics.EnterpriseValue(1_000_000, 300_000, 100_000), 6);
    }

    [Fact]
    public void PositionValue_IsSharesTimesPrice()
    {
        Assert.Equal(2_500.0, EquityMetrics.PositionValue(50, 50), 6);
    }

    [Fact]
    public void PortfolioMarketValue_SumsPositionValues()
    {
        double[] positions = { 10, 20, 5 };
        double[] prices = { 100, 50, 200 };
        // 1000 + 1000 + 1000
        Assert.Equal(3000.0, EquityMetrics.PortfolioMarketValue(positions, prices), 6);
    }

    [Fact]
    public void PortfolioMarketValue_ThrowsOnLengthMismatch()
    {
        Assert.Throws<ArgumentException>(() =>
            EquityMetrics.PortfolioMarketValue(new double[] { 1, 2 }, new double[] { 1 }));
    }

    [Fact]
    public void PortfolioWeight_IsPositionOverTotal()
    {
        Assert.Equal(0.25, EquityMetrics.PortfolioWeight(250, 1000), 8);
    }

    [Fact]
    public void PortfolioWeight_ZeroTotalReturnsZero()
    {
        Assert.Equal(0.0, EquityMetrics.PortfolioWeight(250, 0), 8);
    }

    [Fact]
    public void UnrealizedPnL_IsGainTimesShares()
    {
        // (120 - 100) * 10
        Assert.Equal(200.0, EquityMetrics.UnrealizedPnL(10, 100, 120), 6);
    }

    [Fact]
    public void RealizedPnL_IsGainTimesShares()
    {
        // (90 - 100) * 10 = -100 (a loss)
        Assert.Equal(-100.0, EquityMetrics.RealizedPnL(10, 100, 90), 6);
    }

    [Fact]
    public void PercentReturn_KnownValue()
    {
        // (110 - 100) / 100
        Assert.Equal(0.10, EquityMetrics.PercentReturn(100, 110), 8);
    }

    [Fact]
    public void PercentReturn_ZeroCostBasisReturnsZero()
    {
        Assert.Equal(0.0, EquityMetrics.PercentReturn(0, 110), 8);
    }

    [Fact]
    public void PriceToEarnings_KnownValue()
    {
        Assert.Equal(20.0, EquityMetrics.PriceToEarnings(100, 5), 8);
    }

    [Fact]
    public void PriceToEarnings_NonPositiveEpsIsNaN()
    {
        Assert.True(double.IsNaN(EquityMetrics.PriceToEarnings(100, 0)));
        Assert.True(double.IsNaN(EquityMetrics.PriceToEarnings(100, -5)));
    }

    [Fact]
    public void EarningsYield_IsInverseOfPe()
    {
        double pe = EquityMetrics.PriceToEarnings(100, 5);
        double ey = EquityMetrics.EarningsYield(5, 100);
        Assert.Equal(1.0 / pe, ey, 8);
    }

    [Fact]
    public void PriceToBook_KnownValue()
    {
        Assert.Equal(2.5, EquityMetrics.PriceToBook(50, 20), 8);
    }

    [Fact]
    public void PriceToBook_ZeroBookIsNaN()
    {
        Assert.True(double.IsNaN(EquityMetrics.PriceToBook(50, 0)));
    }

    [Fact]
    public void PriceToSales_KnownValue()
    {
        Assert.Equal(4.0, EquityMetrics.PriceToSales(2000, 500), 8);
    }

    [Fact]
    public void EvToEbitda_KnownValue()
    {
        Assert.Equal(10.0, EquityMetrics.EvToEbitda(1000, 100), 8);
    }

    [Fact]
    public void EvToEbit_KnownValue()
    {
        Assert.Equal(12.5, EquityMetrics.EvToEbit(1000, 80), 8);
    }

    [Fact]
    public void DividendYield_KnownValue()
    {
        Assert.Equal(0.04, EquityMetrics.DividendYield(2, 50), 8);
    }

    [Fact]
    public void DividendYield_ZeroPriceIsNaN()
    {
        Assert.True(double.IsNaN(EquityMetrics.DividendYield(2, 0)));
    }

    [Fact]
    public void KellyCriterion_KnownFormula()
    {
        // f* = mu / sigma^2 = 0.10 / 0.04 = 2.5
        Assert.Equal(2.5, EquityMetrics.KellyCriterion(0.10, 0.20), 8);
    }

    [Fact]
    public void KellyCriterion_ZeroSigmaIsNaN()
    {
        Assert.True(double.IsNaN(EquityMetrics.KellyCriterion(0.10, 0)));
    }

    [Fact]
    public void KellyCriterionDiscrete_KnownFormula()
    {
        // f* = (b*p - q) / b, b = 2, p = 0.6, q = 0.4
        // = (1.2 - 0.4) / 2 = 0.4
        Assert.Equal(0.4, EquityMetrics.KellyCriterionDiscrete(0.6, 2), 8);
    }

    [Fact]
    public void HalfKelly_IsHalfOfFullKelly()
    {
        double full = EquityMetrics.KellyCriterion(0.10, 0.20);
        Assert.Equal(full / 2, EquityMetrics.HalfKelly(0.10, 0.20), 8);
    }
}
