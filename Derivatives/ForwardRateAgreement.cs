using System;

namespace AleksejCupic.FinancialMath.Derivatives;

/// <summary>
/// Forward Rate Agreement (FRA) pricing and settlement.
/// An FRA is an OTC contract locking in an interest rate for a future period.
/// The long receives the FRA rate R_K and pays the market rate R_M at settlement.
/// Based on Hull Chapter 4.
/// </summary>
public static class ForwardRateAgreement
{
    // ── Forward rate ──────────────────────────────────────────────────────────

    /// <summary>
    /// Implied continuously compounded forward rate between times t1 and t2
    /// derived from the zero curve.
    /// f(t1,t2) = (r2·t2 − r1·t1) / (t2 − t1)
    /// </summary>
    /// <param name="r1">Zero rate to t1 (continuously compounded)</param>
    /// <param name="t1">Start of forward period in years</param>
    /// <param name="r2">Zero rate to t2 (continuously compounded)</param>
    /// <param name="t2">End of forward period in years (t2 &gt; t1)</param>
    public static double ForwardRate(double r1, double t1, double r2, double t2)
    {
        if (t2 <= t1) throw new ArgumentException("t2 must be greater than t1.");
        return (r2 * t2 - r1 * t1) / (t2 - t1);
    }

    /// <summary>
    /// Implied simply-compounded (LIBOR-style) forward rate for the period [t1, t2].
    /// Converts the continuously compounded forward rate to simple compounding over δ = t2 − t1.
    /// R_F = (exp(f · δ) − 1) / δ
    /// </summary>
    public static double ForwardRateSimple(double r1, double t1, double r2, double t2)
    {
        double delta = t2 - t1;
        double f     = ForwardRate(r1, t1, r2, t2);
        return (Math.Exp(f * delta) - 1) / delta;
    }

    // ── FRA pricing ───────────────────────────────────────────────────────────

    /// <summary>
    /// Present value of an FRA at inception or mark-to-market.
    /// The long position receives R_K and pays the market forward rate R_F.
    /// Settlement is at t1 (start of accrual period) discounted to today.
    ///
    /// V = L · (R_K − R_F) · δ · exp(−r2 · t2)
    ///
    /// where R_K and R_F are simply-compounded rates, L is notional,
    /// δ = t2 − t1, and the payment is discounted to today at the t2 zero rate.
    /// A positive value means the long position profits.
    /// </summary>
    /// <param name="notional">Notional principal (face amount)</param>
    /// <param name="fraRate">Agreed FRA rate R_K (simply compounded, e.g. 0.05 = 5%)</param>
    /// <param name="r1">Current zero rate to t1 (continuously compounded)</param>
    /// <param name="t1">Start of accrual period in years</param>
    /// <param name="r2">Current zero rate to t2 (continuously compounded)</param>
    /// <param name="t2">End of accrual period in years</param>
    /// <param name="isLong">True = long FRA (receives fixed R_K). False = short (pays fixed R_K).</param>
    public static double FraValue(
        double notional, double fraRate,
        double r1, double t1, double r2, double t2,
        bool isLong = true)
    {
        double rF    = ForwardRateSimple(r1, t1, r2, t2);
        double delta = t2 - t1;
        double value = notional * (fraRate - rF) * delta * Math.Exp(-r2 * t2);
        return isLong ? value : -value;
    }

    // ── FRA settlement ────────────────────────────────────────────────────────

    /// <summary>
    /// FRA settlement cash flow paid at t1 (start of accrual period).
    /// The settlement amount is discounted from t2 back to t1 at the market rate R_M.
    /// Settlement = L · (R_K − R_M) · δ / (1 + R_M · δ)
    /// A positive value = cash received by the long position.
    /// </summary>
    /// <param name="notional">Notional principal</param>
    /// <param name="fraRate">Agreed FRA rate R_K (simply compounded)</param>
    /// <param name="marketRate">Realised / market rate R_M at settlement (simply compounded)</param>
    /// <param name="t1">Start of accrual period in years (also the settlement date)</param>
    /// <param name="t2">End of accrual period in years</param>
    /// <param name="isLong">True = long FRA receives settlement if R_K &gt; R_M</param>
    public static double FraSettlement(
        double notional, double fraRate, double marketRate,
        double t1, double t2, bool isLong = true)
    {
        double delta      = t2 - t1;
        double settlement = notional * (fraRate - marketRate) * delta / (1 + marketRate * delta);
        return isLong ? settlement : -settlement;
    }

    // ── DV01 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// DV01 of an FRA — change in present value for a 1 bp parallel shift in zero rates.
    /// Computed via central finite difference.
    /// </summary>
    public static double DV01(
        double notional, double fraRate,
        double r1, double t1, double r2, double t2,
        bool isLong = true)
    {
        const double shift = 0.0001;
        double vUp   = FraValue(notional, fraRate, r1 + shift, t1, r2 + shift, t2, isLong);
        double vDown = FraValue(notional, fraRate, r1 - shift, t1, r2 - shift, t2, isLong);
        return (vUp - vDown) / 2;
    }
}
