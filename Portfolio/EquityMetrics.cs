using System;

namespace AleksejCupic.FinancialMath.Portfolio;

/// <summary>
/// Equity valuation multiples, portfolio market value calculations, and position sizing.
/// All methods are pure arithmetic — no market data fetching.
/// </summary>
public static class EquityMetrics
{
    // ── Market value ──────────────────────────────────────────────────────────

    /// <summary>
    /// Market capitalisation: shares outstanding × price.
    /// </summary>
    public static double MarketCap(double sharesOutstanding, double price) =>
        sharesOutstanding * price;

    /// <summary>
    /// Enterprise value: EV = Market Cap + Total Debt − Cash and equivalents.
    /// Represents the theoretical takeover price.
    /// </summary>
    public static double EnterpriseValue(double marketCap, double totalDebt, double cashAndEquivalents) =>
        marketCap + totalDebt - cashAndEquivalents;

    /// <summary>
    /// Dollar value of a single position: shares × price.
    /// </summary>
    public static double PositionValue(double shares, double price) =>
        shares * price;

    /// <summary>
    /// Total portfolio market value: Σ(positions_i × prices_i).
    /// </summary>
    /// <param name="positions">Number of shares / units held per position</param>
    /// <param name="prices">Current price per unit per position (same length as positions)</param>
    public static double PortfolioMarketValue(double[] positions, double[] prices)
    {
        if (positions.Length != prices.Length)
            throw new ArgumentException("positions and prices must be the same length.");
        double total = 0;
        for (int i = 0; i < positions.Length; i++) total += positions[i] * prices[i];
        return total;
    }

    // ── Portfolio weights & P&L ───────────────────────────────────────────────

    /// <summary>
    /// Portfolio weight of a single position: positionValue / totalPortfolioValue.
    /// </summary>
    public static double PortfolioWeight(double positionValue, double totalPortfolioValue) =>
        totalPortfolioValue == 0 ? 0 : positionValue / totalPortfolioValue;

    /// <summary>
    /// Unrealised P&amp;L on a position: (currentPrice − avgCostBasis) × shares.
    /// Positive = gain, negative = loss.
    /// </summary>
    public static double UnrealizedPnL(double shares, double avgCostBasis, double currentPrice) =>
        (currentPrice - avgCostBasis) * shares;

    /// <summary>
    /// Realised P&amp;L on a closed position: (salePrice − avgCostBasis) × shares.
    /// </summary>
    public static double RealizedPnL(double shares, double avgCostBasis, double salePrice) =>
        (salePrice - avgCostBasis) * shares;

    /// <summary>
    /// Simple percentage return: (currentValue − costBasis) / costBasis.
    /// </summary>
    public static double PercentReturn(double costBasis, double currentValue) =>
        costBasis == 0 ? 0 : (currentValue - costBasis) / costBasis;

    // ── Valuation multiples ───────────────────────────────────────────────────

    /// <summary>
    /// Price-to-Earnings ratio: price / EPS.
    /// Returns NaN if EPS ≤ 0 (loss-making company).
    /// </summary>
    public static double PriceToEarnings(double price, double eps) =>
        eps <= 0 ? double.NaN : price / eps;

    /// <summary>
    /// Earnings yield (inverse P/E): EPS / price.
    /// Useful for comparing to bond yields.
    /// </summary>
    public static double EarningsYield(double eps, double price) =>
        price == 0 ? double.NaN : eps / price;

    /// <summary>
    /// Price-to-Book ratio: price / book value per share.
    /// </summary>
    public static double PriceToBook(double price, double bookValuePerShare) =>
        bookValuePerShare == 0 ? double.NaN : price / bookValuePerShare;

    /// <summary>
    /// Price-to-Sales ratio: market cap / annual revenue.
    /// </summary>
    public static double PriceToSales(double marketCap, double revenue) =>
        revenue == 0 ? double.NaN : marketCap / revenue;

    /// <summary>
    /// EV/EBITDA multiple: enterprise value / EBITDA.
    /// A key multiple for M&amp;A and LBO analysis.
    /// </summary>
    public static double EvToEbitda(double ev, double ebitda) =>
        ebitda == 0 ? double.NaN : ev / ebitda;

    /// <summary>
    /// EV/EBIT multiple: enterprise value / EBIT (operating income).
    /// </summary>
    public static double EvToEbit(double ev, double ebit) =>
        ebit == 0 ? double.NaN : ev / ebit;

    /// <summary>
    /// Dividend yield: annual dividend per share / price.
    /// </summary>
    public static double DividendYield(double annualDividendPerShare, double price) =>
        price == 0 ? double.NaN : annualDividendPerShare / price;

    // ── Position sizing ───────────────────────────────────────────────────────

    /// <summary>
    /// Continuous Kelly criterion — optimal fraction of capital to allocate.
    /// f* = μ / σ²
    /// where μ is the expected return and σ is the return volatility (both annualised).
    /// This is the Sharpe ratio divided by σ.
    /// </summary>
    /// <param name="mu">Expected annualised return (e.g. 0.10 = 10%)</param>
    /// <param name="sigma">Annualised volatility</param>
    public static double KellyCriterion(double mu, double sigma) =>
        sigma == 0 ? double.NaN : mu / (sigma * sigma);

    /// <summary>
    /// Discrete Kelly criterion for binary win/loss bets.
    /// f* = (b·p − q) / b  where b = odds, p = win probability, q = 1 − p.
    /// </summary>
    /// <param name="winProb">Probability of winning (0 to 1)</param>
    /// <param name="winLossRatio">
    ///   Ratio of gain to loss: b = |win amount| / |loss amount|.
    ///   For a 2:1 bet (win $2, lose $1), b = 2.
    /// </param>
    public static double KellyCriterionDiscrete(double winProb, double winLossRatio)
    {
        double q = 1 - winProb;
        return winLossRatio == 0 ? double.NaN : (winLossRatio * winProb - q) / winLossRatio;
    }

    /// <summary>
    /// Half-Kelly — conservative position size at 50% of the full Kelly fraction.
    /// Reduces variance significantly while capturing most of the long-run growth.
    /// f_half = μ / (2σ²)
    /// </summary>
    public static double HalfKelly(double mu, double sigma) =>
        KellyCriterion(mu, sigma) / 2;
}
