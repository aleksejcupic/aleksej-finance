using System;

namespace Aleksej.Finance.Portfolio;

/// <summary>
/// Fee calculations for hedge funds, private equity, and managed accounts.
/// Covers management fees, performance fees with high-water mark,
/// carried interest, expense ratio drag, and transaction costs.
/// </summary>
public static class FeeCalculations
{
    // ── Management fees ───────────────────────────────────────────────────────

    /// <summary>
    /// Management fee accrued over a period.
    /// fee = AUM × annualFeeRate × (daysInPeriod / daysInYear)
    /// </summary>
    /// <param name="aum">Assets under management (beginning of period)</param>
    /// <param name="annualFeeRate">Annual management fee rate (e.g. 0.02 = 2%)</param>
    /// <param name="daysInPeriod">Number of days in the accrual period</param>
    /// <param name="daysInYear">Day count convention (default 365)</param>
    public static double ManagementFee(
        double aum, double annualFeeRate, double daysInPeriod, double daysInYear = 365) =>
        aum * annualFeeRate * (daysInPeriod / daysInYear);

    /// <summary>
    /// Full-year management fee.
    /// fee = AUM × annualFeeRate
    /// </summary>
    public static double ManagementFeeAnnual(double aum, double annualFeeRate) =>
        aum * annualFeeRate;

    // ── Performance fees (hedge fund style) ──────────────────────────────────

    /// <summary>
    /// Performance fee for a period given the current NAV and high-water mark.
    /// The fee is charged only on gains above the hurdle and high-water mark.
    /// fee = max(0, (NAV − effectiveHwm) × carryRate)
    /// where effectiveHwm = max(highWaterMark, previousNAV × (1 + hurdleRate))
    /// </summary>
    /// <param name="currentNav">Current net asset value per share (or total)</param>
    /// <param name="highWaterMark">Previous peak NAV (the high-water mark)</param>
    /// <param name="previousNav">NAV at the start of the performance period</param>
    /// <param name="carryRate">Performance fee rate (default 0.20 = 20%)</param>
    /// <param name="hurdleRate">Minimum return before carry kicks in (default 0 = no hurdle)</param>
    public static double PerformanceFee(
        double currentNav, double highWaterMark, double previousNav,
        double carryRate = 0.20, double hurdleRate = 0.0)
    {
        double effectiveHwm = Math.Max(highWaterMark, previousNav * (1 + hurdleRate));
        double profit       = currentNav - effectiveHwm;
        return Math.Max(0, profit * carryRate);
    }

    /// <summary>
    /// NAV after deducting the performance fee.
    /// </summary>
    public static double NetNavAfterPerformanceFee(
        double currentNav, double highWaterMark, double previousNav,
        double carryRate = 0.20, double hurdleRate = 0.0)
    {
        double fee = PerformanceFee(currentNav, highWaterMark, previousNav, carryRate, hurdleRate);
        return currentNav - fee;
    }

    /// <summary>
    /// High-water mark — the running maximum NAV over a series.
    /// The performance fee is only charged on gains above this level.
    /// </summary>
    /// <param name="navSeries">NAV values in chronological order</param>
    public static double HighWaterMark(double[] navSeries)
    {
        if (navSeries.Length == 0) return 0;
        double hwm = navSeries[0];
        foreach (double nav in navSeries) if (nav > hwm) hwm = nav;
        return hwm;
    }

    // ── Private equity carried interest ───────────────────────────────────────

    /// <summary>
    /// Carried interest for a private equity fund.
    /// GP receives carryRate of profits above the preferred return hurdle.
    /// carry = carryRate × max(0, totalDistributions − investedCapital × (1 + preferredReturn))
    /// </summary>
    /// <param name="totalDistributions">Total cash returned to LPs</param>
    /// <param name="investedCapital">Total capital invested by LPs</param>
    /// <param name="preferredReturn">Hurdle rate (preferred return) per year, e.g. 0.08 = 8%</param>
    /// <param name="holdingYears">Number of years capital was held (to compound preferred return)</param>
    /// <param name="carryRate">GP's share of profits above hurdle (default 0.20 = 20%)</param>
    public static double CarriedInterest(
        double totalDistributions, double investedCapital,
        double preferredReturn = 0.08, double holdingYears = 1.0, double carryRate = 0.20)
    {
        double preferredAmount = investedCapital * Math.Pow(1 + preferredReturn, holdingYears);
        double profitAboveHurdle = Math.Max(0, totalDistributions - preferredAmount);
        return profitAboveHurdle * carryRate;
    }

    /// <summary>
    /// Catch-up amount — the GP's additional payment to reach their full carry share
    /// after LP receives their preferred return (100% GP catch-up is standard).
    /// catchUp = max(0, carry / (1 - carryRate) - carry)
    /// i.e. the amount GP must receive so that their total equals carryRate of all profits.
    /// </summary>
    /// <param name="profit">Total profit above invested capital</param>
    /// <param name="preferredReturnAmount">Dollar amount of preferred return paid to LPs</param>
    /// <param name="carryRate">GP carry rate (default 0.20)</param>
    /// <param name="catchUpRate">GP's share during catch-up phase (default 1.0 = 100%)</param>
    public static double CatchUpAmount(
        double profit, double preferredReturnAmount,
        double carryRate = 0.20, double catchUpRate = 1.0)
    {
        double fullCarry   = profit * carryRate;
        double catchUpPool = Math.Max(0, profit - preferredReturnAmount);
        double gpCatchUp   = catchUpPool * catchUpRate;
        return Math.Min(gpCatchUp, fullCarry);
    }

    // ── Expense ratio / fee drag ──────────────────────────────────────────────

    /// <summary>
    /// Net return after an annual expense ratio.
    /// netReturn ≈ grossReturn − expenseRatio (linear approximation)
    /// Exact: netReturn = (1 + grossReturn) / (1 + expenseRatio) − 1
    /// </summary>
    /// <param name="grossReturn">Gross annual return (e.g. 0.10 = 10%)</param>
    /// <param name="annualExpenseRatio">Annual expense ratio (e.g. 0.01 = 1%)</param>
    public static double NetReturnAfterFees(double grossReturn, double annualExpenseRatio) =>
        (1 + grossReturn) / (1 + annualExpenseRatio) - 1;

    /// <summary>
    /// Total wealth lost to fees over N years of compounding.
    /// feeImpact = (1 + grossReturn)^years − (1 + netReturn)^years
    /// Expressed as a multiple of initial capital (e.g. 0.15 = lost 15× initial capital to fees).
    /// </summary>
    /// <param name="grossAnnualReturn">Gross annual return</param>
    /// <param name="annualExpenseRatio">Annual expense ratio</param>
    /// <param name="years">Investment horizon in years</param>
    public static double CumulativeFeeImpact(
        double grossAnnualReturn, double annualExpenseRatio, double years)
    {
        double net = NetReturnAfterFees(grossAnnualReturn, annualExpenseRatio);
        return Math.Pow(1 + grossAnnualReturn, years) - Math.Pow(1 + net, years);
    }

    // ── Transaction costs ─────────────────────────────────────────────────────

    /// <summary>
    /// Total round-trip transaction cost for a trade.
    /// cost = tradeValue × commissionRate + tradeValue × spreadBps / 10000 / 2
    /// The spread cost is halved because you pay half the spread on entry and half on exit.
    /// </summary>
    /// <param name="tradeValue">Notional value of the trade</param>
    /// <param name="commissionRate">Commission as a fraction of trade value (e.g. 0.001 = 10bps)</param>
    /// <param name="spreadBps">Bid-ask spread in basis points (e.g. 2 = 2bps)</param>
    public static double TotalTransactionCost(
        double tradeValue, double commissionRate, double spreadBps) =>
        tradeValue * commissionRate + tradeValue * spreadBps / 10_000 / 2;

    /// <summary>
    /// Market impact / slippage cost.
    /// cost = tradeValue × marketImpactBps / 10000
    /// </summary>
    public static double SlippageCost(double tradeValue, double marketImpactBps) =>
        tradeValue * marketImpactBps / 10_000;

    /// <summary>
    /// Break-even return needed to cover transaction costs.
    /// breakEven = totalCost / tradeValue
    /// </summary>
    public static double BreakEvenReturn(double totalCost, double tradeValue) =>
        tradeValue == 0 ? double.NaN : totalCost / tradeValue;
}
