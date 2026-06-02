using System;

namespace AleksejCupic.FinancialMath.Bonds;

/// <summary>
/// Fixed-rate mortgage and loan amortisation mathematics.
/// Covers payment calculation, outstanding balance, amortisation schedule,
/// and effective annual rate conversions.
/// </summary>
public static class MortgageMath
{
    // ── Payment calculation ───────────────────────────────────────────────────

    /// <summary>
    /// Periodic (e.g. monthly) payment for a fully amortising fixed-rate loan.
    /// M = P · r · (1 + r)^n / ((1 + r)^n − 1)
    /// where r is the periodic rate and n is the number of periods.
    /// </summary>
    /// <param name="principal">Loan amount</param>
    /// <param name="annualRate">Annual nominal interest rate (e.g. 0.06 = 6%)</param>
    /// <param name="years">Loan term in years</param>
    /// <param name="paymentsPerYear">Payment frequency per year (default 12 = monthly)</param>
    public static double Payment(
        double principal, double annualRate, double years, int paymentsPerYear = 12)
    {
        int n     = (int)Math.Round(years * paymentsPerYear);
        double r  = annualRate / paymentsPerYear;
        if (r < 1e-14) return principal / n; // zero-rate edge case
        double factor = Math.Pow(1 + r, n);
        return principal * r * factor / (factor - 1);
    }

    // ── Outstanding balance ───────────────────────────────────────────────────

    /// <summary>
    /// Outstanding loan balance after k payments have been made.
    /// B_k = P · ((1+r)^n − (1+r)^k) / ((1+r)^n − 1)
    /// Equivalently, the PV of remaining (n − k) payments.
    /// </summary>
    /// <param name="principal">Original loan amount</param>
    /// <param name="annualRate">Annual nominal interest rate</param>
    /// <param name="years">Total loan term in years</param>
    /// <param name="paymentsMade">Number of payments already made</param>
    /// <param name="paymentsPerYear">Payment frequency per year (default 12)</param>
    public static double OutstandingBalance(
        double principal, double annualRate, double years,
        int paymentsMade, int paymentsPerYear = 12)
    {
        int n    = (int)Math.Round(years * paymentsPerYear);
        double r = annualRate / paymentsPerYear;
        if (r < 1e-14) return principal * (1 - (double)paymentsMade / n);
        double factor = Math.Pow(1 + r, n);
        double paid   = Math.Pow(1 + r, paymentsMade);
        return principal * (factor - paid) / (factor - 1);
    }

    // ── Amortisation schedule ─────────────────────────────────────────────────

    /// <summary>
    /// Full amortisation schedule for a fixed-rate loan.
    /// Returns one row per payment: (payment number, payment, interest, principal, balance).
    /// </summary>
    /// <param name="principal">Loan amount</param>
    /// <param name="annualRate">Annual nominal interest rate</param>
    /// <param name="years">Loan term in years</param>
    /// <param name="paymentsPerYear">Payment frequency per year (default 12)</param>
    public static AmortisationRow[] AmortisationSchedule(
        double principal, double annualRate, double years, int paymentsPerYear = 12)
    {
        int n         = (int)Math.Round(years * paymentsPerYear);
        double r      = annualRate / paymentsPerYear;
        double m      = Payment(principal, annualRate, years, paymentsPerYear);
        var schedule  = new AmortisationRow[n];
        double balance = principal;

        for (int k = 1; k <= n; k++)
        {
            double interest   = balance * r;
            double principalP = m - interest;
            balance          -= principalP;
            if (k == n) balance = 0; // eliminate floating-point drift on last payment

            schedule[k - 1] = new AmortisationRow
            {
                Period        = k,
                Payment       = m,
                InterestPaid  = interest,
                PrincipalPaid = principalP,
                Balance       = Math.Max(balance, 0)
            };
        }
        return schedule;
    }

    // ── Interest totals ───────────────────────────────────────────────────────

    /// <summary>
    /// Total interest paid over the life of the loan.
    /// Total interest = n · M − P
    /// </summary>
    public static double TotalInterest(
        double principal, double annualRate, double years, int paymentsPerYear = 12)
    {
        int n    = (int)Math.Round(years * paymentsPerYear);
        double m = Payment(principal, annualRate, years, paymentsPerYear);
        return n * m - principal;
    }

    // ── Rate conversions ──────────────────────────────────────────────────────

    /// <summary>
    /// Effective Annual Rate (EAR) from a nominal rate compounded m times per year.
    /// EAR = (1 + r/m)^m − 1
    /// </summary>
    /// <param name="nominalRate">Nominal annual interest rate (e.g. 0.06 = 6%)</param>
    /// <param name="frequency">Compounding frequency per year</param>
    public static double EffectiveAnnualRate(double nominalRate, int frequency) =>
        Math.Pow(1 + nominalRate / frequency, frequency) - 1;

    /// <summary>
    /// Annual Percentage Rate (APR) from periodic rate.
    /// APR = r_periodic · paymentsPerYear (simple multiplication, no compounding).
    /// </summary>
    public static double AnnualPercentageRate(double periodicRate, int paymentsPerYear) =>
        periodicRate * paymentsPerYear;

    /// <summary>
    /// Converts an APR (nominal, compounded m times per year) to a continuously
    /// compounded rate: r_cont = m · ln(1 + APR/m).
    /// </summary>
    public static double AprToContinuous(double apr, int frequency) =>
        frequency * Math.Log(1 + apr / frequency);

    // ── Result type ───────────────────────────────────────────────────────────

    /// <summary>One row of an amortisation schedule.</summary>
    public readonly struct AmortisationRow
    {
        /// <summary>Payment number (1-indexed)</summary>
        public int Period { get; init; }
        /// <summary>Total payment amount (interest + principal)</summary>
        public double Payment { get; init; }
        /// <summary>Interest component of this payment</summary>
        public double InterestPaid { get; init; }
        /// <summary>Principal repaid in this payment</summary>
        public double PrincipalPaid { get; init; }
        /// <summary>Outstanding balance after this payment</summary>
        public double Balance { get; init; }
    }
}
