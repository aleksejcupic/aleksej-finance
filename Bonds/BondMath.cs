using System;

namespace AleksejCupic.FinancialMath.Bonds;

/// <summary>
/// Fixed-income calculations: price, yield to maturity, duration, convexity, and DV01.
/// All rates are annual. Coupon frequency defaults to semi-annual (2 per year).
/// </summary>
public static class BondMath
{
    // ── Price ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Present value of a bond given yield to maturity.
    /// </summary>
    /// <param name="face">Face (par) value</param>
    /// <param name="couponRate">Annual coupon rate (e.g. 0.05 for 5%)</param>
    /// <param name="ytm">Annual yield to maturity</param>
    /// <param name="years">Years to maturity</param>
    /// <param name="frequency">Coupon payments per year (default 2 = semi-annual)</param>
    public static double Price(double face, double couponRate, double ytm,
                               double years, int frequency = 2)
    {
        double coupon  = face * couponRate / frequency;
        double r       = ytm / frequency;
        int    periods = (int)Math.Round(years * frequency);
        double pv      = 0;
        for (int t = 1; t <= periods; t++)
            pv += coupon / Math.Pow(1 + r, t);
        pv += face / Math.Pow(1 + r, periods);
        return pv;
    }

    // ── Yield to Maturity ──────────────────────────────────────────────────────

    /// <summary>
    /// Annual yield to maturity via Newton-Raphson. Returns NaN if not converged.
    /// </summary>
    public static double YieldToMaturity(double price, double face, double couponRate,
                                         double years, int frequency = 2,
                                         double tol = 1e-8, int maxIter = 200)
    {
        double ytm = couponRate > 0 ? couponRate : 0.05; // initial guess

        for (int i = 0; i < maxIter; i++)
        {
            double f  = Price(face, couponRate, ytm, years, frequency) - price;
            double fp = DPriceDYtm(face, couponRate, ytm, years, frequency);
            if (Math.Abs(fp) < 1e-15) break;
            double next = ytm - f / fp;
            if (Math.Abs(next - ytm) < tol) return next;
            ytm = Math.Max(next, 1e-8); // keep positive
        }
        return double.NaN;
    }

    // ── Duration & Convexity ───────────────────────────────────────────────────

    /// <summary>Macaulay duration in years.</summary>
    public static double MacaulayDuration(double face, double couponRate, double ytm,
                                          double years, int frequency = 2)
    {
        double coupon  = face * couponRate / frequency;
        double r       = ytm / frequency;
        int    periods = (int)Math.Round(years * frequency);
        double price   = Price(face, couponRate, ytm, years, frequency);
        double weighted = 0;
        for (int t = 1; t <= periods; t++)
            weighted += t * coupon / Math.Pow(1 + r, t);
        weighted += periods * face / Math.Pow(1 + r, periods);
        return weighted / price / frequency; // convert periods to years
    }

    /// <summary>Modified duration = Macaulay duration / (1 + ytm/frequency).</summary>
    public static double ModifiedDuration(double face, double couponRate, double ytm,
                                          double years, int frequency = 2)
    {
        double mac = MacaulayDuration(face, couponRate, ytm, years, frequency);
        return mac / (1 + ytm / frequency);
    }

    /// <summary>Convexity — second-order price sensitivity to yield changes.</summary>
    public static double Convexity(double face, double couponRate, double ytm,
                                   double years, int frequency = 2)
    {
        double coupon  = face * couponRate / frequency;
        double r       = ytm / frequency;
        int    periods = (int)Math.Round(years * frequency);
        double price   = Price(face, couponRate, ytm, years, frequency);
        double conv    = 0;
        for (int t = 1; t <= periods; t++)
            conv += t * (t + 1) * coupon / Math.Pow(1 + r, t + 2);
        conv += periods * (periods + 1) * face / Math.Pow(1 + r, periods + 2);
        return conv / (price * frequency * frequency);
    }

    /// <summary>
    /// DV01 — dollar value of a 1 basis point (0.01%) yield change.
    /// DV01 = ModifiedDuration * Price * 0.0001
    /// </summary>
    public static double DV01(double face, double couponRate, double ytm,
                              double years, int frequency = 2)
    {
        double modDur = ModifiedDuration(face, couponRate, ytm, years, frequency);
        double price  = Price(face, couponRate, ytm, years, frequency);
        return modDur * price * 0.0001;
    }

    // ── Price change approximation ─────────────────────────────────────────────

    /// <summary>
    /// Approximate price change for a given yield change using duration + convexity.
    /// dP ≈ -ModDuration * P * dYield + 0.5 * Convexity * P * dYield²
    /// </summary>
    public static double ApproximatePriceChange(double face, double couponRate, double ytm,
                                                double years, double deltaYtm,
                                                int frequency = 2)
    {
        double price   = Price(face, couponRate, ytm, years, frequency);
        double modDur  = ModifiedDuration(face, couponRate, ytm, years, frequency);
        double conv    = Convexity(face, couponRate, ytm, years, frequency);
        return -modDur * price * deltaYtm + 0.5 * conv * price * deltaYtm * deltaYtm;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static double DPriceDYtm(double face, double couponRate, double ytm,
                                     double years, int frequency)
    {
        double coupon  = face * couponRate / frequency;
        double r       = ytm / frequency;
        int    periods = (int)Math.Round(years * frequency);
        double dp      = 0;
        for (int t = 1; t <= periods; t++)
            dp -= t * coupon / (frequency * Math.Pow(1 + r, t + 1));
        dp -= periods * face / (frequency * Math.Pow(1 + r, periods + 1));
        return dp;
    }
}
