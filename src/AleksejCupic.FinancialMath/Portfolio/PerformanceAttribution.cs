using System;

namespace AleksejCupic.FinancialMath.Portfolio;

/// <summary>
/// Portfolio return calculations and Brinson-Hood-Beebower (1986) performance attribution.
/// Covers time-weighted return, Modified Dietz, IRR (money-weighted return), NPV,
/// and decomposition of active return into allocation, selection, and interaction effects.
/// </summary>
public static class PerformanceAttribution
{
    // ── Return calculations ───────────────────────────────────────────────────

    /// <summary>
    /// Time-Weighted Return (TWR) — chain-links sub-period returns.
    /// Eliminates the distorting effect of external cash flows. Industry standard for
    /// comparing manager performance (GIPS-compliant).
    /// TWR = Π(1 + r_i) − 1
    /// </summary>
    /// <param name="subPeriodReturns">
    ///   Simple returns for each sub-period (e.g. daily, monthly).
    ///   Each sub-period should be calculated between cash flow dates.
    /// </param>
    public static double TimeWeightedReturn(double[] subPeriodReturns)
    {
        double product = 1.0;
        foreach (double r in subPeriodReturns) product *= 1 + r;
        return product - 1;
    }

    /// <summary>
    /// Modified Dietz return — approximates the time-weighted return when sub-period
    /// valuations are unavailable. Weights each external cash flow by the fraction of
    /// the period it was invested.
    /// MDietz = (EV − BV − ΣCF) / (BV + Σ(CF_i × W_i))
    /// W_i = (totalDays − dayOfCashFlow_i) / totalDays
    /// </summary>
    /// <param name="startValue">Portfolio value at the start of the period (BV)</param>
    /// <param name="endValue">Portfolio value at the end of the period (EV)</param>
    /// <param name="cashFlows">External cash flows during the period (positive = contribution, negative = withdrawal)</param>
    /// <param name="cashFlowDays">Day number of each cash flow within the period (0 = start, totalDays = end)</param>
    /// <param name="totalDays">Total number of days in the period</param>
    public static double ModifiedDietz(
        double startValue, double endValue,
        double[] cashFlows, double[] cashFlowDays, double totalDays)
    {
        if (cashFlows.Length != cashFlowDays.Length)
            throw new ArgumentException("cashFlows and cashFlowDays must be the same length.");

        double sumCf  = 0, weightedCf = 0;
        for (int i = 0; i < cashFlows.Length; i++)
        {
            double w    = (totalDays - cashFlowDays[i]) / totalDays;
            sumCf      += cashFlows[i];
            weightedCf += cashFlows[i] * w;
        }

        double denominator = startValue + weightedCf;
        return denominator == 0 ? double.NaN
            : (endValue - startValue - sumCf) / denominator;
    }

    /// <summary>
    /// Net Present Value of a series of cash flows discounted at rate r.
    /// NPV = Σ CF_i × exp(−r × t_i)
    /// Positive NPV = project adds value at this discount rate.
    /// </summary>
    /// <param name="cashFlows">Cash flows (positive = inflow, negative = outflow)</param>
    /// <param name="times">Time of each cash flow in years</param>
    /// <param name="discountRate">Continuously compounded discount rate</param>
    public static double NetPresentValue(double[] cashFlows, double[] times, double discountRate)
    {
        if (cashFlows.Length != times.Length)
            throw new ArgumentException("cashFlows and times must be the same length.");
        double npv = 0;
        for (int i = 0; i < cashFlows.Length; i++)
            npv += cashFlows[i] * Math.Exp(-discountRate * times[i]);
        return npv;
    }

    /// <summary>
    /// Internal Rate of Return (IRR) — the continuously compounded discount rate that
    /// makes NPV = 0. This is the money-weighted return (MWR) and accounts for the
    /// timing of cash flows. Solved via Newton-Raphson.
    /// </summary>
    /// <param name="cashFlows">
    ///   Cash flows: typically the first is negative (initial investment), last is positive (exit).
    /// </param>
    /// <param name="times">Time of each cash flow in years</param>
    /// <param name="guess">Initial guess for IRR (default 0.10 = 10%)</param>
    /// <param name="tol">Convergence tolerance (default 1e-8)</param>
    /// <param name="maxIter">Maximum Newton-Raphson iterations (default 200)</param>
    /// <returns>IRR as a continuously compounded rate, or NaN if no convergence</returns>
    public static double InternalRateOfReturn(
        double[] cashFlows, double[] times,
        double guess = 0.10, double tol = 1e-8, int maxIter = 200)
    {
        if (cashFlows.Length != times.Length)
            throw new ArgumentException("cashFlows and times must be the same length.");

        double r = guess;
        for (int iter = 0; iter < maxIter; iter++)
        {
            double npv  = 0, dNpv = 0;
            for (int i = 0; i < cashFlows.Length; i++)
            {
                double df  = Math.Exp(-r * times[i]);
                npv       += cashFlows[i] * df;
                dNpv      -= cashFlows[i] * times[i] * df; // dNPV/dr
            }
            if (Math.Abs(dNpv) < 1e-14) return double.NaN;
            double rNew = r - npv / dNpv;
            if (Math.Abs(rNew - r) < tol) return rNew;
            r = rNew;
        }
        return double.NaN;
    }

    // ── Brinson-Hood-Beebower attribution (BHB 1986) ─────────────────────────
    //
    // Decomposes a portfolio's active return vs benchmark into three effects:
    //   Allocation:  did the portfolio over/underweight sectors that outperformed?
    //   Selection:   did the portfolio pick better stocks within each sector?
    //   Interaction: combined effect of weight and stock selection decisions.
    //
    // For sector i:
    //   Allocation  = (w_p_i − w_b_i) × (r_b_i − R_b)
    //   Selection   = w_b_i × (r_p_i − r_b_i)
    //   Interaction = (w_p_i − w_b_i) × (r_p_i − r_b_i)
    //
    // Total active return = Σ(Allocation_i + Selection_i + Interaction_i)

    /// <summary>
    /// Allocation effect for a single sector.
    /// Measures the value added by overweighting/underweighting the sector.
    /// alloc = (w_portfolio − w_benchmark) × (r_benchmark_sector − r_benchmark_total)
    /// </summary>
    public static double AllocationEffect(
        double portfolioWeight, double benchmarkWeight,
        double benchmarkSectorReturn, double totalBenchmarkReturn) =>
        (portfolioWeight - benchmarkWeight) * (benchmarkSectorReturn - totalBenchmarkReturn);

    /// <summary>
    /// Selection effect for a single sector.
    /// Measures the value added by picking better securities within the sector.
    /// select = w_benchmark × (r_portfolio_sector − r_benchmark_sector)
    /// </summary>
    public static double SelectionEffect(
        double benchmarkWeight, double portfolioSectorReturn, double benchmarkSectorReturn) =>
        benchmarkWeight * (portfolioSectorReturn - benchmarkSectorReturn);

    /// <summary>
    /// Interaction effect for a single sector.
    /// Captures the combined impact of weight and selection decisions.
    /// interact = (w_portfolio − w_benchmark) × (r_portfolio_sector − r_benchmark_sector)
    /// </summary>
    public static double InteractionEffect(
        double portfolioWeight, double benchmarkWeight,
        double portfolioSectorReturn, double benchmarkSectorReturn) =>
        (portfolioWeight - benchmarkWeight) * (portfolioSectorReturn - benchmarkSectorReturn);

    /// <summary>
    /// Active return: portfolio return minus benchmark return.
    /// </summary>
    public static double ActiveReturn(double portfolioReturn, double benchmarkReturn) =>
        portfolioReturn - benchmarkReturn;

    /// <summary>
    /// Full Brinson-Hood-Beebower attribution across all sectors.
    /// Returns the sum of each effect across all N sectors.
    /// </summary>
    /// <param name="portfolioWeights">Portfolio weights per sector (length N, should sum to 1)</param>
    /// <param name="benchmarkWeights">Benchmark weights per sector (length N, should sum to 1)</param>
    /// <param name="portfolioReturns">Portfolio return per sector</param>
    /// <param name="benchmarkReturns">Benchmark return per sector</param>
    /// <returns>
    ///   (totalAllocation, totalSelection, totalInteraction, totalActiveReturn)
    /// </returns>
    public static (double allocation, double selection, double interaction, double activeReturn)
        BhbAttribution(
            double[] portfolioWeights, double[] benchmarkWeights,
            double[] portfolioReturns, double[] benchmarkReturns)
    {
        int n = portfolioWeights.Length;
        if (benchmarkWeights.Length != n || portfolioReturns.Length != n || benchmarkReturns.Length != n)
            throw new ArgumentException("All arrays must be the same length.");

        // Total benchmark return = Σ w_b_i × r_b_i
        double totalBenchmark = 0;
        for (int i = 0; i < n; i++) totalBenchmark += benchmarkWeights[i] * benchmarkReturns[i];

        // Total portfolio return = Σ w_p_i × r_p_i
        double totalPortfolio = 0;
        for (int i = 0; i < n; i++) totalPortfolio += portfolioWeights[i] * portfolioReturns[i];

        double alloc = 0, select = 0, interact = 0;
        for (int i = 0; i < n; i++)
        {
            alloc   += AllocationEffect(portfolioWeights[i], benchmarkWeights[i],
                                        benchmarkReturns[i], totalBenchmark);
            select  += SelectionEffect(benchmarkWeights[i],
                                       portfolioReturns[i], benchmarkReturns[i]);
            interact += InteractionEffect(portfolioWeights[i], benchmarkWeights[i],
                                          portfolioReturns[i], benchmarkReturns[i]);
        }

        return (alloc, select, interact, totalPortfolio - totalBenchmark);
    }
}
