using System;

namespace AleksejCupic.FinancialMath.Derivatives;

/// <summary>
/// Black's (1976) model for pricing interest rate derivatives.
/// Used for caps, floors, and swaptions, assuming log-normal forward rates.
/// Based on Hull Chapter 29.
/// </summary>
public static class BlackModel
{
    // ── Caplets and Floorlets ─────────────────────────────────────────────────

    /// <summary>
    /// Price of a single caplet (or floorlet) using Black's model.
    /// Caplet: N · δ · [F · N(d1) − K · N(d2)] · exp(−r · T)
    /// Floorlet: N · δ · [K · N(−d2) − F · N(−d1)] · exp(−r · T)
    /// </summary>
    /// <param name="notional">Notional principal</param>
    /// <param name="forwardRate">Forward interest rate for the accrual period (annualised)</param>
    /// <param name="strike">Cap / floor strike rate (annualised)</param>
    /// <param name="t">Time to the start of the accrual period in years (option expiry)</param>
    /// <param name="r">Continuously compounded zero rate to T</param>
    /// <param name="sigma">Volatility of the forward rate (Black vol)</param>
    /// <param name="accrualFraction">Length of the accrual period in years (e.g. 0.5 for semi-annual)</param>
    /// <param name="isFloor">True for floorlet, false for caplet (default)</param>
    public static double CapletPrice(
        double notional, double forwardRate, double strike,
        double t, double r, double sigma,
        double accrualFraction = 0.5, bool isFloor = false)
    {
        if (t <= 0)
        {
            double intrinsic = isFloor
                ? Math.Max(strike - forwardRate, 0)
                : Math.Max(forwardRate - strike, 0);
            return notional * accrualFraction * intrinsic;
        }
        var (d1, d2) = D1D2(forwardRate, strike, t, sigma);
        double disc  = Math.Exp(-r * t);
        double payoff = isFloor
            ? strike * N(-d2) - forwardRate * N(-d1)
            : forwardRate * N(d1) - strike * N(d2);
        return notional * accrualFraction * disc * payoff;
    }

    // ── Caps and Floors ───────────────────────────────────────────────────────

    /// <summary>
    /// Price of an interest rate cap — sum of caplets over the payment schedule.
    /// The cap pays max(F_i − K, 0) · δ_i · notional at each reset date.
    /// </summary>
    /// <param name="notional">Notional principal</param>
    /// <param name="strike">Cap strike rate (annualised)</param>
    /// <param name="sigma">Flat Black volatility for all caplets</param>
    /// <param name="paymentTimes">
    ///   Start times of each accrual period in years (these are the caplet expiry dates).
    ///   Length must match forwardRates and accrualFractions.
    /// </param>
    /// <param name="zeroRates">Continuously compounded zero rates at each payment time</param>
    /// <param name="forwardRates">Forward rates for each accrual period (annualised)</param>
    /// <param name="accrualFractions">Length of each accrual period in years (e.g. 0.5)</param>
    public static double CapPrice(
        double notional, double strike, double sigma,
        double[] paymentTimes, double[] zeroRates,
        double[] forwardRates, double[] accrualFractions)
    {
        ValidateArrays(paymentTimes, zeroRates, forwardRates, accrualFractions);
        double total = 0;
        for (int i = 0; i < paymentTimes.Length; i++)
            total += CapletPrice(notional, forwardRates[i], strike,
                                 paymentTimes[i], zeroRates[i], sigma,
                                 accrualFractions[i], isFloor: false);
        return total;
    }

    /// <summary>
    /// Price of an interest rate floor — sum of floorlets over the payment schedule.
    /// </summary>
    public static double FloorPrice(
        double notional, double strike, double sigma,
        double[] paymentTimes, double[] zeroRates,
        double[] forwardRates, double[] accrualFractions)
    {
        ValidateArrays(paymentTimes, zeroRates, forwardRates, accrualFractions);
        double total = 0;
        for (int i = 0; i < paymentTimes.Length; i++)
            total += CapletPrice(notional, forwardRates[i], strike,
                                 paymentTimes[i], zeroRates[i], sigma,
                                 accrualFractions[i], isFloor: true);
        return total;
    }

    // ── Swaptions ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Forward swap rate — the fixed rate that makes an at-the-money swaption worth zero.
    /// R = (P(t_0) − P(t_n)) / Σ(δ_i · P(t_i))
    /// where P(t) = exp(−zeroRate(t) · t).
    /// </summary>
    /// <param name="paymentTimes">
    ///   Swap payment times in years (ascending). The first element t_0 is the swaption expiry /
    ///   swap start; the last element t_n is the swap maturity.
    /// </param>
    /// <param name="zeroRates">Zero rates at each payment time</param>
    /// <param name="accrualFractions">Accrual fraction for each coupon period</param>
    public static double ForwardSwapRate(
        double[] paymentTimes, double[] zeroRates, double[] accrualFractions)
    {
        if (paymentTimes.Length != zeroRates.Length || paymentTimes.Length != accrualFractions.Length)
            throw new ArgumentException("All arrays must have the same length.");
        if (paymentTimes.Length < 2)
            throw new ArgumentException("At least two payment times are required.");

        double p0 = Math.Exp(-zeroRates[0]  * paymentTimes[0]);
        double pn = Math.Exp(-zeroRates[^1] * paymentTimes[^1]);

        double annuity = 0;
        for (int i = 0; i < paymentTimes.Length; i++)
            annuity += accrualFractions[i] * Math.Exp(-zeroRates[i] * paymentTimes[i]);

        return (p0 - pn) / annuity;
    }

    /// <summary>
    /// Swaption price via Black's model.
    /// The swaption gives the holder the right to enter a swap at expiry T_0.
    /// Value = A · [R · N(d1) − K · N(d2)]  (payer swaption, right to pay fixed K)
    /// Value = A · [K · N(−d2) − R · N(−d1)] (receiver swaption, right to receive fixed K)
    /// where A is the annuity (PV01) of the underlying swap and R is the forward swap rate.
    /// </summary>
    /// <param name="notional">Notional principal</param>
    /// <param name="strike">Fixed strike rate of the underlying swap</param>
    /// <param name="t">Time to swaption expiry in years (= swap start date)</param>
    /// <param name="sigma">Black volatility of the forward swap rate</param>
    /// <param name="paymentTimes">
    ///   Swap coupon payment times in years (ascending, starting from swap start t).
    /// </param>
    /// <param name="zeroRates">Zero rates at each payment time</param>
    /// <param name="accrualFractions">Accrual fraction for each coupon period</param>
    /// <param name="isPayer">True = payer swaption (right to pay fixed). False = receiver.</param>
    public static double SwaptionPrice(
        double notional, double strike, double t, double sigma,
        double[] paymentTimes, double[] zeroRates, double[] accrualFractions,
        bool isPayer = true)
    {
        double r0 = ForwardSwapRate(paymentTimes, zeroRates, accrualFractions);
        double annuity = 0;
        for (int i = 0; i < paymentTimes.Length; i++)
            annuity += accrualFractions[i] * Math.Exp(-zeroRates[i] * paymentTimes[i]);

        if (t <= 0)
        {
            double intrinsic = isPayer
                ? Math.Max(r0 - strike, 0)
                : Math.Max(strike - r0, 0);
            return notional * annuity * intrinsic;
        }

        var (d1, d2) = D1D2(r0, strike, t, sigma);
        double payoff = isPayer
            ? r0 * N(d1) - strike * N(d2)
            : strike * N(-d2) - r0 * N(-d1);

        return notional * annuity * payoff;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    // Black's model d1/d2 using the forward rate F in place of the asset price.
    private static (double d1, double d2) D1D2(double f, double k, double t, double sigma)
    {
        double d1 = (Math.Log(f / k) + 0.5 * sigma * sigma * t) / (sigma * Math.Sqrt(t));
        return (d1, d1 - sigma * Math.Sqrt(t));
    }

    private static double N(double x)
    {
        const double a1 =  0.254829592, a2 = -0.284496736, a3 =  1.421413741;
        const double a4 = -1.453152027, a5 =  1.061405429, p  =  0.3275911;
        double sign = x < 0 ? -1 : 1;
        x = Math.Abs(x);
        double t = 1.0 / (1.0 + p * x);
        double poly = t * (a1 + t * (a2 + t * (a3 + t * (a4 + t * a5))));
        return 0.5 * (1.0 + sign * (1.0 - poly * Math.Exp(-x * x / 2)));
    }

    private static void ValidateArrays(params double[][] arrays)
    {
        int len = arrays[0].Length;
        foreach (var arr in arrays)
            if (arr.Length != len)
                throw new ArgumentException("All input arrays must have the same length.");
    }
}
